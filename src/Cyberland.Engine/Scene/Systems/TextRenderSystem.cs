using System;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Sequential pass: resolves/shapes each visible <see cref="BitmapText"/> row via <see cref="TextRuntimeBuilder.TryPrepare"/>
/// (glyph cache + fingerprint), then submits MSDF glyphs and optional decoration sprites.
/// </summary>
/// <remarks>
/// <para>
/// Viewport/swapchain rows enqueue into the renderer’s overlay queue (see <see cref="VulkanRenderer"/> HUD routing);
/// glyph counts therefore flow through the same <see cref="FramePlan"/> sort/count rules as other UI sprites.
/// </para>
/// <para>
/// Glyph layout runs here on the main thread immediately before <see cref="IRenderer.SubmitTextGlyphs"/> — do not register a
/// second “text build” pass that also calls <see cref="TextRuntimeBuilder.TryPrepare"/>; you would duplicate work and risk
/// races on <see cref="TextSpriteCache"/>.
/// </para>
/// <para>
/// This pass remains serial because <see cref="TextSpriteCache"/> and render submission ordering are mutated in place. Future
/// parallelization should split immutable shaping/prep from serial submit, with deterministic merge order preserved.
/// </para>
/// <para>
/// Gameplay systems assign <see cref="BitmapText.Content"/> like any other component field; cache invalidation when copy or
/// layout-affecting inputs change is handled inside <see cref="TextRuntimeBuilder.TryPrepare"/>.
/// </para>
/// </remarks>
public sealed class TextRenderSystem : ISystem, ILateUpdate, IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;
    private const int ParallelRangeSize = 128;
    private const int SmallChunkSerialThreshold = 64;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec =>
        SystemQuerySpec.All<BitmapText, Transform, TextBuildFingerprint, TextSpriteCache>();

    /// <param name="host">Uses <see cref="GameHostServices.Renderer"/>, <see cref="GameHostServices.Fonts"/>, <see cref="GameHostServices.TextGlyphCache"/>, and <see cref="GameHostServices.LocalizedContent"/>.</param>
    public TextRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        OnParallelLateUpdate(query, deltaSeconds, new ParallelOptions { MaxDegreeOfParallelism = 1 });
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions options)
    {
#if DEBUG
        using var frameScope = FrameProfilerScope.Enter("text.render.frame");
#endif
        _ = deltaSeconds;
        var r = _host.Renderer;
        foreach (var chunk in query)
        {
#if DEBUG
            using var chunkScope = FrameProfilerScope.Enter("text.render.chunk");
#endif
            if (chunk.Count <= SmallChunkSerialThreshold || options.MaxDegreeOfParallelism == 1)
            {
                SubmitChunkRange(chunk, 0, chunk.Count, r);
                continue;
            }

            Parallel.ForEach(Partitioner.Create(0, chunk.Count, ParallelRangeSize), options, range =>
            {
                SubmitChunkRange(chunk, range.Item1, range.Item2, r);
            });
        }
    }

    private void SubmitChunkRange(in MultiComponentChunkView chunk, int start, int endExclusive, IRenderer renderer)
    {
        var texts = chunk.Column<BitmapText>();
        var transforms = chunk.Column<Transform>();
        var fingerprints = chunk.Column<TextBuildFingerprint>();
        var caches = chunk.Column<TextSpriteCache>();
        for (var i = start; i < endExclusive; i++)
        {
#if DEBUG
            using var rowScope = FrameProfilerScope.Enter("text.render.row");
#endif
            ref var bt = ref texts[i];
            ref var fingerprint = ref fingerprints[i];
            ref var cache = ref caches[i];
            ref readonly var transform = ref transforms[i];
            SubmitBitmapTextRow(ref bt, ref fingerprint, ref cache, in transform, renderer);
        }
    }

    private void SubmitBitmapTextRow(
        ref BitmapText bt,
        ref TextBuildFingerprint fingerprint,
        ref TextSpriteCache cache,
        ref readonly Transform transform,
        IRenderer renderer)
    {
        // Must run before the glyph-cache replay path: invisible rows still hold last frame's TextSpriteCache.
        if (!bt.Visible)
        {
            TextRuntimeBuilder.DiscardPreparedRow(ref cache, ref fingerprint);
            return;
        }

        if (!TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, _host, renderer, out _, out _))
            return;

        // GlyphCount is set inside TryPrepare from FillGlyphRunGlyphs; do not clamp with fingerprint.ResolvedCharCount —
        // a cleared/default fingerprint (e.g. ordering bugs) could read 0 and suppress all submits while the cache holds quads.
        var glyphSubmitCount = cache.GlyphCount;
        if (glyphSubmitCount > 0 && cache.CachedGlyphs is not null)
            renderer.SubmitTextGlyphs(cache.CachedGlyphs.AsSpan(0, glyphSubmitCount));

        if (!bt.Style.Underline && !bt.Style.Strikethrough)
            return;

        var baselineAuthored = cache.BaselineAuthored;
        var space = cache.Space;
        var applyViewportClip = TryGetDecorationClipRect(space, renderer, out var viewportClip);

        // Viewport/swapchain: same baseline-left as FillGlyphRunGlyphs / SubmitGlyphRunWithDecorations — AddDecorations snaps.
        // World space: recover Y from an emitted quad when camera-space baseline and quad centers can diverge.
        var decorBaseline = baselineAuthored;
        ReadOnlySpan<TextGlyphDrawRequest> ink = default;
        if (glyphSubmitCount > 0 && cache.CachedGlyphs is not null)
        {
            ink = cache.CachedGlyphs.AsSpan(0, glyphSubmitCount);
            if (space is not CoordinateSpace.ViewportSpace and not CoordinateSpace.SwapchainSpace)
            {
                var y = TextRenderer.RecoverBaselineYFromGlyph(in ink[0]);
                decorBaseline = new Vector2D<float>(baselineAuthored.X, y);
            }
        }

        TextRenderer.SubmitTextDecorations(
            renderer,
            in bt.Style,
            decorBaseline,
            0f,
            cache.PenAfter,
            bt.SortKey,
            renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId,
            space,
            applyViewportClip,
            viewportClip,
            _host.Fonts,
            ink);
    }

    private static bool TryGetDecorationClipRect(CoordinateSpace space, IRenderer renderer, out UiRect viewportClip)
    {
        switch (space)
        {
            case CoordinateSpace.ViewportSpace:
            {
                var viewport = renderer.ActiveCameraViewportSize;
                viewportClip = new UiRect(0f, 0f, viewport.X, viewport.Y);
                return viewport.X > 0 && viewport.Y > 0;
            }
            case CoordinateSpace.SwapchainSpace:
            {
                var swapchain = renderer.SwapchainPixelSize;
                viewportClip = new UiRect(0f, 0f, swapchain.X, swapchain.Y);
                return swapchain.X > 0 && swapchain.Y > 0;
            }
            default:
                viewportClip = default;
                return false;
        }
    }
}
