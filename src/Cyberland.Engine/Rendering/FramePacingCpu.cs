namespace Cyberland.Engine.Rendering;

/// <summary>
/// CPU-side delay math for <see cref="FramePacingMode.Limited"/> (testable without sleeping).
/// </summary>
/// <remarks>Windows timer resolution may affect how closely wall-clock pacing matches <see cref="GetRemainingDelaySeconds"/>.</remarks>
public static class FramePacingCpu
{
    /// <summary>
    /// Returns how many seconds to wait after <paramref name="elapsedSeconds"/> to reach one frame at <paramref name="targetFps"/>.
    /// </summary>
    /// <returns>Non-negative seconds; zero if the budget is already exhausted or <paramref name="targetFps"/> is invalid.</returns>
    public static double GetRemainingDelaySeconds(double elapsedSeconds, int targetFps)
    {
        if (targetFps < 1)
            return 0;
        var budget = 1.0 / targetFps;
        var r = budget - elapsedSeconds;
        return r > 0 ? r : 0;
    }
}
