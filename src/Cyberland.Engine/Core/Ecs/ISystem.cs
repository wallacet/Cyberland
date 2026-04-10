namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Single-threaded system: runs on the main thread in deterministic order.
/// </summary>
public interface ISystem
{
    /// <summary>
    /// Called at most <strong>once</strong> per scheduler registration: on the first <see cref="Tasks.SystemScheduler.RunFrame"/>
    /// where this entry exists and is <strong>enabled</strong>, on the same thread as <see cref="OnUpdate"/>.
    /// Runs <strong>before</strong> the first <see cref="OnUpdate"/> for that entry that frame. If multiple systems
    /// start the same frame, <see cref="OnStart"/> runs in registration order. Disabling and re-enabling the entry
    /// does not invoke <see cref="OnStart"/> again; replacing the registration or unregistering and registering again does.
    /// </summary>
    void OnStart(World world) { }

    void OnUpdate(World world, float deltaSeconds);
}

/// <summary>
/// Parallel-friendly system: implementors receive <see cref="ParallelOptions"/> from the engine.
/// Execution order is the same as registration order in <see cref="Tasks.SystemScheduler"/>; use
/// <see cref="Parallel.ForEach"/> (or similar) inside <see cref="OnParallelUpdate"/> for multi-core work.
/// </summary>
public interface IParallelSystem
{
    /// <inheritdoc cref="ISystem.OnStart"/>
    void OnStart(World world) { }

    void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions);
}
