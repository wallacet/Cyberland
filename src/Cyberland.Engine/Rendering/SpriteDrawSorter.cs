using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Sorts sprite draw order: lower <see cref="SpriteDrawRequest.Layer"/> first, then lower
/// <see cref="SpriteDrawRequest.SortKey"/>, then lower <see cref="SpriteDrawRequest.DepthHint"/>.
/// <c>Array.Sort</c> is not stable when keys tie; bitmap text assigns a small per-glyph <see cref="SpriteDrawRequest.DepthHint"/>
/// so overlapping transparent glyph quads keep a consistent draw order frame-to-frame.
/// Uses a thread-static draw buffer only for the duration of <see cref="SortByLayerOrder"/> so
/// <c>Array.Sort</c> does not allocate a per-call <c>Comparison&lt;int&gt;</c> closure (sort runs after parallel submit, on the recording path).
/// </summary>
public static class SpriteDrawSorter
{
    // Avoids a per-sort Comparison<int> closure allocation; sort runs on the render thread after parallel submit.
    [ThreadStatic]
    private static SpriteDrawRequest[]? _compareDraws;

    /// <summary>
    /// Fills <paramref name="indices"/> with a permutation that sorts <paramref name="draws"/> by layer, then sort key, then depth hint.
    /// </summary>
    /// <param name="indices">Same length as <paramref name="draws"/> (frame path uses exact-sized scratch buffers).</param>
    /// <param name="draws">Sprite batch to order.</param>
    public static void SortByLayerOrder(int[] indices, SpriteDrawRequest[] draws)
    {
        for (var i = 0; i < indices.Length; i++)
            indices[i] = i;

        if (indices.Length < 2)
            return;

        // O(n log n); ties may reorder relative to input (unlike a stable selection sort).
        _compareDraws = draws;
        try
        {
            Array.Sort(indices, CompareIndicesNoAlloc);
        }
        finally
        {
            _compareDraws = null;
        }
    }

    private static int CompareIndicesNoAlloc(int ia, int ib) =>
        Compare(in _compareDraws![ia], in _compareDraws[ib]);

    /// <summary>Comparer used by <see cref="SortByLayerOrder"/>; negative if <paramref name="a"/> should draw before <paramref name="b"/>.</summary>
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
