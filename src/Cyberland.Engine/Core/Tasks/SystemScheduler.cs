using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Runs ECS systems in <strong>registration order</strong> by default; optional <see cref="RunBeforeAttribute"/> /
/// <see cref="RunAfterAttribute"/> on system classes add ordering constraints resolved when the entry list changes.
/// Each entry is either an <see cref="ISystem"/> or an <see cref="IParallelSystem"/>; chunk iteration uses
/// <see cref="IEcsQuerySource.QuerySpec"/> from that instance. Optional
/// <see cref="IEarlyUpdate"/> / <see cref="IFixedUpdate"/> / <see cref="ILateUpdate"/> (or parallel equivalents) control
/// which phases run. Phase interfaces are resolved once at <c>Register*</c> so <see cref="RunFrame(World, float)"/> does not
/// repeat type tests on the hot path.
/// </summary>
public sealed class SystemScheduler
{
    private abstract class Entry
    {
        public required string Id { get; init; }
        public required Type SystemType { get; init; }
        public required SystemQuerySpec QuerySpec { get; init; }
        public bool Enabled;
        public bool Started;
        public required Action<World> Start { get; init; }
        public Action<World, float>? Early { get; init; }
        public Action<World, float>? Fixed { get; init; }
        public Action<World, float>? Late { get; init; }
    }

    private sealed class SequentialEntry : Entry
    {
    }

    private sealed class ParallelEntry : Entry
    {
        public Action<World, float, ParallelOptions>? ParallelEarly { get; init; }
        public Action<World, float, ParallelOptions>? ParallelFixed { get; init; }
        public Action<World, float, ParallelOptions>? ParallelLate { get; init; }
    }

    private sealed class DeferScope : IDisposable
    {
        private SystemScheduler? _owner;

        public DeferScope(SystemScheduler owner) => _owner = owner;

        public void Dispose()
        {
            _owner?.EndDeferExecutionOrderRebuilds();
            _owner = null;
        }
    }

    private readonly ParallelismSettings _parallelism;
    private readonly List<Entry> _entries = new();
    private readonly Dictionary<string, int> _logicalIds = new(StringComparer.Ordinal);
    /// <summary>First-seen registration ordinal per logical id (tie-break for topological sort).</summary>
    private readonly Dictionary<string, int> _registrationOrdinals = new(StringComparer.Ordinal);

    private float _fixedAccumulator;
    private int _nextRegistrationOrdinal;
    private int _deferExecutionOrderRebuildDepth;
    private bool _constraintTargetsValidatedOnFirstRunFrame;

    /// <summary>Creates a scheduler that supplies <paramref name="parallelism"/> to parallel ECS systems.</summary>
    public SystemScheduler(ParallelismSettings parallelism) => _parallelism = parallelism;

    /// <summary>Fixed timestep step size in seconds (default 1/60).</summary>
    public float FixedDeltaSeconds { get; set; } = 1f / 60f;

    /// <summary>Maximum fixed substeps per <see cref="RunFrame(World, float)"/> call to avoid spiral-of-death.</summary>
    public int MaxSubstepsPerFrame { get; set; } = 8;

    /// <summary>Remaining fixed time carried to the next <see cref="RunFrame(World, float)"/> (read-only for diagnostics).</summary>
    public float FixedAccumulator => _fixedAccumulator;

    /// <summary>Raised after <see cref="ISystem.OnStart"/> / <see cref="IParallelSystem.OnStart"/> returns for an entry.</summary>
    public event Action<string>? SystemStarted;

    /// <summary>Raised when <see cref="SetEnabled"/> transitions an entry from disabled to enabled (not on initial <c>Register*</c>).</summary>
    public event Action<string>? SystemEnabled;

    /// <summary>Raised when <see cref="SetEnabled"/> transitions an entry from disabled to enabled.</summary>
    public event Action<string>? SystemDisabled;

    /// <summary>Raised after <see cref="TryUnregister"/> removes an entry.</summary>
    public event Action<string>? SystemUnregistered;

    /// <summary>Invoked after all early-phase callbacks for this frame.</summary>
    public event Action<World, float>? AfterEarlyUpdate;

    /// <summary>Invoked once per frame after the fixed loop completes (not per substep).</summary>
    public event Action<World, float>? AfterFixedUpdate;

    /// <summary>Invoked after all late-phase callbacks for this frame.</summary>
    public event Action<World, float>? AfterLateUpdate;

