using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>Parallel late pass: submits each active <see cref="PointLightSource"/> with its <see cref="Transform"/>.</summary>
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
            Parallel.For(0, chunk.Count, parallelOptions, j =>
            {
                ref readonly var s = ref chunk.Column<PointLightSource>()[j];
                if (!s.Active)
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
            });
        }
    }
}
