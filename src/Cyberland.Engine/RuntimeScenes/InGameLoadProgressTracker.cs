namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// In-game load progress (separate keys from <see cref="Hosting.StartupProgressTracker"/> cold-start phases).
/// </summary>
public sealed class InGameLoadProgressTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, float> _phase01 = new(StringComparer.Ordinal);

    /// <summary>Reports monotonic per-phase progress in <c>[0,1]</c>.</summary>
    public void ReportPhaseProgress(string key, float value01)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var clamped = Math.Clamp(value01, 0f, 1f);
        lock (_gate)
        {
            if (_phase01.TryGetValue(key, out var cur))
                _phase01[key] = Math.Max(cur, clamped);
            else
                _phase01[key] = clamped;
        }
    }

    /// <summary>Latest reported value for <paramref name="key"/>, or 0.</summary>
    public float GetPhaseProgress(string key)
    {
        lock (_gate)
            return _phase01.TryGetValue(key, out var v) ? v : 0f;
    }

    /// <summary>Clears all phases (e.g. when returning to main menu).</summary>
    public void Reset()
    {
        lock (_gate)
            _phase01.Clear();
    }
}
