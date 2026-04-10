using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Runs ECS systems in strict <strong>registration order</strong>. Each entry is either an <see cref="ISystem"/>
/// or an <see cref="IParallelSystem"/>; parallel entries receive <see cref="ParallelOptions"/> from
/// <see cref="ParallelismSettings"/>.
/// </summary>
public sealed class SystemScheduler
{
    private abstract class Entry
    {
        public required string Id { get; init; }
        public bool Enabled;
        public bool Started;
    }

    private sealed class SequentialEntry : Entry
    {
        public required ISystem System { get; init; }
    }

    private sealed class ParallelEntry : Entry
    {
        public required IParallelSystem System { get; init; }
    }

    private readonly ParallelismSettings _parallelism;
    private readonly List<Entry> _entries = new();
    private readonly Dictionary<string, int> _logicalIds = new(StringComparer.Ordinal);

    /// <summary>Creates a scheduler that supplies <paramref name="parallelism"/> to parallel ECS systems.</summary>
    /// <param name="parallelism">Concurrency limits for <see cref="IParallelSystem.OnParallelUpdate"/>.</param>
    public SystemScheduler(ParallelismSettings parallelism) => _parallelism = parallelism;

    /// <summary>Raised after <see cref="ISystem.OnStart"/> / <see cref="IParallelSystem.OnStart"/> returns for an entry.</summary>
    public event Action<string>? SystemStarted;

    /// <summary>Raised when <see cref="SetEnabled"/> transitions an entry from disabled to enabled (not on initial <c>Register*</c>).</summary>
    public event Action<string>? SystemEnabled;

    /// <summary>Raised when <see cref="SetEnabled"/> transitions an entry from enabled to disabled.</summary>
    public event Action<string>? SystemDisabled;

    /// <summary>Raised after <see cref="TryUnregister"/> removes an entry.</summary>
    public event Action<string>? SystemUnregistered;

    /// <summary>Registers or replaces a sequential system. Execution order follows registration order.</summary>
    /// <param name="logicalId">Stable id used for enable/disable and diagnostics.</param>
    /// <param name="system">Sequential ECS system instance.</param>
    /// <param name="enabled">Initial enabled flag. Replacing an id resets lifecycle state so the new instance receives <see cref="ISystem.OnStart"/> once.</param>
    public void RegisterSequential(string logicalId, ISystem system, bool enabled = true)
    {
        ValidateLogicalId(logicalId);
        ArgumentNullException.ThrowIfNull(system);
        Upsert(logicalId, new SequentialEntry { Id = logicalId, System = system, Enabled = enabled, Started = false });
    }

    /// <summary>Registers or replaces a parallel system. Execution order follows registration order.</summary>
    /// <param name="logicalId">Stable id used for enable/disable and diagnostics.</param>
    /// <param name="system">Parallel ECS system instance.</param>
    /// <param name="enabled">Initial enabled flag. Replacing an id resets lifecycle state so the new instance receives <see cref="IParallelSystem.OnStart"/> once.</param>
    public void RegisterParallel(string logicalId, IParallelSystem system, bool enabled = true)
    {
        ValidateLogicalId(logicalId);
        ArgumentNullException.ThrowIfNull(system);
        Upsert(logicalId, new ParallelEntry { Id = logicalId, System = system, Enabled = enabled, Started = false });
    }

    /// <summary>Sets whether a registered system runs. Returns false if <paramref name="logicalId"/> is not found.</summary>
    public bool SetEnabled(string logicalId, bool enabled)
    {
        ValidateLogicalId(logicalId);

        if (!_logicalIds.TryGetValue(logicalId, out var idx))
            return false;

        var e = _entries[idx];
        if (e.Enabled == enabled)
            return true;

        e.Enabled = enabled;
        if (enabled)
            SystemEnabled?.Invoke(logicalId);
        else
            SystemDisabled?.Invoke(logicalId);
        return true;
    }

    /// <summary>Returns whether the entry is enabled, or false if <paramref name="logicalId"/> is not registered.</summary>
    public bool IsEnabled(string logicalId)
    {
        ValidateLogicalId(logicalId);
        return _logicalIds.TryGetValue(logicalId, out var idx) && _entries[idx].Enabled;
    }

    /// <summary>Returns whether <paramref name="logicalId"/> exists; if so, sets <paramref name="enabled"/>.</summary>
    public bool TryGetEnabled(string logicalId, out bool enabled)
    {
        ValidateLogicalId(logicalId);
        if (!_logicalIds.TryGetValue(logicalId, out var idx))
        {
            enabled = false;
            return false;
        }

        enabled = _entries[idx].Enabled;
        return true;
    }

    /// <summary>Removes a system by id. Returns false if not found.</summary>
    public bool TryUnregister(string logicalId)
    {
        ValidateLogicalId(logicalId);

        if (!_logicalIds.TryGetValue(logicalId, out var idx))
            return false;

        _entries.RemoveAt(idx);
        RebuildIdMap();
        SystemUnregistered?.Invoke(logicalId);
        return true;
    }

    /// <summary>
    /// Executes one frame: every <strong>enabled</strong> system in registration order, calling <see cref="ISystem.OnUpdate"/> or <see cref="IParallelSystem.OnParallelUpdate"/>.
    /// </summary>
    /// <param name="world">ECS world passed to each system.</param>
    /// <param name="deltaSeconds">Elapsed time since last frame.</param>
    public void RunFrame(World world, float deltaSeconds)
    {
        var opts = _parallelism.CreateParallelOptions();

        foreach (var e in _entries)
        {
            if (!e.Enabled)
                continue;

            switch (e)
            {
                case SequentialEntry se:
                    if (!se.Started)
                    {
                        se.System.OnStart(world);
                        se.Started = true;
                        SystemStarted?.Invoke(se.Id);
                    }

                    se.System.OnUpdate(world, deltaSeconds);
                    break;
                case ParallelEntry pe:
                    if (!pe.Started)
                    {
                        pe.System.OnStart(world);
                        pe.Started = true;
                        SystemStarted?.Invoke(pe.Id);
                    }

                    pe.System.OnParallelUpdate(world, deltaSeconds, opts);
                    break;
            }
        }
    }

    private void Upsert(string logicalId, Entry entry)
    {
        if (_logicalIds.TryGetValue(logicalId, out var idx))
        {
            _entries[idx] = entry;
            return;
        }

        idx = _entries.Count;
        _entries.Add(entry);
        _logicalIds[logicalId] = idx;
    }

    private void RebuildIdMap()
    {
        _logicalIds.Clear();
        for (var i = 0; i < _entries.Count; i++)
            _logicalIds[_entries[i].Id] = i;
    }

    private static void ValidateLogicalId(string logicalId)
    {
        if (string.IsNullOrWhiteSpace(logicalId))
            throw new ArgumentException("Logical id must be non-empty.", nameof(logicalId));
    }
}
