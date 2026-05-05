using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Sequential pass: resolves/shapes each visible <see cref="BitmapText"/> row via <see cref="TextRuntimeBuilder.TryPrepare"/>
/// (glyph cache + fingerprint), then submits sprite quads.
/// </summary>
/// <remarks>
/// <para>
/// Viewport/swapchain rows enqueue into the renderer’s overlay queue (see <see cref="VulkanRenderer"/> HUD routing);
/// glyph counts therefore flow through the same <see cref="FramePlan"/> sort/count rules as other UI sprites.
/// </para>
/// <para>
/// Glyph layout runs here on the main thread immediately before <see cref="IRenderer.SubmitSprites"/> — do not register a
/// second “text build” pass that also calls <see cref="TextRuntimeBuilder.TryPrepare"/>; you would duplicate work and risk
/// races on <see cref="TextSpriteCache"/>.
/// </para>
/// <para>
/// Gameplay systems assign <see cref="BitmapText.Content"/> like any other component field; cache invalidation when copy or
/// layout-affecting inputs change is handled inside <see cref="TextRuntimeBuilder.TryPrepare"/>.
/// </para>
/// </remarks>
public sealed class TextRenderSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

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
        _ = deltaSeconds;
        var r = _host.Renderer;
        foreach (var chunk in query)
        {
            var texts = chunk.Column<BitmapText>();
            var transforms = chunk.Column<Transform>();
            var fingerprints = chunk.Column<TextBuildFingerprint>();
            var caches = chunk.Column<TextSpriteCache>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var bt = ref texts[i];
                ref var fingerprint = ref fingerprints[i];
                ref var cache = ref caches[i];
                ref readonly var transform = ref transforms[i];
                SubmitBitmapTextRow(ref bt, ref fingerprint, ref cache, in transform, r);
            }
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

        // GlyphCount is set inside TryPrepare from FillGlyphRunSprites; do not clamp with fingerprint.ResolvedCharCount —
        // a cleared/default fingerprint (e.g. ordering bugs) could read 0 and suppress all submits while the cache holds quads.
        var glyphSubmitCount = cache.GlyphCount;
        if (glyphSubmitCount > 0 && cache.CachedGlyphs is not null)
            renderer.SubmitSprites(cache.CachedGlyphs.AsSpan(0, glyphSubmitCount));

        if (!bt.Style.Underline && !bt.Style.Strikethrough)
            return;

        var baselineAuthored = cache.BaselineAuthored;
        var space = cache.Space;

        TextRenderer.SubmitTextDecorations(
            renderer,
            in bt.Style,
            baselineAuthored,
            0f,
            cache.PenAfter,
            bt.SortKey,
            renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId,
            space);
    }
}
