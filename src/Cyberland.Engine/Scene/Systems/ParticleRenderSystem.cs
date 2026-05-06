using System.Threading;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Diagnostics;
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
    private const int MaxSubmittedParticlesPerEmitter = 2048;

    private readonly GameHostServices _host;
    private int _particleBudgetWarningIssued;

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
        var defaultNormal = r.DefaultNormalTextureId;
        var camera = _host.CameraRuntimeState;
        
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
                var submitCount = Math.Min(em.RuntimeCount, MaxSubmittedParticlesPerEmitter);
                if (em.RuntimeCount > MaxSubmittedParticlesPerEmitter &&
                    Interlocked.Exchange(ref _particleBudgetWarningIssued, 1) == 0)
                {
                    EngineDiagnostics.Report(
                        EngineErrorSeverity.Warning,
                        "Cyberland.Engine.ParticleRenderSystem",
                        $"Particle emitter submissions exceeded budget ({MaxSubmittedParticlesPerEmitter}); keeping deterministic prefix of runtime particles.");
                }
                for (var p = 0; p < submitCount; p++)
                {
                    var center = new Vector2D<float>(
                        originX + em.RuntimePx[p],
                        originY + em.RuntimePy[p]);
                    if (IsOutsideActiveCamera(center, he, in camera))
                        continue;

                    var req = new SpriteDrawRequest
                    {
                        CenterWorld = center,
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

    private static bool IsOutsideActiveCamera(Vector2D<float> centerWorld, float halfExtent, in CameraRuntimeState camera)
    {
        if (!camera.Valid)
            return false;

        var viewport = new Vector2D<float>(camera.ViewportSizeWorld.X, camera.ViewportSizeWorld.Y);
        if (viewport.X <= 0f || viewport.Y <= 0f)
            return false;

        var centerVp = CameraProjection.WorldToViewportPixel(
            centerWorld,
            camera.PositionWorld,
            camera.RotationRadians,
            viewport);
        var radius = MathF.Sqrt(halfExtent * halfExtent * 2f);
        return centerVp.X + radius < 0f ||
               centerVp.Y + radius < 0f ||
               centerVp.X - radius > viewport.X ||
               centerVp.Y - radius > viewport.Y;
    }
}