    /// <summary>
    /// Suppresses <see cref="TryRebuildExecutionOrder"/> after each structural change until <see cref="EndDeferExecutionOrderRebuilds"/>.
    /// Nesting increments a depth counter; the order is rebuilt once when the counter returns to zero.
    /// </summary>
    public void BeginDeferExecutionOrderRebuilds() => _deferExecutionOrderRebuildDepth++;

    /// <summary>
    /// Ends a matching <see cref="BeginDeferExecutionOrderRebuilds"/>; when the defer depth reaches zero, runs
    /// <see cref="TryRebuildExecutionOrder"/> once.
    /// </summary>
    public void EndDeferExecutionOrderRebuilds()
    {
        _deferExecutionOrderRebuildDepth--;
        if (_deferExecutionOrderRebuildDepth < 0)
            throw new InvalidOperationException("Unbalanced EndDeferExecutionOrderRebuilds (no matching Begin).");

        if (_deferExecutionOrderRebuildDepth == 0)
            TryRebuildExecutionOrder();
    }

    /// <summary>
    /// Same as <see cref="BeginDeferExecutionOrderRebuilds"/> followed by <see cref="EndDeferExecutionOrderRebuilds"/> on dispose.
    /// </summary>
    public IDisposable DeferExecutionOrderRebuilds()
    {
        BeginDeferExecutionOrderRebuilds();
        return new DeferScope(this);
    }

    /// <summary>Registers or replaces a sequential system. Execution order follows registration order unless constrained by attributes.</summary>
    /// <param name="logicalId">Stable id for ordering attributes, enable/disable, and diagnostics.</param>
    /// <param name="system">Sequential ECS system implementation; chunk query comes from <see cref="IEcsQuerySource.QuerySpec"/>.</param>
    /// <param name="enabled">When false, the entry is registered but skipped until <see cref="SetEnabled"/> enables it.</param>
    public void RegisterSequential(string logicalId, ISystem system, bool enabled = true)
    {
        ValidateLogicalId(logicalId);
        ArgumentNullException.ThrowIfNull(system);
        var query = system.QuerySpec;
        Upsert(logicalId, new SequentialEntry
        {
            Id = logicalId,
            SystemType = system.GetType(),
            QuerySpec = query,
            Start = world => system.OnStart(world, world.QueryChunks(query)),
            Enabled = enabled,
            Started = false,
            Early = system is IEarlyUpdate early
                ? (world, deltaSeconds) => early.OnEarlyUpdate(world, world.QueryChunks(query), deltaSeconds)
                : null,
            Fixed = system is IFixedUpdate fixedUpdate
                ? (world, fixedDeltaSeconds) => fixedUpdate.OnFixedUpdate(world, world.QueryChunks(query), fixedDeltaSeconds)
                : null,
            Late = system is ILateUpdate late
                ? (world, deltaSeconds) => late.OnLateUpdate(world, world.QueryChunks(query), deltaSeconds)
                : null,
        });
    }

    /// <summary>Registers or replaces a parallel system. Execution order follows registration order unless constrained by attributes.</summary>
    /// <param name="logicalId">Stable id for ordering attributes, enable/disable, and diagnostics.</param>
    /// <param name="system">Parallel ECS system implementation; chunk query comes from <see cref="IEcsQuerySource.QuerySpec"/>.</param>
    /// <param name="enabled">When false, the entry is registered but skipped until <see cref="SetEnabled"/> enables it.</param>
    public void RegisterParallel(string logicalId, IParallelSystem system, bool enabled = true)
    {
        ValidateLogicalId(logicalId);
        ArgumentNullException.ThrowIfNull(system);
        var query = system.QuerySpec;
        Upsert(logicalId, new ParallelEntry
        {
            Id = logicalId,
            SystemType = system.GetType(),
            QuerySpec = query,
            Start = world => system.OnStart(world, world.QueryChunks(query)),
            Enabled = enabled,
            Started = false,
            ParallelEarly = system is IParallelEarlyUpdate early
                ? (world, deltaSeconds, parallelOptions) => early.OnParallelEarlyUpdate(world, world.QueryChunks(query), deltaSeconds, parallelOptions)
                : null,
            ParallelFixed = system is IParallelFixedUpdate fixedUpdate
                ? (world, fixedDeltaSeconds, parallelOptions) => fixedUpdate.OnParallelFixedUpdate(world, world.QueryChunks(query), fixedDeltaSeconds, parallelOptions)
                : null,
            ParallelLate = system is IParallelLateUpdate late
                ? (world, deltaSeconds, parallelOptions) => late.OnParallelLateUpdate(world, world.QueryChunks(query), deltaSeconds, parallelOptions)
                : null,
        });
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
        _registrationOrdinals.Remove(logicalId);
        RebuildIdMap();
        SystemUnregistered?.Invoke(logicalId);
        NotifyStructureChanged();
        return true;
    }

