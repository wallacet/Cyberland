using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: submits billboard sprites for live particles stored in <see cref="ParticleEmitter"/> runtime SoA arrays
/// (pairs with <see cref="ParticleSimulationSystem"/>).
/// </summary>
/// <remarks>
/// Keep simulation registered and ordered <strong>before</strong> render (stock <see cref="GameApplication"/> order)
/// so each frame renders freshly integrated emitter runtime arrays.
/// </remarks>
public sealed class ParticleRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private static readonly Vector4D<float> WhiteColor = new(1f, 1f, 1f, 1f);
    private static readonly Vector3D<float> WhiteEmissive = new(1f, 1f, 1f);

    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ParticleEmitter, Transform>();

    /// <param name="host">Requires <see cref="Hosting.GameHostServices.Renderer"/>.</param>
    public ParticleRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;
        var defaultNormal = r.DefaultNormalTextureId;
        
        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref readonly var em = ref chunk.Column<ParticleEmitter>()[i];
                ref readonly var transform = ref chunk.Column<Transform>()[i];
                if (em.RuntimeCount == 0)
                    return;

                // Translation lives in the world matrix row directly; pull it once so the per-particle loop doesn't
                // defensive-copy + decompose on every sample.
                var originX = transform.WorldMatrix.M31;
                var originY = transform.WorldMatrix.M32;
                var he = em.HalfExtent;
                for (var p = 0; p < em.RuntimeCount; p++)
                {
                    var req = new SpriteDrawRequest
                    {
                        CenterWorld = new Vector2D<float>(
                            originX + em.RuntimePx[p],
                            originY + em.RuntimePy[p]),
                        HalfExtentsWorld = new Vector2D<float>(he, he),
                        RotationRadians = 0f,
                        Layer = em.Layer,
                        SortKey = em.SortKey + p * 0.001f,
                        AlbedoTextureId = em.AlbedoTextureId,
                        NormalTextureId = defaultNormal,
                        EmissiveTextureId = TextureId.MaxValue,
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
}
