namespace Cyberland.Engine.Rendering;

/// <summary>
/// Deterministic overflow clamp for per-frame rendering submission queues (lights, sprites, text glyphs, and other
/// primitives whose counts may exceed GPU buffer or draw-call caps).
/// </summary>
/// <remarks>
/// The renderer can reorder overflowing arrays into a stable value-order before clamping so worker-thread queue
/// interleaving does not change which items survive the cap.
/// </remarks>
internal static class SubmissionClamp
{
    public static int ClampWithDropCount(int submittedCount, int maxCount, out int droppedCount)
    {
        if (submittedCount <= 0)
        {
            droppedCount = 0;
            return 0;
        }

        if (maxCount <= 0)
        {
            droppedCount = submittedCount;
            return 0;
        }

        if (submittedCount <= maxCount)
        {
            droppedCount = 0;
            return submittedCount;
        }

        droppedCount = submittedCount - maxCount;
        return maxCount;
    }
}
