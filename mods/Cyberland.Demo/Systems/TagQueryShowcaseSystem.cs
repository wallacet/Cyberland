using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Demo;

/// <summary>
/// Exercises a tag-only <see cref="IEcsQuerySource.QuerySpec"/> with <see cref="IParallelSystem"/> so the sample shows
/// chunk queries on non-player entities (see <see cref="PlayerTag"/> in <see cref="IntegrateSystem"/>).
/// </summary>
public sealed class TagQueryShowcaseSystem : IParallelSystem, IParallelLateUpdate
{
    /// <inheritdoc />
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<NeonStripTag>();

    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        // Neon strip is static; the query is here to demonstrate tag+parallel late wiring, not to mutate state.
        _ = query;
        _ = deltaSeconds;
        _ = parallelOptions;
    }
}
