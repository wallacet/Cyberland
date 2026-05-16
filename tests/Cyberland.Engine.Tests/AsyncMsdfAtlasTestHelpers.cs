namespace Cyberland.Engine.Tests;

/// <summary>Polls render-thread MSDF drains until async atlas tasks complete (CI-safe vs fixed short timeouts).</summary>
internal static class AsyncMsdfAtlasTestHelpers
{
    public static async Task DrainUntilComplete(Action drainPendingUploads, Task pending, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(drainPendingUploads);
        ArgumentNullException.ThrowIfNull(pending);

        var started = Environment.TickCount64;
        var limitMs = (long)timeout.TotalMilliseconds;
        while (!pending.IsCompleted && Environment.TickCount64 - started < limitMs)
        {
            drainPendingUploads();
            await Task.Delay(10).ConfigureAwait(false);
        }
    }
}
