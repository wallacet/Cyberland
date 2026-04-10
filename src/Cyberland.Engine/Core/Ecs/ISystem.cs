namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Single-threaded game/update pass: invoked from the host scheduler on the main thread in a stable order with other <see cref="ISystem"/> instances.
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

    /// <summary>Per-frame work after <see cref="OnStart"/> (if any). Runs once per frame while the system is enabled.</summary>
    /// <param name="world">Shared ECS world.</param>
    /// <param name="deltaSeconds">Frame time in seconds (not fixed timestep unless you implement it).</param>
    void OnUpdate(World world, float deltaSeconds);
}

/// <summary>
/// Optional parallel stage: runs in registration order like <see cref="ISystem"/>, but receives <see cref="ParallelOptions"/> so you can fan out work with <c>Parallel.ForEach</c> (e.g. over <see cref="World.QueryChunks{T}"/> chunks).
/// </summary>
/// <remarks>
/// Still respect thread-safety of services you touch (see remarks on <see cref="Rendering.IRenderer"/> for concurrent submits).
/// </remarks>
public interface IParallelSystem
{
    /// <inheritdoc cref="ISystem.OnStart"/>
    void OnStart(World world) { }

    /// <summary>Per-frame parallel work; use <paramref name="parallelOptions"/> for <c>Parallel.ForEach</c> and similar.</summary>
    /// <param name="world">Shared ECS world.</param>
    /// <param name="deltaSeconds">Elapsed time in seconds.</param>
    /// <param name="parallelOptions">Max degree of parallelism and cancellation from the host.</param>
    void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions);
}
