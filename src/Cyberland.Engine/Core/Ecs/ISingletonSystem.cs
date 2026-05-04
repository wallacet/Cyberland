namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// ECS entry for exactly one entity matching <see cref="IEcsQuerySource.QuerySpec"/> (singleton / marker-row pattern).
/// The scheduler resolves the entity once at startup and passes <see cref="SingletonEntity"/> into phase hooks instead of <see cref="ChunkQueryAll"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IEcsQuerySource.QuerySpec"/> must not be <see cref="SystemQuerySpec.Empty"/> and must match <strong>exactly one</strong>
/// entity when the scheduler first starts this entry; otherwise resolution throws (see <see cref="ChunkQueryAllExtensions.RequireSingleEntity"/>).
/// </para>
/// <para>
/// Ordering uses the same registration-order rules and optional ordering attributes as <see cref="ISystem"/> / <see cref="IParallelSystem"/>.
/// </para>
/// </remarks>
public interface ISingletonSystem : IEcsQuerySource
{
    /// <summary>Human-readable label for errors when zero or multiple entities match <see cref="IEcsQuerySource.QuerySpec"/>.</summary>
    string SingletonLabel => GetType().Name;

    /// <summary>
    /// Called once before any phase hook, after the singleton entity has been resolved.
    /// </summary>
    /// <param name="singleton">Resolved row; use <see cref="SingletonEntity.Get{T}"/> or cache <see cref="SingletonEntity.Entity"/>.</param>
    void OnSingletonStart(in SingletonEntity singleton) { }
}

/// <summary>Variable frame time phase for <see cref="ISingletonSystem"/>.</summary>
public interface ISingletonEarlyUpdate
{
    /// <param name="singleton">The singleton row for this registration.</param>
    /// <param name="deltaSeconds">Wall time for this frame.</param>
    void OnSingletonEarlyUpdate(in SingletonEntity singleton, float deltaSeconds);
}

/// <summary>Fixed timestep phase for <see cref="ISingletonSystem"/>.</summary>
public interface ISingletonFixedUpdate
{
    /// <param name="singleton">The singleton row for this registration.</param>
    /// <param name="fixedDeltaSeconds"><see cref="Tasks.SystemScheduler.FixedDeltaSeconds"/>.</param>
    void OnSingletonFixedUpdate(in SingletonEntity singleton, float fixedDeltaSeconds);
}

/// <summary>Post-fixed variable frame time phase for <see cref="ISingletonSystem"/>.</summary>
public interface ISingletonLateUpdate
{
    /// <param name="singleton">The singleton row for this registration.</param>
    /// <param name="deltaSeconds">Wall time for this frame.</param>
    void OnSingletonLateUpdate(in SingletonEntity singleton, float deltaSeconds);
}
