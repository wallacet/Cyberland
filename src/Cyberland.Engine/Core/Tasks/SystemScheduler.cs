using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Runs ECS systems in strict <strong>registration order</strong>. Each entry is either an <see cref="ISystem"/>
/// or an <see cref="IParallelSystem"/>; optional <see cref="IEarlyUpdate"/> / <see cref="IFixedUpdate"/> / <see cref="ILateUpdate"/>
/// (or parallel equivalents) control which phases run. Phase interfaces are resolved once at <c>Register*</c> so
/// <see cref="RunFrame(World, float)"/> does not repeat type tests on the hot path.
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

        // Resolved once at Register* so RunFrame avoids repeated pattern-matching / type tests on every tick.
        public IEarlyUpdate? Early { get; init; }
        public IFixedUpdate? Fixed { get; init; }
        public ILateUpdate? Late { get; init; }
    }

    private sealed class ParallelEntry : Entry
    {
        public required IParallelSystem System { get; init; }

        public IParallelEarlyUpdate? Early { get; init; }
        public IParallelFixedUpdate? Fixed { get; init; }
        public IParallelLateUpdate? Late { get; init; }
    }

    private readonly ParallelismSettings _parallelism;
    private readonly List<Entry> _entries = new();
    private readonly Dictionary<string, int> _logicalIds = new(StringComparer.Ordinal);

    private float _fixedAccumulator;

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

    /// <summary>Registers or replaces a sequential system. Execution order follows registration order.</summary>
    public void RegisterSequential(string logicalId, ISystem system, bool enabled = true)
    {
        ValidateLogicalId(logicalId);
        ArgumentNullException.ThrowIfNull(system);
        Upsert(logicalId, new SequentialEntry
        {
            Id = logicalId,
            System = system,
            Enabled = enabled,
            Started = false,
            Early = system as IEarlyUpdate,
            Fixed = system as IFixedUpdate,
            Late = system as ILateUpdate,
        });
    }

    /// <summary>Registers or replaces a parallel system. Execution order follows registration order.</summary>
    public void RegisterParallel(string logicalId, IParallelSystem system, bool enabled = true)
    {
        ValidateLogicalId(logicalId);
        ArgumentNullException.ThrowIfNull(system);
        Upsert(logicalId, new ParallelEntry
        {
            Id = logicalId,
            System = system,
            Enabled = enabled,
            Started = false,
            Early = system as IParallelEarlyUpdate,
            Fixed = system as IParallelFixedUpdate,
            Late = system as IParallelLateUpdate,
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
        RebuildIdMap();
        SystemUnregistered?.Invoke(logicalId);
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
                    se.Early?.OnEarlyUpdate(world, deltaSeconds);
                    break;
                case ParallelEntry pe:
                    pe.Early?.OnParallelEarlyUpdate(world, deltaSeconds, opts);
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
                        se.Fixed?.OnFixedUpdate(world, fixedDt);
                        break;
                    case ParallelEntry pe:
                        pe.Fixed?.OnParallelFixedUpdate(world, fixedDt, opts);
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
                    se.Late?.OnLateUpdate(world, deltaSeconds);
                    break;
                case ParallelEntry pe:
                    pe.Late?.OnParallelLateUpdate(world, deltaSeconds, opts);
                    break;
            }
        }

        AfterLateUpdate?.Invoke(world, deltaSeconds);
    }

    private void EnsureStarted(World world, Entry e)
    {
        if (e.Started)
            return;

        switch (e)
        {
            case SequentialEntry se:
                se.System.OnStart(world);
                break;
            case ParallelEntry pe:
                pe.System.OnStart(world);
                break;
        }

        e.Started = true;
        SystemStarted?.Invoke(e.Id);
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
