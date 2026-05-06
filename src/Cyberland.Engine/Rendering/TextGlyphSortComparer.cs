using System.Collections.Generic;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Sort helper for text glyph indices: <see cref="TextGlyphDrawRequest.SortKey"/>, then
/// <see cref="TextGlyphDrawRequest.DepthHint"/>, then index tie-break.
/// </summary>
internal static class TextGlyphSortComparer
{
    [ThreadStatic]
    private static TextGlyphDrawRequest[]? _compareGlyphs;

    private static readonly IComparer<int> CompareGlyphIndicesIComparer = Comparer<int>.Create(CompareIndicesNoAlloc);

    public static void SortByOrder(int[] indices, TextGlyphDrawRequest[] glyphs, int count)
    {
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(glyphs);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > indices.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count > glyphs.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        for (var i = 0; i < count; i++)
            indices[i] = i;

        if (count < 2)
            return;

        _compareGlyphs = glyphs;
        try
        {
            Array.Sort(indices, 0, count, CompareGlyphIndicesIComparer);
        }
        finally
        {
            _compareGlyphs = null;
        }
    }

    public static int Compare(in TextGlyphDrawRequest a, in TextGlyphDrawRequest b)
    {
        var c = a.SortKey.CompareTo(b.SortKey);
        if (c != 0)
            return c;

        return a.DepthHint.CompareTo(b.DepthHint);
    }

    private static int CompareIndicesNoAlloc(int ia, int ib)
    {
        var c = Compare(in _compareGlyphs![ia], in _compareGlyphs[ib]);
        return c != 0 ? c : ia.CompareTo(ib);
    }
}
