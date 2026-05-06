using Cyberland.Engine.Rendering;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class TextGlyphSortComparerTests
{
    [Fact]
    public void SortByOrder_orders_sort_key_then_depth_then_index()
    {
        var glyphs = new TextGlyphDrawRequest[3];
        glyphs[0] = new TextGlyphDrawRequest { SortKey = 10f, DepthHint = 1f };
        glyphs[1] = new TextGlyphDrawRequest { SortKey = 5f, DepthHint = 99f };
        glyphs[2] = new TextGlyphDrawRequest { SortKey = 10f, DepthHint = 2f };

        var indices = new int[glyphs.Length];
        TextGlyphSortComparer.SortByOrder(indices, glyphs, glyphs.Length);
        Assert.Equal(new[] { 1, 0, 2 }, indices);
    }

    [Fact]
    public void SortByOrder_handles_empty_and_singleton_counts()
    {
        var emptyGlyphs = Array.Empty<TextGlyphDrawRequest>();
        var emptyIndices = Array.Empty<int>();
        TextGlyphSortComparer.SortByOrder(emptyIndices, emptyGlyphs, 0);

        var oneGlyph = new[] { new TextGlyphDrawRequest { SortKey = 1f, DepthHint = 2f } };
        var oneIndex = new int[1];
        TextGlyphSortComparer.SortByOrder(oneIndex, oneGlyph, 1);
        Assert.Equal(0, oneIndex[0]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void SortByOrder_validates_count(int count)
    {
        var glyphs = new[] { new TextGlyphDrawRequest() };
        var indices = new[] { 0 };
        Assert.Throws<ArgumentOutOfRangeException>(() => TextGlyphSortComparer.SortByOrder(indices, glyphs, count));
    }

    [Fact]
    public void SortByOrder_throws_when_count_exceeds_glyph_array()
    {
        var glyphs = new[] { new TextGlyphDrawRequest() };
        var indices = new[] { 0, 1 };
        Assert.Throws<ArgumentOutOfRangeException>(() => TextGlyphSortComparer.SortByOrder(indices, glyphs, 2));
    }

    [Fact]
    public void SortByOrder_uses_index_tie_break_for_identical_keys()
    {
        var glyphs = new[]
        {
            new TextGlyphDrawRequest { SortKey = 10f, DepthHint = 1f },
            new TextGlyphDrawRequest { SortKey = 10f, DepthHint = 1f }
        };

        var indices = new[] { 1, 0 };
        TextGlyphSortComparer.SortByOrder(indices, glyphs, 2);
        Assert.Equal(new[] { 0, 1 }, indices);
    }
}
