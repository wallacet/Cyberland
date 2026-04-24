namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Convenience <see cref="World"/> queries for singleton-style entity resolution.
/// </summary>
public static class WorldQueryExtensions
{
    /// <inheritdoc cref="ChunkQueryAllExtensions.RequireSingleEntityWith{TComponent}(ChunkQueryAll, string)"/>
    public static EntityId RequireSingleEntityWith<TComponent>(this World world, string label)
        where TComponent : struct, IComponent =>
        world.QueryChunks(SystemQuerySpec.All<TComponent>()).RequireSingleEntityWith<TComponent>(label);

    /// <inheritdoc cref="ChunkQueryAllExtensions.TryGetSingleEntityWith{TComponent}(ChunkQueryAll, out EntityId)"/>
    public static bool TryGetSingleEntityWith<TComponent>(this World world, out EntityId entityId)
        where TComponent : struct, IComponent =>
        world.QueryChunks(SystemQuerySpec.All<TComponent>()).TryGetSingleEntityWith<TComponent>(out entityId);
}
