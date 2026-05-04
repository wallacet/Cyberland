namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Declares which components must be present for chunk iteration for this scheduler entry.
/// Implemented by <see cref="ISystem"/>, <see cref="IParallelSystem"/>, and <see cref="ISingletonSystem"/>; the scheduler reads
/// <see cref="QuerySpec"/> at registration.
/// </summary>
/// <remarks>
/// Default: <see cref="SystemQuerySpec.Empty"/> for systems that do not iterate ECS chunks (host-only work).
/// <see cref="ISingletonSystem"/> must use a non-empty spec that resolves to exactly one entity.
/// </remarks>
public interface IEcsQuerySource
{
    /// <summary>
    /// Unity-style <c>All</c> filter for <see cref="ChunkQueryAll"/> passed into phase callbacks.
    /// </summary>
    SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;
}
