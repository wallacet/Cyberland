namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Helpers for <see cref="ChunkQueryAll"/>: singleton entity resolution and conveniences for the first matching row.
/// </summary>
public static class ChunkQueryAllExtensions
{
    /// <summary>
    /// Returns a mutable reference to <typeparamref name="T"/> for the <strong>first entity</strong> matching <paramref name="query"/>
    /// (scheduler iteration order over non-empty chunks). Chunk boundaries are a storage detail—this targets one logical row for
    /// singletons or “any first match” workflows.
    /// </summary>
    /// <param name="query">Scheduler or <see cref="World.QueryChunks(SystemQuerySpec)"/> result.</param>
    /// <typeparam name="T">A component type included in the query spec.</typeparam>
    /// <exception cref="InvalidOperationException">No matching chunks or all chunks are empty.</exception>
    /// <exception cref="ArgumentException"><typeparamref name="T"/> is not part of this query (see <see cref="MultiComponentChunkView.Column{T}()"/>).</exception>
    public static ref T GetFirst<T>(this ChunkQueryAll query)
        where T : struct, IComponent
    {
        var e = query.GetEnumerator();
        if (!e.MoveNext())
        {
            throw new InvalidOperationException(
                $"Chunk query has no matching entities; cannot read first {typeof(T).Name}.");
        }

        return ref e.Current.Column<T>()[0];
    }

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
