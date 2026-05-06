namespace Cyberland.Engine.Rendering;

/// <summary>
/// Deterministic overflow policy for per-frame deferred light queues.
/// </summary>
/// <remarks>
/// The renderer can reorder overflowing light arrays into a stable value-order before clamping so worker-thread queue
/// interleaving does not change which lights survive the cap.
/// </remarks>
internal static class LightSubmissionPolicy
{
    public static int ClampWithDropCount(int submittedLights, int maxLights, out int droppedLights)
    {
        if (submittedLights <= 0)
        {
            droppedLights = 0;
            return 0;
        }

        if (submittedLights <= maxLights)
        {
            droppedLights = 0;
            return submittedLights;
        }

        droppedLights = submittedLights - maxLights;
        return maxLights;
    }
}
