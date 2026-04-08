using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Runs ECS systems: sequential systems first (gameplay ordering), then parallel passes.
/// </summary>
public sealed class SystemScheduler
{
    private readonly ParallelismSettings _parallelism;
    private readonly List<ISystem> _sequential = new();
    private readonly List<IParallelSystem> _parallel = new();

    public SystemScheduler(ParallelismSettings parallelism) => _parallelism = parallelism;

    public void Register(ISystem system) => _sequential.Add(system);
    public void Register(IParallelSystem system) => _parallel.Add(system);

    public void RunFrame(World world, float deltaSeconds)
    {
        foreach (var s in _sequential)
            s.OnUpdate(world, deltaSeconds);

        var opts = _parallelism.CreateParallelOptions();
        foreach (var p in _parallel)
            p.OnParallelUpdate(world, opts);
    }
}