    /// <summary>
    /// Executes one frame: <see cref="IEarlyUpdate"/> / <see cref="IParallelEarlyUpdate"/>, then fixed substeps, then
    /// <see cref="ILateUpdate"/> / <see cref="IParallelLateUpdate"/>.
    /// </summary>
    /// <remarks>
    /// Early runs once; <see cref="IFixedUpdate"/> may run multiple times per call. Input written in early is visible to every
    /// fixed substep — avoid resetting entire input components inside fixed after reading held controls.
    /// After the fixed loop, <see cref="FixedAccumulator"/> holds the remainder for optional render extrapolation.
    /// The stock host passes a callback into the three-parameter <c>RunFrame</c> overload so
    /// <see cref="Hosting.GameHostServices.FixedAccumulatorSeconds"/> is updated <strong>before</strong> late phase, allowing
    /// <see cref="ILateUpdate"/> to extrapolate visuals with the current frame remainder (not the previous frame).
    /// </remarks>
    /// <param name="world">ECS world passed to each system.</param>
    /// <param name="deltaSeconds">Real elapsed frame time in seconds (variable).</param>
    public void RunFrame(World world, float deltaSeconds) =>
        RunFrame(world, deltaSeconds, syncFixedAccumulatorBeforeLate: null);

    /// <summary>Same phases as <see cref="RunFrame(World, float)"/>, plus an optional hook after the fixed loop.</summary>
    /// <param name="world">ECS world passed to each system.</param>
    /// <param name="deltaSeconds">Real elapsed frame time in seconds (variable).</param>
    /// <param name="syncFixedAccumulatorBeforeLate">
    /// Invoked once per frame after fixed substeps with the current <see cref="FixedAccumulator"/> (before <see cref="AfterFixedUpdate"/> and late phase).
    /// Use to mirror the remainder into <see cref="Hosting.GameHostServices.FixedAccumulatorSeconds"/> for <see cref="ILateUpdate"/> extrapolation.
    /// </param>
    public void RunFrame(World world, float deltaSeconds, Action<float>? syncFixedAccumulatorBeforeLate)
    {
        if (!_constraintTargetsValidatedOnFirstRunFrame)
        {
            _constraintTargetsValidatedOnFirstRunFrame = true;
            ValidateAllConstraintTargetsRegistered();
        }

        if (FixedDeltaSeconds <= 0f)
            throw new InvalidOperationException($"{nameof(FixedDeltaSeconds)} must be positive.");

        var fixedDt = FixedDeltaSeconds;
        var maxSubsteps = MaxSubstepsPerFrame;
        var opts = _parallelism.CreateParallelOptions();

        foreach (var e in _entries)
        {
            if (!e.Enabled)
                continue;

            EnsureStarted(world, e);
        }

        foreach (var e in _entries)
        {
            if (!e.Enabled)
                continue;

            switch (e)
            {
                case SequentialEntry se:
                    se.Early?.Invoke(world, deltaSeconds);
                    break;
                case ParallelEntry pe:
                    pe.ParallelEarly?.Invoke(world, deltaSeconds, opts);
                    break;
            }
        }

        AfterEarlyUpdate?.Invoke(world, deltaSeconds);

        _fixedAccumulator += deltaSeconds;
        var substeps = 0;
        while (_fixedAccumulator >= fixedDt && substeps < maxSubsteps)
        {
            foreach (var e in _entries)
            {
                if (!e.Enabled)
                    continue;

                switch (e)
                {
                    case SequentialEntry se:
                        se.Fixed?.Invoke(world, fixedDt);
                        break;
                    case ParallelEntry pe:
                        pe.ParallelFixed?.Invoke(world, fixedDt, opts);
                        break;
                }
            }

            _fixedAccumulator -= fixedDt;
            substeps++;
        }

        syncFixedAccumulatorBeforeLate?.Invoke(_fixedAccumulator);

        AfterFixedUpdate?.Invoke(world, deltaSeconds);

        foreach (var e in _entries)
        {
            if (!e.Enabled)
                continue;

            switch (e)
            {
                case SequentialEntry se:
                    se.Late?.Invoke(world, deltaSeconds);
                    break;
                case ParallelEntry pe:
                    pe.ParallelLate?.Invoke(world, deltaSeconds, opts);
                    break;
            }
        }

        AfterLateUpdate?.Invoke(world, deltaSeconds);
    }

