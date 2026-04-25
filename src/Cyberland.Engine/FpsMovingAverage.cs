using System.Collections.Generic;

namespace Cyberland.Engine;

/// <summary>
/// Simple moving average of frame rate: average of <c>1 / mean(Δt)</c> over frame durations kept in a sliding
/// <see cref="WindowSeconds"/> sum. Oldest samples drop only while at least two frames remain, so a single long
/// hitch is still reported as a low (nonzero) rate.
/// </summary>
public sealed class FpsMovingAverage
{
    /// <summary>Default window (seconds) for demo HUDs: half a second of recent frames.</summary>
    public const float DefaultWindowSeconds = 0.5f;

    private readonly Queue<float> _deltas = new();
    private float _sum;

    /// <summary>Creates a tracker; <paramref name="windowSeconds"/> sets <see cref="WindowSeconds"/>.</summary>
    public FpsMovingAverage(float windowSeconds = DefaultWindowSeconds) => WindowSeconds = windowSeconds;

    /// <summary>Width of the sliding window in seconds; can be changed at runtime.</summary>
    public float WindowSeconds { get; set; }

    /// <summary>Feed the most recent present/frame delta in seconds (e.g. <see cref="Hosting.GameHostServices.LastPresentDeltaSeconds"/>).</summary>
    public void AddFrameDeltaSeconds(float deltaSeconds)
    {
        if (deltaSeconds <= 1e-9f)
            return;

        _deltas.Enqueue(deltaSeconds);
        _sum += deltaSeconds;
        var w = WindowSeconds;
        while (_sum > w && _deltas.Count > 1)
            _sum -= _deltas.Dequeue();
    }

    /// <summary>Average frames per second over the retained samples: <c>count / sum(Δt)</c>.</summary>
    public bool TryGetAverageFps(out float fps)
    {
        if (_deltas.Count == 0 || _sum < 1e-9f)
        {
            fps = 0f;
            return false;
        }

        fps = _deltas.Count / _sum;
        return true;
    }
}
