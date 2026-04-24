using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Sequential deterministic submit pass for prebuilt text sprite runs.
/// </summary>
/// <remarks>
/// Runtime sprite runs are expected to be built by <see cref="TextBuildSystem"/>. This pass preserves stable ordering by
/// iterating chunks/entities in ECS query order and submitting on one thread.
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
        var r = _host.Renderer!;

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
        if (!TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, _host, renderer,
                out var baselineWorld))
        {
            cache.GlyphCount = 0;
            cache.PenAfter = 0f;
            return;
        }

        if (cache.GlyphCount > 0 && cache.CachedGlyphs is not null)
            renderer.SubmitSprites(cache.CachedGlyphs.AsSpan(0, cache.GlyphCount));

        if (!bt.Style.Underline && !bt.Style.Strikethrough)
            return;

        TextRenderer.SubmitTextDecorations(
            renderer,
            in bt.Style,
            baselineWorld,
            0f,
            cache.PenAfter,
            bt.SortKey,
            renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId);
    }
}
