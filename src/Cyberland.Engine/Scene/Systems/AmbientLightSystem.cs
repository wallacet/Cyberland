using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel late pass: submits <see cref="AmbientLightSource"/> rows as <see cref="AmbientLight"/>; the renderer sums them
/// for the deferred ambient term.
/// </summary>
[RunAfter("cyberland.engine/transform2d")]
public sealed class AmbientLightSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<AmbientLightSource>();

    /// <summary>Creates the system.</summary>
    public AmbientLightSystem(GameHostServices host) => _host = host;

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
                ref readonly var row = ref chunk.Column<AmbientLightSource>()[j];
                if (!row.Active)
                    return;
                var payload = new AmbientLight { Color = row.Color, Intensity = row.Intensity };
                r.SubmitAmbientLight(in payload);
            });
        }
    }
}
