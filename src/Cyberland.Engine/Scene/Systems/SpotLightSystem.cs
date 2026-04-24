using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>Parallel late pass: submits each active <see cref="SpotLightSource"/> with its <see cref="Transform"/>.</summary>
[RunAfter("cyberland.engine/transform2d")]
public sealed class SpotLightSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SpotLightSource, Transform>();

    /// <param name="host">Requires a non-null <see cref="GameHostServices.Renderer"/> at <see cref="ISystem.OnStart"/>.</param>
    public SpotLightSystem(GameHostServices host) => _host = host;

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
        var r = _host.Renderer!;
        
        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, j =>
            {
                ref readonly var s = ref chunk.Column<SpotLightSource>()[j];
                if (!s.Active)
                    return;
                ref readonly var t = ref chunk.Column<Transform>()[j];
                var dir = LightSceneMath.DirectionFromWorldRotation(t.WorldRotationRadians);
                var radiusScale = LightSceneMath.MaxAbsScale(t.WorldScale);
                var payload = new SpotLight
                {
                    PositionWorld = t.WorldPosition,
                    DirectionWorld = dir,
                    Radius = s.Radius * radiusScale,
                    InnerConeRadians = s.InnerConeRadians,
                    OuterConeRadians = s.OuterConeRadians,
                    Color = s.Color,
                    Intensity = s.Intensity,
                    CastsShadow = s.CastsShadow
                };
                r.SubmitSpotLight(in payload);
            });
        }
    }
}
