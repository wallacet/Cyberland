using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Default engine sprite pass: walks <see cref="Sprite"/> chunks with <see cref="Transform"/> and
/// submits <see cref="SpriteDrawRequest"/>s. Mods normally attach components and let this system draw—no custom <see cref="IRenderer.SubmitSprite"/> calls.
/// </summary>
public sealed class SpriteRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly List<MultiComponentChunkView> _chunks = new();
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Sprite, Transform>();

    /// <param name="host">Must expose a non-null <see cref="Hosting.GameHostServices.Renderer"/> after startup.</param>
    public SpriteRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelLateUpdate(World world, ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        _chunks.Clear();
        foreach (var chunk in query)
            _chunks.Add(chunk);

        if (_chunks.Count == 0)
            return;

        var sprites = world.Components<Sprite>();
        var transforms = world.Components<Transform>();

        Parallel.For(0, _chunks.Count, parallelOptions, idx =>
        {
            var chunk = _chunks[idx];
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                ref readonly var spr = ref sprites.Get(entity);
                if (!spr.Visible)
                    continue;

                ref readonly var transform = ref transforms.Get(entity);
                var hx = spr.HalfExtents.X * transform.WorldScale.X;
                var hy = spr.HalfExtents.Y * transform.WorldScale.Y;

                var req = new SpriteDrawRequest
                {
                    CenterWorld = transform.WorldPosition,
                    HalfExtentsWorld = new Vector2D<float>(hx, hy),
                    RotationRadians = transform.WorldRotationRadians,
                    Layer = spr.Layer,
                    SortKey = spr.SortKey,
                    AlbedoTextureId = spr.AlbedoTextureId,
                    NormalTextureId = spr.NormalTextureId,
                    EmissiveTextureId = spr.EmissiveTextureId,
                    ColorMultiply = spr.ColorMultiply,
                    Alpha = spr.Alpha,
                    EmissiveTint = spr.EmissiveTint,
                    EmissiveIntensity = spr.EmissiveIntensity,
                    DepthHint = spr.DepthHint,
                    UvRect = spr.UvRect,
                    Transparent = spr.Transparent
                };

                r.SubmitSprite(in req);
            }
        });
    }
}
