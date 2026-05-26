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
        var r = _host.Renderer;
        foreach (var chunk in query)
        {
            if (chunk.Count <= DeferredRenderingConstants.LightSystemParallelThreshold)
            {
                for (int j = 0; j < chunk.Count; j++)
                    SubmitLight(r, chunk, j);
            }
            else
            {
                Parallel.For(0, chunk.Count, parallelOptions, j =>
                    SubmitLight(r, chunk, j));
            }
        }
    }

    private static void SubmitLight(IRenderer r, MultiComponentChunkView chunk, int j)
    {
        ref readonly var s = ref chunk.Column<DirectionalLightSource>()[j];
        if (!s.Active)
            return;
        if (s.Intensity <= 0f || (s.Color.X <= 0f && s.Color.Y <= 0f && s.Color.Z <= 0f))
            return;
        ref readonly var t = ref chunk.Column<Transform>()[j];
        TransformMath.DecomposeToPRS(t.WorldMatrix, out _, out var worldRad, out _);
        var dir = LightSceneMath.DirectionFromWorldRotation(worldRad);
        var payload = new DirectionalLight
        {
            DirectionWorld = dir,
            Color = s.Color,
            Intensity = s.Intensity,
            CastsShadow = s.CastsShadow
        };
        r.SubmitDirectionalLight(in payload);
    }
}
