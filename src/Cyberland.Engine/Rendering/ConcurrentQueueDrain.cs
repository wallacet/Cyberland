using System.Collections.Concurrent;

namespace Cyberland.Engine.Rendering;

// Purpose: Deterministic drain of submit queues into reusable scratch arrays for FramePlan build, plus discard when
// a present is skipped (see VulkanRenderer DrawFrame) so a later successful Build() does not merge multiple ECS ticks.
//
// Why dequeue until empty (not queue.Count): ConcurrentQueue.Count is documented as approximate under concurrency —
// trusting it as an upper bound left surplus items that resurfaced on later frames (HUD ghosting).

/// <summary>
/// Helpers for <see cref="VulkanRenderer"/> submission queues: drain everything into grow-only scratch for <see cref="FramePlan"/>
/// snapshots, or drop everything when recording aborts.
/// </summary>
internal static class ConcurrentQueueDrain
{
    /// <summary>Dequeues and drops every element (e.g. swapchain out-of-date before <c>Build()</c> runs).</summary>
    internal static void DiscardAll<T>(ConcurrentQueue<T> queue)
    {
        while (queue.TryDequeue(out _)) { }
    }

    /// <summary>Dequeues every element. Returns the number of elements written to <paramref name="result"/>.</summary>
    internal static int DrainToScratch<T>(ConcurrentQueue<T> queue, ref T[]? scratch, out T[] result)
    {
        var i = 0;
        // Prime capacity from a count hint; still grow if more elements appear while dequeuing.
        var hint = queue.Count;
        if (hint > 0)
            EnsureScratch(ref scratch, hint);

        while (queue.TryDequeue(out var value))
        {
            EnsureScratch(ref scratch, i + 1);
            scratch![i++] = value;
        }

        if (i == 0)
        {
            result = [];
            return 0;
        }

        result = scratch!;
        return i;
    }

    /// <summary>
    /// Grow-only resize (never shrinks): amortized doubling avoids reallocating every frame when batches grow.
    /// Scratch arrays therefore stay larger than the current drain count — callers must track logical counts separately.
    /// </summary>
    private static void EnsureScratch<T>(ref T[]? scratch, int requiredLength)
    {
        if (scratch is not null && scratch.Length >= requiredLength)
            return;

        var newLen = scratch is null
            ? requiredLength
            : Math.Max(requiredLength, scratch.Length * 2);
        Array.Resize(ref scratch, newLen);
    }
}
