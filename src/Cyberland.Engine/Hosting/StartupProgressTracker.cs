namespace Cyberland.Engine.Hosting;

/// <summary>
/// Thread-safe weighted startup progress tracker shared by host bootstrap and mod loading.
/// </summary>
public sealed class StartupProgressTracker
{
    private sealed class PhaseState
    {
        public required string Key { get; init; }
        public required float Weight { get; init; }
        public float Reported01 { get; set; }
        public string? Label { get; set; }
        public string? Owner { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, PhaseState> _phases = new(StringComparer.Ordinal);
    private string? _activePhaseKey;
    private float _displayProgress01;

    /// <summary>Clears all phases and resets progress for a new startup session.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _phases.Clear();
            _activePhaseKey = null;
            _displayProgress01 = 0f;
        }
    }

    /// <summary>Registers (or reuses) a weighted phase and marks it active for user-facing labels.</summary>
    public IDisposable BeginPhase(string key, float weight, string? label = null, string? owner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (weight <= 0f)
            weight = 0.0001f;

        lock (_gate)
        {
            if (!_phases.TryGetValue(key, out var state))
            {
                state = new PhaseState
                {
                    Key = key,
                    Weight = weight,
                    Reported01 = 0f,
                    Label = label,
                    Owner = owner
                };
                _phases.Add(key, state);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(label))
                    state.Label = label;
                if (!string.IsNullOrWhiteSpace(owner))
                    state.Owner = owner;
            }

            _activePhaseKey = key;
        }

        return new Scope(this, key);
    }

    /// <summary>
    /// Reports monotonic per-phase progress in <c>[0,1]</c>. Values lower than the current value are ignored.
    /// </summary>
    public void ReportPhaseProgress(string key, float value01, string? label = null, string? owner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var clamped = Math.Clamp(value01, 0f, 1f);

        lock (_gate)
        {
            if (!_phases.TryGetValue(key, out var state))
            {
                state = new PhaseState
                {
                    Key = key,
                    Weight = 1f,
                    Reported01 = clamped,
                    Label = label,
                    Owner = owner
                };
                _phases.Add(key, state);
            }
            else
            {
                state.Reported01 = Math.Max(state.Reported01, clamped);
                if (!string.IsNullOrWhiteSpace(label))
                    state.Label = label;
                if (!string.IsNullOrWhiteSpace(owner))
                    state.Owner = owner;
            }

            _activePhaseKey = key;
        }
    }

    /// <summary>Advances display progress toward reported progress with a bounded catch-up rate.</summary>
    public void AdvanceDisplay(float deltaSeconds, float maxCatchupPerSecond)
    {
        if (deltaSeconds < 0f)
            deltaSeconds = 0f;
        if (maxCatchupPerSecond <= 0f)
            maxCatchupPerSecond = 1f;

        lock (_gate)
        {
            var reported = ComputeReportedProgressUnsafe();
            var step = maxCatchupPerSecond * deltaSeconds;
            _displayProgress01 = Math.Min(reported, _displayProgress01 + step);
        }
    }

    /// <summary>Marks all known phases complete and pins display/reported to 1.0.</summary>
    public void MarkComplete()
    {
        lock (_gate)
        {
            foreach (var phase in _phases.Values)
                phase.Reported01 = 1f;
            _displayProgress01 = 1f;
        }
    }

    /// <summary>Returns a thread-safe snapshot used by loading UI.</summary>
    public StartupProgressSnapshot Snapshot()
    {
        lock (_gate)
        {
            var reported = ComputeReportedProgressUnsafe();
            string? label = null;
            string? owner = null;
            if (_activePhaseKey is not null && _phases.TryGetValue(_activePhaseKey, out var active))
            {
                label = active.Label ?? active.Key;
                owner = active.Owner;
            }

            return new StartupProgressSnapshot(
                reported,
                _displayProgress01,
                label,
                owner,
                _phases.Count);
        }
    }

    private void CompletePhase(string key)
    {
        lock (_gate)
        {
            if (_phases.TryGetValue(key, out var state))
                state.Reported01 = 1f;
        }
    }

    private float ComputeReportedProgressUnsafe()
    {
        if (_phases.Count == 0)
            return 0f;

        double weighted = 0d;
        double totalWeight = 0d;
        foreach (var phase in _phases.Values)
        {
            weighted += phase.Reported01 * phase.Weight;
            totalWeight += phase.Weight;
        }

        // Weights are always positive (see <see cref="BeginPhase"/>); guard only against floating drift.
        return (float)Math.Clamp(weighted / Math.Max(totalWeight, double.Epsilon), 0d, 1d);
    }

    private sealed class Scope : IDisposable
    {
        private StartupProgressTracker? _owner;
        private readonly string _key;

        public Scope(StartupProgressTracker owner, string key)
        {
            _owner = owner;
            _key = key;
        }

        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
                return;
            _owner = null;
            owner.CompletePhase(_key);
        }
    }
}

/// <summary>Immutable snapshot for startup loading UI and diagnostics.</summary>
public readonly record struct StartupProgressSnapshot(
    float ReportedProgress01,
    float DisplayProgress01,
    string? ActiveLabel,
    string? ActiveOwner,
    int PhaseCount);
