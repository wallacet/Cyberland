namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Root-owned session clock: wall time, scale, pause. Additive scene worlds must not advance this; they read <see cref="SessionSeconds"/>.
/// </summary>
public sealed class GlobalSessionClock
{
    private bool _paused;
    private float _timeScale = 1f;
    private double _sessionSeconds;

    /// <summary>When true, <see cref="Advance"/> does not increase session time.</summary>
    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    /// <summary>Multiplier applied to delta seconds in <see cref="Advance"/>.</summary>
    public float TimeScale
    {
        get => _timeScale;
        set => _timeScale = Math.Max(0f, value);
    }

    /// <summary>Accumulated scaled session seconds.</summary>
    public double SessionSeconds => _sessionSeconds;

    /// <summary>Adds scaled delta from a root-world tick.</summary>
    public void Advance(float deltaSeconds)
    {
        if (_paused || deltaSeconds <= 0f)
            return;
        _sessionSeconds += deltaSeconds * _timeScale;
    }
}
