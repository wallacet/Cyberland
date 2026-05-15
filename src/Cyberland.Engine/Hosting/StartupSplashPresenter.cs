namespace Cyberland.Engine.Hosting;

/// <summary>
/// Startup splash sequence controller.
/// A splash advances automatically by duration and can optionally advance one-at-a-time on user skip requests.
/// </summary>
public sealed class StartupSplashPresenter
{
    private readonly IReadOnlyList<StartupSplashScreen> _screens;
    private int _index;
    private double _elapsedInScreenSeconds;

    /// <summary>
    /// Creates a presenter from an ordered list of splash cards (empty uses <see cref="StartupSplashScreen.Default"/>).
    /// </summary>
    /// <param name="screens">Splash cards in display order.</param>
    public StartupSplashPresenter(IReadOnlyList<StartupSplashScreen> screens)
    {
        _screens = screens.Count > 0 ? screens : [StartupSplashScreen.Default];
        _index = 0;
        _elapsedInScreenSeconds = 0d;
    }

    /// <summary>Built-in three-card sequence used by the stock host.</summary>
    public static StartupSplashPresenter CreateDefault() =>
        new(
        [
            new StartupSplashScreen("Booting", 1.20f, 0f, 0f, 0f),
            new StartupSplashScreen("Loading Runtime", 1.20f, 0f, 0f, 0f),
            new StartupSplashScreen("Preparing World", 1.20f, 0f, 0f, 0f)
        ]);

    /// <summary>True when the sequence has finished (timeline and skips no longer advance the index).</summary>
    public bool IsCompleted => _index >= _screens.Count;

    /// <summary>Current card, or the last card after <see cref="IsCompleted"/> is true.</summary>
    public StartupSplashScreen Current => IsCompleted ? _screens[^1] : _screens[_index];

    /// <summary>
    /// Advances timeline and optionally handles one-shot skip requests.
    /// </summary>
    public void Advance(double deltaSeconds, bool canSkip, bool skipRequested)
    {
        if (IsCompleted)
            return;

        if (canSkip && skipRequested)
        {
            _index++;
            _elapsedInScreenSeconds = 0d;
            return;
        }

        var dt = Math.Max(0d, deltaSeconds);
        _elapsedInScreenSeconds += dt;
        var duration = Math.Max(0.05f, Current.DurationSeconds);
        if (_elapsedInScreenSeconds >= duration)
        {
            _elapsedInScreenSeconds = 0d;
            _index++;
        }
    }
}

/// <summary>Single startup splash card descriptor.</summary>
/// <param name="Label">Short title for UI or window chrome.</param>
/// <param name="DurationSeconds">Auto-advance time when skip is not used.</param>
/// <param name="R">Bootstrap clear red channel in <c>[0,1]</c>.</param>
/// <param name="G">Bootstrap clear green channel in <c>[0,1]</c>.</param>
/// <param name="B">Bootstrap clear blue channel in <c>[0,1]</c>.</param>
public readonly record struct StartupSplashScreen(
    string Label,
    float DurationSeconds,
    float R,
    float G,
    float B)
{
    /// <summary>Fallback single card when no sequence is configured.</summary>
    public static StartupSplashScreen Default => new("Loading", 1f, 0f, 0f, 0f);
}
