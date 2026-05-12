using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Xunit;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Shared regression checks for viewport (+Y down) underline placement vs approximate ink band (matches engine metrics).
/// </summary>
internal static class ViewportUnderlinePlacementAssert
{
    /// <summary>
    /// Fails when the underline is entirely above text, intersects approximate ink, or sits more than ceil(10% × visible height) below it.
    /// </summary>
    public static void FollowsTightVisibleBandRules(
        in SpriteDrawRequest underline,
        float baselineYDownSnapped,
        float sizePixels,
        float inkMinTopVp,
        float inkMaxBottomVp)
    {
        var tightTop = MathF.Max(inkMinTopVp, baselineYDownSnapped - sizePixels * TextDecorationMetrics.ViewportUnderlineTightBandAboveBaselineEm);
        var approxInkBottom = TextDecorationMetrics.ViewportUnderlineApproxInkBottomPx(
            baselineYDownSnapped,
            sizePixels,
            inkMaxBottomVp);
        if (approxInkBottom <= tightTop + 1e-3f)
        {
            tightTop = inkMinTopVp;
            approxInkBottom = inkMaxBottomVp;
        }
        var visibleH = approxInkBottom - tightTop;
        Assert.True(visibleH > 1e-3f, "degenerate tight visible height");
        var maxGap = TextDecorationMetrics.MaxUnderlineGapBelowVisibleLinePx(visibleH);
        var minClearance = TextDecorationMetrics.ViewportUnderlineMinInkClearancePx(sizePixels);
        var strokeTop = underline.CenterWorld.Y - underline.HalfExtentsWorld.Y;
        var strokeBottom = underline.CenterWorld.Y + underline.HalfExtentsWorld.Y;

        Assert.True(strokeBottom > tightTop + 1e-3f,
            $"underline must not lie entirely above estimated visible text (strokeBottom={strokeBottom} tightTop={tightTop})");

        var crossesVisibleInk = strokeBottom > tightTop + 1e-3f && strokeTop < approxInkBottom + minClearance - 1e-3f;
        Assert.False(crossesVisibleInk,
            $"underline must not pass through approx ink [{tightTop:F3},{approxInkBottom:F3}] + clearance {minClearance:F3} (stroke [{strokeTop:F3},{strokeBottom:F3}])");

        Assert.True(strokeTop <= approxInkBottom + maxGap + 2e-2f,
            $"underline too far below visible bottom: strokeTop={strokeTop} tightBottom={approxInkBottom} maxGap={maxGap} (ceil 10% rule)");
    }
}
