using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Xunit;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Detects viewport underlines drawn through visible glyph ink using row baseline + MSDF quads. MSDF boxes are much taller
/// than ink; clipping each quad’s bottom to <c>baseline + descender allowance</c> drops transparent padding below Latin
/// descenders so an underline sitting just under caps still passes.
/// </summary>
internal static class ViewportUnderlineGlyphBodyAssert
{
    /// <summary>
    /// Viewport +Y down. Fails when the underline stroke overlaps the approximate ink band inside glyph quads.
    /// </summary>
    public static void UnderlineMustNotOverlapBaselineClippedGlyphInk(
        in SpriteDrawRequest underline,
        IEnumerable<TextGlyphDrawRequest> rowGlyphs,
        float baselineYDownSnapped,
        float sizePixels)
    {
        var uTop = underline.CenterWorld.Y - underline.HalfExtentsWorld.Y;
        var uBottom = underline.CenterWorld.Y + underline.HalfExtentsWorld.Y;
        var belowBaselineCap = MathF.Max(3f, sizePixels * TextDecorationMetrics.ViewportUnderlineBaselineInkDescenderMaxEm);

        foreach (var g in rowGlyphs)
        {
            var gTop = g.Center.Y - g.HalfExtents.Y;
            var gBottom = g.Center.Y + g.HalfExtents.Y;
            var inkBottom = MathF.Min(gBottom, baselineYDownSnapped + belowBaselineCap);
            if (inkBottom <= gTop + 1e-3f)
                continue;

            var overlapsInk = uBottom > gTop + 1e-3f && uTop < inkBottom - 1e-3f;
            Assert.False(overlapsInk,
                $"underline [{uTop:F3},{uBottom:F3}] must not intersect approximate ink [{gTop:F3},{inkBottom:F3}] (baseline={baselineYDownSnapped:F3})");
        }
    }
}
