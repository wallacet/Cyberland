using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Default engine sprite pass: walks <see cref="Sprite"/> chunks with <see cref="Position"/> / <see cref="Rotation"/> / <see cref="Scale"/> and
/// submits <see cref="SpriteDrawRequest"/>s. Mods normally attach components and let this system draw—no custom <see cref="IRenderer.SubmitSprite"/> calls.
/// </summary>
public sealed class SpriteRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly List<MultiComponentChunkView> _chunks = new();
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Sprite>();

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

        var positions = world.Components<Position>();
        var rotations = world.Components<Rotation>();
        var scales = world.Components<Scale>();

        Parallel.For(0, _chunks.Count, parallelOptions, idx =>
        {
            var chunk = _chunks[idx];
            var ents = chunk.Entities;
            var sprites = chunk.Column<Sprite>(0);
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var spr = ref sprites[i];
                if (!spr.Visible)
                    continue;

                var id = ents[i];
                if (!positions.TryGet(id, out var pos))
                    continue;

                var rot = rotations.TryGet(id, out var r2) ? r2.Radians : 0f;
                var sc = scales.TryGet(id, out var sc2) ? sc2 : Scale.One;
                var hx = spr.HalfExtents.X * sc.X;
                var hy = spr.HalfExtents.Y * sc.Y;

                var req = new SpriteDrawRequest
                {
                    CenterWorld = pos.AsVector(),
                    HalfExtentsWorld = new Vector2D<float>(hx, hy),
                    RotationRadians = rot,
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
