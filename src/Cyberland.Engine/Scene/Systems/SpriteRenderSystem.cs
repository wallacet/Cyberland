using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: walks all <see cref="Sprite"/> chunks, pairs with <see cref="Position"/> / <see cref="Rotation"/> / <see cref="Scale"/>, and submits <see cref="SpriteDrawRequest"/>s to <see cref="Hosting.GameHostServices.Renderer"/>.
/// </summary>
public sealed class SpriteRenderSystem : IParallelSystem
{
    private readonly GameHostServices _host;

    /// <param name="host">Must expose a non-null <see cref="Hosting.GameHostServices.Renderer"/> after startup.</param>
    public SpriteRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var chunks = new List<ComponentChunkView<Sprite>>();
        foreach (var chunk in world.QueryChunks<Sprite>())
            chunks.Add(chunk);

        if (chunks.Count == 0)
            return;

        Parallel.ForEach(chunks, parallelOptions, chunk =>
        {
            var ents = chunk.Entities;
            var sprites = chunk.Components;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var spr = ref sprites[i];
                if (!spr.Visible)
                    continue;

                var id = ents[i];
                if (!world.Components<Position>().TryGet(id, out var pos))
                    continue;

                var rot = world.Components<Rotation>().TryGet(id, out var r2) ? r2.Radians : 0f;
                var sc = world.Components<Scale>().TryGet(id, out var sc2) ? sc2 : Scale.One;
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
