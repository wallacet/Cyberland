using System;
using System.Buffers;
using System.Collections.Generic;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Sequential late pass: draws entities that have both <see cref="BitmapText"/> and <see cref="Position"/> via <see cref="TextRenderer"/>.
/// </summary>
/// <remarks>
/// Runs after <see cref="TextStagingSystem"/> and mod systems update labels; register order places this after <see cref="SpriteRenderSystem"/> so typical HUD sort keys stack above world sprites.
/// Glyph rasterization uses a locked cache; this system runs on the main scheduler thread (sequential <see cref="ILateUpdate"/>).
/// <para>
/// The renderer clears sprite batches each frame, so text must be resubmitted every frame. When the resolved string, style, placement,
/// and framebuffer size (for screen-space baselines) are unchanged, this system replays the last frame&apos;s glyph quads without
/// re-running layout / atlas lookups, so mods can assign <see cref="BitmapText.Content"/> every frame without paying full text cost when the display is stable.
/// </para>
/// </remarks>
public sealed class TextRenderSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private bool _haveColumnMap;
    private int _colBitmapText;
    private int _colPosition;

    /// <summary>Per-entity cached glyph quads; keyed by <see cref="EntityId.Raw"/>.</summary>
    private readonly Dictionary<uint, CachedGlyphRow> _glyphRowCache = new();

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BitmapText, Position>();

    /// <param name="host">Uses <see cref="GameHostServices.Renderer"/>, <see cref="GameHostServices.Fonts"/>, <see cref="GameHostServices.TextGlyphCache"/>, and <see cref="GameHostServices.LocalizedContent"/>.</param>
    public TextRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query) =>
        EnsureColumnMap(world, query.Spec);

    /// <inheritdoc />
    public void OnLateUpdate(World world, ChunkQueryAll query, float deltaSeconds)
    {
        _ = deltaSeconds;
        if (!_haveColumnMap)
            EnsureColumnMap(world, query.Spec);

        var r = _host.Renderer;
        if (r is null)
        {
            PruneDeadCacheEntries(world);
            return;
        }

        var fonts = _host.Fonts;
        var textGlyphCache = _host.TextGlyphCache;
        var loc = _host.LocalizedContent?.Strings;
        var fb = r.SwapchainPixelSize;

        foreach (var chunk in query)
        {
            var texts = chunk.Column<BitmapText>(_colBitmapText);
            var positions = chunk.Column<Position>(_colPosition);
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var bt = ref texts[i];
                ref readonly var pos = ref positions[i];
                var entity = entities[i];
                RenderBitmapTextRow(entity, ref bt, in pos, r, fonts, textGlyphCache, loc, fb);
            }
        }

        PruneDeadCacheEntries(world);
    }

    private void RenderBitmapTextRow(
        EntityId entity,
        ref BitmapText bt,
        ref readonly Position pos,
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache glyphCache,
        LocalizationManager? localization,
        Vector2D<int> framebufferSize)
    {
        if (!bt.Visible)
        {
            _glyphRowCache.Remove(entity.Raw);
            return;
        }

        if (string.IsNullOrEmpty(bt.Content))
        {
            _glyphRowCache.Remove(entity.Raw);
            return;
        }

        string resolved;
        if (bt.IsLocalizationKey)
        {
            if (localization is null)
            {
                _glyphRowCache.Remove(entity.Raw);
                return;
            }

            resolved = localization.Get(bt.Content);
            if (string.IsNullOrEmpty(resolved))
            {
                _glyphRowCache.Remove(entity.Raw);
                return;
            }
        }
        else
        {
            resolved = bt.Content;
        }

        Vector2D<float> baselineWorld;
        int fbW;
        int fbH;
        if (bt.CoordinateSpace == TextCoordinateSpace.WorldBaseline)
        {
            baselineWorld = pos.AsVector();
            fbW = 0;
            fbH = 0;
        }
        else
        {
            baselineWorld = WorldScreenSpace.ScreenPixelToWorldCenter(pos.AsVector(), framebufferSize);
            fbW = framebufferSize.X;
            fbH = framebufferSize.Y;
        }

        var stamp = new TextRowCacheStamp(resolved, bt.Style, bt.CoordinateSpace, bt.SortKey, baselineWorld.X,
            baselineWorld.Y, fbW, fbH);

        if (bt.Style.Underline || bt.Style.Strikethrough)
        {
            // Decorations add extra sprites; keep using the full path and do not store glyph-only replay data.
            _glyphRowCache.Remove(entity.Raw);
            if (bt.CoordinateSpace == TextCoordinateSpace.WorldBaseline)
            {
                if (bt.IsLocalizationKey)
                {
                    // Localization was required to resolve <paramref name="resolved"/>; non-null here.
                    TextRenderer.DrawLocalized(renderer, fonts, glyphCache, localization!, in bt.Style, bt.Content,
                        baselineWorld, bt.SortKey);
                }
                else
                {
                    TextRenderer.DrawLiteral(renderer, fonts, glyphCache, in bt.Style, resolved, baselineWorld,
                        bt.SortKey);
                }
            }
            else
            {
                var screen = pos.AsVector();
                if (bt.IsLocalizationKey)
                {
                    TextRenderer.DrawLocalizedScreen(renderer, fonts, glyphCache, localization!, in bt.Style, bt.Content,
                        screen, framebufferSize, bt.SortKey);
                }
                else
                {
                    TextRenderer.DrawLiteralScreen(renderer, fonts, glyphCache, in bt.Style, resolved, screen,
                        framebufferSize, bt.SortKey);
                }
            }

            return;
        }

        if (_glyphRowCache.TryGetValue(entity.Raw, out var cached) && cached.Stamp == stamp)
        {
            if (cached.Count > 0)
                renderer.SubmitSprites(cached.Sprites.AsSpan(0, cached.Count));
            return;
        }

        var pool = ArrayPool<SpriteDrawRequest>.Shared;
        var buf = pool.Rent(resolved.Length);
        try
        {
            var dest = buf.AsSpan(0, resolved.Length);
            var n = TextRenderer.FillGlyphRunSprites(renderer, fonts, glyphCache, resolved, in bt.Style, baselineWorld,
                0f, bt.SortKey, dest, out _);
            if (!_glyphRowCache.TryGetValue(entity.Raw, out var row))
                row = new CachedGlyphRow();
            if (row.Sprites.Length < n)
                row.Sprites = new SpriteDrawRequest[n];
            if (n > 0)
                dest[..n].CopyTo(row.Sprites.AsSpan(0, n));
            row.Count = n;
            row.Stamp = stamp;
            _glyphRowCache[entity.Raw] = row;
            if (n > 0)
                renderer.SubmitSprites(row.Sprites.AsSpan(0, n));
        }
        finally
        {
            pool.Return(buf);
        }
    }

    private void PruneDeadCacheEntries(World world)
    {
        if (_glyphRowCache.Count == 0)
            return;

        var keys = new uint[_glyphRowCache.Count];
        _glyphRowCache.Keys.CopyTo(keys, 0);
        foreach (var raw in keys)
        {
            if (!world.IsAlive(new EntityId(raw)))
                _glyphRowCache.Remove(raw);
        }
    }

    private void EnsureColumnMap(World world, SystemQuerySpec spec)
    {
        _colBitmapText = world.GetQueryColumnIndex<BitmapText>(spec);
        _colPosition = world.GetQueryColumnIndex<Position>(spec);
        _haveColumnMap = true;
    }

    /// <summary>Fingerprint for replaying last frame&apos;s glyph quads when inputs are unchanged.</summary>
    private readonly struct TextRowCacheStamp : IEquatable<TextRowCacheStamp>
    {
        public TextRowCacheStamp(
            string resolvedText,
            TextStyle style,
            TextCoordinateSpace coordinateSpace,
            float sortKey,
            float baselineWorldX,
            float baselineWorldY,
            int framebufferW,
            int framebufferH)
        {
            ResolvedText = resolvedText;
            Style = style;
            CoordinateSpace = coordinateSpace;
            SortKey = sortKey;
            BaselineWorldX = baselineWorldX;
            BaselineWorldY = baselineWorldY;
            FramebufferW = framebufferW;
            FramebufferH = framebufferH;
        }

        public string ResolvedText { get; }
        public TextStyle Style { get; }
        public TextCoordinateSpace CoordinateSpace { get; }
        public float SortKey { get; }
        public float BaselineWorldX { get; }
        public float BaselineWorldY { get; }
        public int FramebufferW { get; }
        public int FramebufferH { get; }

        public bool Equals(TextRowCacheStamp other) =>
            string.Equals(ResolvedText, other.ResolvedText, StringComparison.Ordinal) &&
            Style == other.Style &&
            CoordinateSpace == other.CoordinateSpace &&
            SortKey == other.SortKey &&
            BaselineWorldX == other.BaselineWorldX &&
            BaselineWorldY == other.BaselineWorldY &&
            FramebufferW == other.FramebufferW &&
            FramebufferH == other.FramebufferH;

        public override bool Equals(object? obj) => obj is TextRowCacheStamp o && Equals(o);

        public override int GetHashCode() =>
            HashCode.Combine(ResolvedText, Style, CoordinateSpace, SortKey, BaselineWorldX, BaselineWorldY, FramebufferW,
                FramebufferH);

        public static bool operator ==(TextRowCacheStamp a, TextRowCacheStamp b) => a.Equals(b);
        public static bool operator !=(TextRowCacheStamp a, TextRowCacheStamp b) => !a.Equals(b);
    }

    private sealed class CachedGlyphRow
    {
        public TextRowCacheStamp Stamp;
        public SpriteDrawRequest[] Sprites = [];
        public int Count;
    }
}
