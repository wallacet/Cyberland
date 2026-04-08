namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Single-threaded system: runs on the main thread in deterministic order.
/// </summary>
public interface ISystem
{
    void OnUpdate(World world, float deltaSeconds);
}

/// <summary>
/// Parallel-friendly system: implementors receive a <see cref="ParallelOptions"/> tuned by the engine
/// (e.g. many logical cores on high-end CPUs).
/// </summary>
public interface IParallelSystem
{
    void OnParallelUpdate(World world, ParallelOptions parallelOptions);
}
