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
        var r = _host.Renderer;
        if (r is null)
            return;

        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref readonly var row = ref chunk.Column<PostProcessVolumeSource>()[i];
                if (!row.Active)
                    return;

                ref readonly var tf = ref chunk.Column<Transform>()[i];
                // Single decomposition feeds all three submit arguments; property access through a ref readonly would
                // decompose three times.
                TransformMath.DecomposeToPRS(tf.WorldMatrix, out var worldPos, out var worldRad, out var worldScale);
                r.SubmitPostProcessVolume(
                    in row.Volume,
                    worldPos,
                    worldRad,
                    worldScale);
            });
        }
    }
}