    private void EnsureStarted(World world, Entry e)
    {
        if (e.Started)
            return;

        e.Start.Invoke(world);

        e.Started = true;
        SystemStarted?.Invoke(e.Id);
    }

    private void Upsert(string logicalId, Entry entry)
    {
        if (_logicalIds.TryGetValue(logicalId, out var idx))
        {
            _entries[idx] = entry;
            NotifyStructureChanged();
            return;
        }

        _registrationOrdinals[logicalId] = _nextRegistrationOrdinal++;
        idx = _entries.Count;
        _entries.Add(entry);
        _logicalIds[logicalId] = idx;
        NotifyStructureChanged();
    }

    private void NotifyStructureChanged()
    {
        if (_deferExecutionOrderRebuildDepth == 0)
            TryRebuildExecutionOrder();
    }

    /// <summary>
    /// Applies <see cref="RunBeforeAttribute"/> / <see cref="RunAfterAttribute"/> constraints. Returns false if a referenced
    /// id is not registered yet (caller may register more systems and retry). Throws if constraints are contradictory (cycle).
    /// </summary>
    private bool TryRebuildExecutionOrder()
    {
        if (_entries.Count == 0)
            return true;

        var ids = new List<string>(_entries.Count);
        foreach (var e in _entries)
            ids.Add(e.Id);

        var rawEdges = new List<(string From, string To)>();
        foreach (var e in _entries)
        {
            var t = GetEntrySystemType(e);
            var id = e.Id;
            foreach (RunAfterAttribute attr in t.GetCustomAttributes(typeof(RunAfterAttribute), false))
            {
                if (!_logicalIds.ContainsKey(attr.TargetId))
                    return false;
                rawEdges.Add((attr.TargetId, id));
            }

            foreach (RunBeforeAttribute attr in t.GetCustomAttributes(typeof(RunBeforeAttribute), false))
            {
                if (!_logicalIds.ContainsKey(attr.TargetId))
                    return false;
                rawEdges.Add((id, attr.TargetId));
            }
        }

        var successors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            indegree[id] = 0;
            successors[id] = new List<string>();
        }

        var seenEdges = new HashSet<(string From, string To)>();
        foreach (var (from, to) in rawEdges)
        {
            if (!seenEdges.Add((from, to)))
                continue;
            successors[from].Add(to);
            indegree[to]++;
        }

        var result = new List<string>(ids.Count);
        var ready = new List<string>();
        foreach (var id in ids)
        {
            if (indegree[id] == 0)
                ready.Add(id);
        }

        while (ready.Count > 0)
        {
            string? best = null;
            var bestOrd = int.MaxValue;
            foreach (var id in ready)
            {
                var o = _registrationOrdinals[id];
                if (best is null || o < bestOrd)
                {
                    best = id;
                    bestOrd = o;
                }
            }

            ready.Remove(best!);
            result.Add(best!);
            foreach (var succ in successors[best!])
            {
                indegree[succ]--;
                if (indegree[succ] == 0)
                    ready.Add(succ);
            }
        }

        if (result.Count != ids.Count)
            throw new InvalidOperationException("System ordering constraints contain a cycle (check RunBefore / RunAfter attributes).");

        var idToEntry = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var e in _entries)
            idToEntry[e.Id] = e;

        _entries.Clear();
        foreach (var id in result)
            _entries.Add(idToEntry[id]);

        RebuildIdMap();
        return true;
    }

    private static Type GetEntrySystemType(Entry e)
    {
        return e.SystemType;
    }

    private void ValidateAllConstraintTargetsRegistered()
    {
        foreach (var e in _entries)
        {
            var t = GetEntrySystemType(e);
            foreach (RunAfterAttribute attr in t.GetCustomAttributes(typeof(RunAfterAttribute), false))
            {
                if (!_logicalIds.ContainsKey(attr.TargetId))
                {
                    throw new InvalidOperationException(
                        $"System \"{e.Id}\" has RunAfter(\"{attr.TargetId}\") but no system is registered with that logical id.");
                }
            }

            foreach (RunBeforeAttribute attr in t.GetCustomAttributes(typeof(RunBeforeAttribute), false))
            {
                if (!_logicalIds.ContainsKey(attr.TargetId))
                {
                    throw new InvalidOperationException(
                        $"System \"{e.Id}\" has RunBefore(\"{attr.TargetId}\") but no system is registered with that logical id.");
                }
            }
        }
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
