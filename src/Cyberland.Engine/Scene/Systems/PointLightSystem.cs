using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>Parallel late pass: submits each active <see cref="PointLightSource"/> with its <see cref="Transform"/>.</summary>
/// <remarks>
/// Caches <see cref="World"/> in <see cref="ISystem.OnStart"/> and reads it from parallel workers
/// during the late phase. This is safe because the late phase forbids structural mutation and all component reads are
/// read-only. Do not invoke <see cref="LightSceneMath.ResolveLightPositionWorldForSubmit"/> from phases that mutate
/// the world.
/// </remarks>
[RunAfter("cyberland.engine/transform2d")]
public sealed class PointLightSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;
    private World _world = null!;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PointLightSource, Transform>();

    /// <param name="host">Requires a non-null <see cref="GameHostServices.Renderer"/> at <see cref="ISystem.OnStart"/>.</param>
    public PointLightSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        var world = _world;
        var cam = _host.CameraRuntimeState;
        foreach (var chunk in query)
        {
            if (chunk.Count <= DeferredRenderingConstants.LightSystemParallelThreshold)
            {
                for (int j = 0; j < chunk.Count; j++)
                    SubmitLight(r, world, chunk, j, in cam);
            }
            else
            {
                Parallel.For(0, chunk.Count, parallelOptions, j =>
                    SubmitLight(r, world, chunk, j, in cam));
            }
        }
    }

    private static void SubmitLight(
        IRenderer r, World world, MultiComponentChunkView chunk, int j, in CameraRuntimeState cam)
    {
        ref readonly var s = ref chunk.Column<PointLightSource>()[j];
        if (!s.Active)
            return;
        if (s.Intensity <= 0f || (s.Color.X <= 0f && s.Color.Y <= 0f && s.Color.Z <= 0f))
            return;
        ref readonly var t = ref chunk.Column<Transform>()[j];
        TransformMath.DecomposeToPRS(t.WorldMatrix, out var worldPos, out _, out var worldScale);
        var radiusScale = LightSceneMath.MaxAbsScale(worldScale);
        var positionWorld = LightSceneMath.ResolveLightPositionWorldForSubmit(
            world, chunk.Entities[j], worldPos, in cam);
        var payload = new PointLight
        {
            PositionWorld = positionWorld,
            Radius = s.Radius * radiusScale,
            Color = s.Color,
            Intensity = s.Intensity,
            FalloffExponent = s.FalloffExponent,
            CastsShadow = s.CastsShadow
        };
        r.SubmitPointLight(in payload);
    }
}
