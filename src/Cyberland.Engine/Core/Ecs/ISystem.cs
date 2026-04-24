namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Single-threaded ECS entry: lifecycle plus optional <see cref="IEarlyUpdate"/>, <see cref="IFixedUpdate"/>, and/or <see cref="ILateUpdate"/>.
/// Declare chunk requirements via <see cref="IEcsQuerySource.QuerySpec"/>; the scheduler passes matching <see cref="ChunkQueryAll"/> views.
/// </summary>
public interface ISystem : IEcsQuerySource
{
    /// <summary>
    /// Called at most <strong>once</strong> per scheduler registration: on the first <see cref="Tasks.SystemScheduler.RunFrame(World, float)"/>
    /// where this entry exists and is <strong>enabled</strong>. Runs before any phase callback for that entry.
    /// </summary>
    /// <param name="world">Shared ECS world. Not passed to phase hooks; use this if you need <see cref="World"/> access outside chunk iteration.</param>
    /// <param name="query">Pre-queried chunks for this system’s <see cref="IEcsQuerySource.QuerySpec"/>.</param>
    /// <remarks>
    /// <para>
    /// Phase callbacks receive only <see cref="ChunkQueryAll"/> so hot paths default to data-oriented chunk iteration.
    /// If you must call <see cref="World"/> APIs (e.g. ad-hoc component lookups not covered by <see cref="SystemQuerySpec"/>), assign
    /// <paramref name="world"/> to a field here and use it in <see cref="IEarlyUpdate"/> / <see cref="IFixedUpdate"/> / <see cref="ILateUpdate"/>
    /// only when there is no reasonable chunk-based alternative. That pattern is a last resort, not the default.
    /// </para>
    /// <para>
    /// For <see cref="IParallelSystem"/>, the same applies: <see cref="IParallelEarlyUpdate"/> and other parallel phase interfaces do not receive
    /// <see cref="World"/>. Caching the world and using it from parallel callbacks is only valid when the work respects ECS threading
    /// rules (structural changes are not parallel with chunk writes in this engine; treat the world with the same care as you would
    /// for any shared mutable state).
    /// </para>
    /// </remarks>
    void OnStart(World world, ChunkQueryAll query) { }
}

/// <summary>Parallel ECS entry: lifecycle plus optional parallel early/fixed/late interfaces.</summary>
public interface IParallelSystem : IEcsQuerySource
{
    /// <inheritdoc cref="ISystem.OnStart"/>
    void OnStart(World world, ChunkQueryAll query) { }
}

/// <summary>
/// Runs <strong>once</strong> per frame before fixed simulation, with real (variable) frame time.
/// Poll input here and write components that fixed simulation will read; reset held-action state at the start of this phase.
/// </summary>
public interface IEarlyUpdate
{
    /// <param name="query">Pre-queried chunks for this system’s <see cref="IEcsQuerySource.QuerySpec"/>. Use this for SoA iteration.</param>
    /// <param name="deltaSeconds">Elapsed wall time for this frame (variable).</param>
    /// <remarks>Chunk iteration and timing only — <see cref="World"/> is not passed. See <see cref="ISystem.OnStart"/>.</remarks>
    void OnEarlyUpdate(ChunkQueryAll query, float deltaSeconds);
}

/// <summary>
/// Runs <strong>zero or more times</strong> per frame during the fixed timestep loop (multiple substeps when one render
/// frame spans more than one fixed tick). Do not clear whole input components after reading held state — later substeps would
/// see stale zeros. Consume edge triggers explicitly (see <see cref="Input.FrameEdgeLatch"/>).
/// </summary>
public interface IFixedUpdate
{
    /// <param name="query">Pre-queried chunks for this system’s <see cref="IEcsQuerySource.QuerySpec"/>. Use this for SoA iteration.</param>
    /// <param name="fixedDeltaSeconds">Scheduler <see cref="Tasks.SystemScheduler.FixedDeltaSeconds"/>.</param>
    /// <remarks>Chunk iteration and timing only — <see cref="World"/> is not passed. See <see cref="ISystem.OnStart"/>.</remarks>
    void OnFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds);
}

/// <summary>
/// Runs once per frame after all fixed substeps, with real (variable) frame time.
/// For display smoothing, read <see cref="Hosting.GameHostServices.FixedAccumulatorSeconds"/> — the stock host sets it from the
/// fixed-step remainder <strong>before</strong> this phase runs.
/// </summary>
public interface ILateUpdate
{
    /// <param name="query">Pre-queried chunks for this system’s <see cref="IEcsQuerySource.QuerySpec"/>. Use this for SoA iteration.</param>
    /// <param name="deltaSeconds">Elapsed wall time for this frame (variable).</param>
    /// <remarks>Chunk iteration and timing only — <see cref="World"/> is not passed. See <see cref="ISystem.OnStart"/>.</remarks>
    void OnLateUpdate(ChunkQueryAll query, float deltaSeconds);
}

/// <summary>Parallel: early variable phase.</summary>
public interface IParallelEarlyUpdate
{
    /// <param name="query">Pre-queried chunks for this system’s <see cref="IEcsQuerySource.QuerySpec"/>. Use this for SoA iteration.</param>
    /// <param name="deltaSeconds">Elapsed wall time for this frame (variable).</param>
    /// <param name="parallelOptions">Concurrency from the host.</param>
    /// <remarks>Chunk iteration and timing only — <see cref="World"/> is not passed. See <see cref="ISystem.OnStart"/>.</remarks>
    void OnParallelEarlyUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions);
}

/// <summary>Parallel: fixed timestep phase (may run multiple substeps per frame — see <see cref="IFixedUpdate"/>).</summary>
public interface IParallelFixedUpdate
{
    /// <param name="query">Pre-queried chunks for this system’s <see cref="IEcsQuerySource.QuerySpec"/>. Use this for SoA iteration.</param>
    /// <param name="fixedDeltaSeconds">Scheduler fixed step size.</param>
    /// <param name="parallelOptions">Concurrency from the host.</param>
    /// <remarks>Chunk iteration and timing only — <see cref="World"/> is not passed. See <see cref="ISystem.OnStart"/>.</remarks>
    void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions);
}

/// <summary>Parallel: late variable phase.</summary>
public interface IParallelLateUpdate
{
    /// <param name="query">Pre-queried chunks for this system’s <see cref="IEcsQuerySource.QuerySpec"/>. Use this for SoA iteration.</param>
    /// <param name="deltaSeconds">Elapsed wall time for this frame (variable).</param>
    /// <param name="parallelOptions">Concurrency from the host.</param>
    /// <remarks>Chunk iteration and timing only — <see cref="World"/> is not passed. See <see cref="ISystem.OnStart"/>.</remarks>
    void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions);
}
