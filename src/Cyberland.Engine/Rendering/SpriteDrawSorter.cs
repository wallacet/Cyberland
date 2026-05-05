using System.Collections.Generic;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

// Stable ordering for SpriteDrawRequest batches after FramePlan drains grow-only scratch arrays.
// Always pass the active batch count — sorting indices.Length would compare stale trailing scratch (see FrameExecution).

/// <summary>
/// Sorts sprite draw order: lower <see cref="SpriteDrawRequest.Layer"/> first, then lower
/// <see cref="SpriteDrawRequest.SortKey"/>, then lower <see cref="SpriteDrawRequest.DepthHint"/>.
/// <c>Array.Sort</c> is not stable when keys tie; bitmap text assigns a small per-glyph <see cref="SpriteDrawRequest.DepthHint"/>
/// so overlapping transparent glyph quads keep a consistent draw order frame-to-frame.
/// Uses a thread-static draw buffer only for the duration of <see cref="SortByLayerOrder(int[], SpriteDrawRequest[])"/> so
/// <c>Array.Sort</c> does not allocate a per-call <c>Comparison&lt;int&gt;</c> closure (sort runs after parallel submit, on the recording path).
/// </summary>
public static class SpriteDrawSorter
{
    // Avoids a per-sort Comparison<int> closure allocation; sort runs on the render thread after parallel submit.
    [ThreadStatic]
    private static SpriteDrawRequest[]? _compareDraws;

    // Comparer<int>.Create wraps our comparison so Array.Sort can use the (index, length, IComparer) overload.
    private static readonly IComparer<int> CompareDrawIndicesIComparer = Comparer<int>.Create(CompareIndicesNoAlloc);

    /// <summary>
    /// Fills <paramref name="indices"/> with a permutation that sorts <paramref name="draws"/> by layer, then sort key, then depth hint (entire array).
    /// </summary>
    /// <param name="indices">Permutation buffer; length must match the number of draws being sorted (tests, small batches).</param>
    /// <param name="draws">Sprite batch to order.</param>
    public static void SortByLayerOrder(int[] indices, SpriteDrawRequest[] draws) =>
        SortByLayerOrder(indices, draws, indices.Length);

    /// <summary>
    /// Fills <paramref name="indices"/>[0..<paramref name="count"/>) with a permutation of <c>0..count-1</c> that sorts
    /// <paramref name="draws"/>[0..<paramref name="count"/>) by layer, sort key, depth hint.
    /// </summary>
    /// <remarks>
    /// <see cref="VulkanRenderer"/> drains queues into grow-only scratch arrays during frame planning: when this frame's batch is
    /// smaller than the previous frame, <paramref name="indices"/> may still be long, but only the leading <paramref name="count"/>
    /// slots must be sorted — otherwise <c>Array.Sort</c> would compare stale trailing scratch slots and reorder draws incorrectly
    /// (HUD “ghost” glyphs after long→short text, or missing draws).
    /// </remarks>
    /// <param name="indices">Must have length ≥ <paramref name="count"/>.</param>
    /// <param name="draws">Backing store; only indices &lt; <paramref name="count"/> are read.</param>
    /// <param name="count">Number of active sprites in <paramref name="draws"/> for this frame.</param>
    public static void SortByLayerOrder(int[] indices, SpriteDrawRequest[] draws, int count)
    {
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(draws);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > indices.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count > draws.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        for (var i = 0; i < count; i++)
            indices[i] = i;

        if (count < 2)
            return;

        // O(n log n); ties may reorder relative to input (unlike a stable selection sort).
        _compareDraws = draws;
        try
        {
            Array.Sort(indices, 0, count, CompareDrawIndicesIComparer);
        }
        finally
        {
            _compareDraws = null;
        }
    }

    /// <summary>
    /// <paramref name="ia"/> / <paramref name="ib"/> are draw-array indices (values stored in the permutation array),
    /// not Sort pass positions — ties break on index so Array.Sort stays deterministic.
    /// </summary>
    private static int CompareIndicesNoAlloc(int ia, int ib)
    {
        var c = Compare(in _compareDraws![ia], in _compareDraws[ib]);
        return c != 0 ? c : ia.CompareTo(ib);
    }

    /// <summary>Comparer used by <see cref="SortByLayerOrder(int[], SpriteDrawRequest[], int)"/>; negative if <paramref name="a"/> should draw before <paramref name="b"/>.</summary>
    public static int Compare(in SpriteDrawRequest a, in SpriteDrawRequest b)
    {
        var c = a.Layer.CompareTo(b.Layer);
        if (c != 0)
            return c;

        c = a.SortKey.CompareTo(b.SortKey);
        if (c != 0)
            return c;

        return a.DepthHint.CompareTo(b.DepthHint);
    }
}
