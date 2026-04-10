using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Stable sort for sprite draws: lower <see cref="SpriteDrawRequest.Layer"/> first, then lower
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

        // Selection sort avoids capturing ReadOnlySpan in a comparer delegate (ref-like restriction).
        for (var i = 0; i < indices.Length - 1; i++)
        {
            var min = i;
            for (var j = i + 1; j < indices.Length; j++)
            {
                if (Compare(draws[indices[j]], draws[indices[min]]) < 0)
                    min = j;
            }

            if (min != i)
                (indices[i], indices[min]) = (indices[min], indices[i]);
        }
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
