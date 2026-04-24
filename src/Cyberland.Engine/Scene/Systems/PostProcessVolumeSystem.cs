using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Submits <see cref="PostProcessVolumeSource"/> rows to <see cref="IRenderer.SubmitPostProcessVolume"/>.
/// </summary>
public sealed class PostProcessVolumeSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PostProcessVolumeSource, Transform>();

    /// <summary>Creates the system.</summary>
    public PostProcessVolumeSystem(GameHostServices host) => _host = host;

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
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref readonly var row = ref chunk.Column<PostProcessVolumeSource>()[i];
                if (!row.Active)
                    return;

                ref readonly var tf = ref chunk.Column<Transform>()[i];
                r.SubmitPostProcessVolume(
                    in row.Volume,
                    tf.WorldPosition,
                    tf.WorldRotationRadians,
                    tf.WorldScale);
            });
        }
    }
}
