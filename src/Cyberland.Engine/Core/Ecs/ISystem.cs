namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Single-threaded ECS entry: lifecycle plus optional <see cref="IEarlyUpdate"/>, <see cref="IFixedUpdate"/>, and/or <see cref="ILateUpdate"/>.
/// </summary>
public interface ISystem
{
    /// <summary>
    /// Called at most <strong>once</strong> per scheduler registration: on the first <see cref="Tasks.SystemScheduler.RunFrame(World, float)"/>
    /// where this entry exists and is <strong>enabled</strong>. Runs before any phase callback for that entry.
    /// </summary>
    void OnStart(World world) { }
}

/// <summary>Parallel ECS entry: lifecycle plus optional parallel early/fixed/late interfaces.</summary>
public interface IParallelSystem
{
    /// <inheritdoc cref="ISystem.OnStart"/>
    void OnStart(World world) { }
}

/// <summary>
/// Runs <strong>once</strong> per frame before fixed simulation, with real (variable) frame time.
/// Poll input here and write components that fixed simulation will read; reset held-action state at the start of this phase.
/// </summary>
public interface IEarlyUpdate
{
    /// <param name="world">Shared ECS world.</param>
    /// <param name="deltaSeconds">Elapsed wall time for this frame (variable).</param>
    void OnEarlyUpdate(World world, float deltaSeconds);
}

/// <summary>
/// Runs <strong>zero or more times</strong> per frame during the fixed timestep loop (multiple substeps when one render
/// frame spans more than one fixed tick). Do not clear whole input components after reading held state — later substeps would
/// see stale zeros. Consume edge triggers explicitly (see <see cref="Input.FrameEdgeLatch"/>).
/// </summary>
public interface IFixedUpdate
{
    /// <param name="world">Shared ECS world.</param>
    /// <param name="fixedDeltaSeconds">Scheduler <see cref="Tasks.SystemScheduler.FixedDeltaSeconds"/>.</param>
    void OnFixedUpdate(World world, float fixedDeltaSeconds);
}

/// <summary>
/// Runs once per frame after all fixed substeps, with real (variable) frame time.
/// For display smoothing, read <see cref="Hosting.GameHostServices.FixedAccumulatorSeconds"/> — the stock host sets it from the
/// fixed-step remainder <strong>before</strong> this phase runs.
/// </summary>
public interface ILateUpdate
{
    /// <param name="world">Shared ECS world.</param>
    /// <param name="deltaSeconds">Elapsed wall time for this frame (variable).</param>
    void OnLateUpdate(World world, float deltaSeconds);
}

/// <summary>Parallel: early variable phase.</summary>
public interface IParallelEarlyUpdate
{
    /// <param name="world">Shared ECS world.</param>
    /// <param name="deltaSeconds">Elapsed wall time for this frame (variable).</param>
    /// <param name="parallelOptions">Concurrency from the host.</param>
    void OnParallelEarlyUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions);
}

/// <summary>Parallel: fixed timestep phase (may run multiple substeps per frame — see <see cref="IFixedUpdate"/>).</summary>
public interface IParallelFixedUpdate
{
    /// <param name="world">Shared ECS world.</param>
    /// <param name="fixedDeltaSeconds">Scheduler fixed step size.</param>
    /// <param name="parallelOptions">Concurrency from the host.</param>
    void OnParallelFixedUpdate(World world, float fixedDeltaSeconds, ParallelOptions parallelOptions);
}

/// <summary>Parallel: late variable phase.</summary>
public interface IParallelLateUpdate
{
    /// <param name="world">Shared ECS world.</param>
    /// <param name="deltaSeconds">Elapsed wall time for this frame (variable).</param>
    /// <param name="parallelOptions">Concurrency from the host.</param>
    void OnParallelLateUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions);
}
