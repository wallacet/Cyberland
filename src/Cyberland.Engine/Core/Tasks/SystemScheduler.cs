using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Runs ECS systems: sequential systems first (gameplay ordering), then parallel passes.
/// Each system is registered with a stable logical id so later mods can replace or unregister it.
/// </summary>
public sealed class SystemScheduler
{
    private readonly ParallelismSettings _parallelism;
    private readonly List<(string Id, ISystem System)> _sequential = new();
    private readonly List<(string Id, IParallelSystem System)> _parallel = new();
    private readonly Dictionary<string, int> _sequentialIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _parallelIndex = new(StringComparer.Ordinal);

    public SystemScheduler(ParallelismSettings parallelism) => _parallelism = parallelism;

    /// <summary>
    /// Registers or replaces a sequential system. Duplicate ids across sequential and parallel are not allowed.
    /// </summary>
    public void RegisterSequential(string logicalId, ISystem system)
    {
        ValidateLogicalId(logicalId);
        ArgumentNullException.ThrowIfNull(system);
        if (_parallelIndex.ContainsKey(logicalId))
        {
            throw new InvalidOperationException(
                $"Logical id '{logicalId}' is already registered as a parallel system.");
        }

        if (_sequentialIndex.TryGetValue(logicalId, out var idx))
        {
            _sequential[idx] = (logicalId, system);
            return;
        }

        _sequentialIndex[logicalId] = _sequential.Count;
        _sequential.Add((logicalId, system));
    }

    /// <summary>
    /// Registers or replaces a parallel system. Duplicate ids across sequential and parallel are not allowed.
    /// </summary>
    public void RegisterParallel(string logicalId, IParallelSystem system)
    {
        ValidateLogicalId(logicalId);
        ArgumentNullException.ThrowIfNull(system);
        if (_sequentialIndex.ContainsKey(logicalId))
        {
            throw new InvalidOperationException(
                $"Logical id '{logicalId}' is already registered as a sequential system.");
        }

        if (_parallelIndex.TryGetValue(logicalId, out var idx))
        {
            _parallel[idx] = (logicalId, system);
            return;
        }

        _parallelIndex[logicalId] = _parallel.Count;
        _parallel.Add((logicalId, system));
    }

    /// <summary>
    /// Removes a system by id from either the sequential or parallel list. Returns false if not found.
    /// </summary>
    public bool TryUnregister(string logicalId)
    {
        ValidateLogicalId(logicalId);

        if (_sequentialIndex.TryGetValue(logicalId, out var seqIdx))
        {
            _sequential.RemoveAt(seqIdx);
            RebuildSequentialIndex();
            return true;
        }

        if (_parallelIndex.TryGetValue(logicalId, out var parIdx))
        {
            _parallel.RemoveAt(parIdx);
            RebuildParallelIndex();
            return true;
        }

        return false;
    }

    public void RunFrame(World world, float deltaSeconds)
    {
        foreach (var (_, s) in _sequential)
            s.OnUpdate(world, deltaSeconds);

        var opts = _parallelism.CreateParallelOptions();
        foreach (var (_, p) in _parallel)
            p.OnParallelUpdate(world, opts);
    }

    private static void ValidateLogicalId(string logicalId)
    {
        if (string.IsNullOrWhiteSpace(logicalId))
            throw new ArgumentException("Logical id must be non-empty.", nameof(logicalId));
    }

    private void RebuildSequentialIndex()
    {
        _sequentialIndex.Clear();
        for (var i = 0; i < _sequential.Count; i++)
            _sequentialIndex[_sequential[i].Id] = i;
    }

    private void RebuildParallelIndex()
    {
        _parallelIndex.Clear();
        for (var i = 0; i < _parallel.Count; i++)
            _parallelIndex[_parallel[i].Id] = i;
    }
}
