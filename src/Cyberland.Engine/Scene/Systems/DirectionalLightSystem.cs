using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>Parallel late pass: submits each active <see cref="DirectionalLightSource"/> with its <see cref="Transform"/>.</summary>
[RunAfter("cyberland.engine/transform2d")]
public sealed class DirectionalLightSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<DirectionalLightSource, Transform>();

    /// <param name="host">Requires a non-null <see cref="GameHostServices.Renderer"/> at <see cref="ISystem.OnStart"/>.</param>
    public DirectionalLightSystem(GameHostServices host) => _host = host;

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
                ref readonly var s = ref chunk.Column<DirectionalLightSource>()[j];
                if (!s.Active)
                    return;
                ref readonly var t = ref chunk.Column<Transform>()[j];
                var dir = LightSceneMath.DirectionFromWorldRotation(t.WorldRotationRadians);
                var payload = new DirectionalLight
                {
                    DirectionWorld = dir,
                    Color = s.Color,
                    Intensity = s.Intensity,
                    CastsShadow = s.CastsShadow
                };
                r.SubmitDirectionalLight(in payload);
            });
        }
    }
}
