using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: submits billboard sprites for live particles stored in <see cref="ParticleStore"/> (pairs with <see cref="ParticleSimulationSystem"/>).
/// </summary>
public sealed class ParticleRenderSystem : IParallelSystem
{
    private readonly GameHostServices _host;

    /// <param name="host">Requires <see cref="Hosting.GameHostServices.Renderer"/> and <see cref="Hosting.GameHostServices.Particles"/>.</param>
    public ParticleRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        var store = _host.Particles;
        if (r is null || store is null)
            return;

        var ids = new List<EntityId>();
        foreach (var view in world.QueryChunks<ParticleEmitter>())
        {
            var ents = view.Entities;
            for (var i = 0; i < view.Count; i++)
                ids.Add(ents[i]);
        }

        if (ids.Count == 0)
            return;

        Parallel.ForEach(ids, parallelOptions, id =>
        {
            ref readonly var em = ref world.Components<ParticleEmitter>().Get(id);
            if (!world.Components<Position>().TryGet(id, out var pos))
                return;
            if (!store.TryGetBucket(id, out var b) || b is null || b.Count == 0)
                return;

            var he = em.HalfExtent;
            for (var p = 0; p < b.Count; p++)
            {
                var req = new SpriteDrawRequest
                {
                    CenterWorld = new Vector2D<float>(pos.X + b.Px[p], pos.Y + b.Py[p]),
                    HalfExtentsWorld = new Vector2D<float>(he, he),
                    RotationRadians = 0f,
                    Layer = em.Layer,
                    SortKey = em.SortKey + p * 0.001f,
                    AlbedoTextureId = em.AlbedoTextureId,
                    NormalTextureId = r.DefaultNormalTextureId,
                    EmissiveTextureId = -1,
                    ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f),
                    Alpha = 1f,
                    EmissiveTint = new Vector3D<float>(1f, 1f, 1f),
                    EmissiveIntensity = 0.6f,
                    DepthHint = em.SortKey,
                    UvRect = default
                };
                r.SubmitSprite(in req);
            }
        });
    }
}
