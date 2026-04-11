using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Sorts sprite draw order: lower <see cref="SpriteDrawRequest.Layer"/> first, then lower
/// <see cref="SpriteDrawRequest.SortKey"/>, then lower <see cref="SpriteDrawRequest.DepthHint"/>.
/// </summary>
public static class SpriteDrawSorter
{
    /// <summary>
    /// Fills <paramref name="indices"/> with a permutation that sorts <paramref name="draws"/> by layer, then sort key, then depth hint.
    /// </summary>
    /// <param name="indices">Pre-allocated to at least <paramref name="draws"/>.Length.</param>
    /// <param name="draws">Sprite batch to order.</param>
    public static void SortByLayerOrder(int[] indices, SpriteDrawRequest[] draws)
    {
        for (var i = 0; i < indices.Length; i++)
            indices[i] = i;

        if (indices.Length < 2)
            return;

        // O(n log n); ties may reorder relative to input (unlike a stable selection sort).
        Array.Sort(indices, (ia, ib) => Compare(in draws[ia], in draws[ib]));
    }

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
