namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Helpers for singleton-style queries: exactly one entity matching a chunk query.
/// </summary>
public static class ChunkQueryAllExtensions
{
    /// <summary>
    /// Returns the sole entity id across all chunks in <paramref name="query"/>, which must be built from a
    /// <see cref="SystemQuerySpec"/> that includes <typeparamref name="TComponent"/> so every row carries that component.
    /// </summary>
    /// <param name="query">Scheduler or <see cref="World.QueryChunks(SystemQuerySpec)"/> result.</param>
    /// <param name="label">Human-readable name for error messages (e.g. "HUD title").</param>
    /// <typeparam name="TComponent">One of the components in the query spec (used only for error text and compile-time clarity).</typeparam>
    /// <exception cref="InvalidOperationException">Zero matching entities or more than one.</exception>
    public static EntityId RequireSingleEntityWith<TComponent>(this ChunkQueryAll query, string label)
        where TComponent : struct, IComponent
    {
        EntityId? found = null;
        var typeName = typeof(TComponent).Name;
        foreach (var chunk in query)
        {
            foreach (var entity in chunk.Entities)
            {
                if (found.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Expected exactly one {label} entity with component {typeName}.");
                }

                found = entity;
            }
        }

        return found ?? throw new InvalidOperationException($"Missing {label} entity with component {typeName}.");
    }

    /// <summary>
    /// Tries to return the single entity in <paramref name="query"/>. Returns false if there are zero entities or more than one.
    /// </summary>
    /// <param name="query">Scheduler or <see cref="World.QueryChunks(SystemQuerySpec)"/> result.</param>
    /// <param name="entityId">Set when the method returns true.</param>
    /// <typeparam name="TComponent">One of the components in the query spec.</typeparam>
    /// <returns>True if exactly one entity exists in the query.</returns>
    public static bool TryGetSingleEntityWith<TComponent>(this ChunkQueryAll query, out EntityId entityId)
        where TComponent : struct, IComponent
    {
        entityId = default;
        EntityId? found = null;
        foreach (var chunk in query)
        {
            foreach (var entity in chunk.Entities)
            {
                if (found.HasValue)
                {
                    entityId = default;
                    return false;
                }

                found = entity;
            }
        }

        if (!found.HasValue)
            return false;

        entityId = found.Value;
        return true;
    }
}
