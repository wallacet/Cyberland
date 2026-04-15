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
/// <remarks>
/// Uses <see cref="GameHostServices.ParticleEmitterIdsForFrame"/> filled by <see cref="ParticleSimulationSystem"/> in the same frame.
/// Keep simulation registered and ordered <strong>before</strong> render (stock <see cref="GameApplication"/> order); disabling simulation while leaving render enabled can leave stale emitter ids.
/// </remarks>
public sealed class ParticleRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private static readonly Vector4D<float> WhiteColor = new(1f, 1f, 1f, 1f);
    private static readonly Vector3D<float> WhiteEmissive = new(1f, 1f, 1f);

    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ParticleEmitter>();

    /// <param name="host">Requires <see cref="Hosting.GameHostServices.Renderer"/> and <see cref="Hosting.GameHostServices.Particles"/>.</param>
    public ParticleRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelLateUpdate(World world, ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        var store = _host.Particles;
        if (r is null || store is null)
            return;

        var ids = _host.ParticleEmitterIdsForFrame;
        if (ids.Count == 0)
            return;

        var emitters = world.Components<ParticleEmitter>();
        var positions = world.Components<Position>();
        var defaultNormal = r.DefaultNormalTextureId;

        Parallel.For(0, ids.Count, parallelOptions, i =>
        {
            var id = ids[i];
            ref readonly var em = ref emitters.Get(id);
            if (!positions.TryGet(id, out var pos))
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
                    NormalTextureId = defaultNormal,
                    EmissiveTextureId = -1,
                    ColorMultiply = WhiteColor,
                    Alpha = 1f,
                    EmissiveTint = WhiteEmissive,
                    EmissiveIntensity = 0.6f,
                    DepthHint = em.SortKey,
                    UvRect = default
                };
                r.SubmitSprite(in req);
            }
        });
    }
}
