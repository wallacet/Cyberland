namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Declares which components must be present for chunk iteration for this scheduler entry.
/// Implemented by <see cref="ISystem"/> and <see cref="IParallelSystem"/>; the scheduler reads <see cref="QuerySpec"/> at registration.
/// </summary>
/// <remarks>
/// Default: <see cref="SystemQuerySpec.Empty"/> for systems that do not iterate ECS chunks (singleton setup, host-only work).
/// </remarks>
public interface IEcsQuerySource
{
    /// <summary>
    /// Unity-style <c>All</c> filter for <see cref="ChunkQueryAll"/> passed into phase callbacks.
    /// </summary>
    SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;
}
