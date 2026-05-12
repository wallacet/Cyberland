using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;
using Xunit;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Locks the three viewport underline placement rules implemented by <see cref="ViewportUnderlinePlacementAssert"/>.
/// </summary>
public sealed class ViewportUnderlinePlacementAssertTests
{
    private static SpriteDrawRequest Underline(float centerY, float halfHeight = 0.5f) =>
        new()
        {
            CenterWorld = new Vector2D<float>(50f, centerY),
            HalfExtentsWorld = new Vector2D<float>(40f, halfHeight)
        };

    [Fact]
    public void FollowsTightVisibleBandRules_fails_when_underline_entirely_above_visible_text()
    {
        const float baseline = 100f;
        const float sizePx = 20f;
        const float inkTop = 90f;
        const float inkBottom = 112f;
        TextDecorationMetrics.EstimateTightVisibleInkBandViewport(baseline, sizePx, inkTop, inkBottom, out var tightTop,
            out _);
        var tooHigh = Underline(tightTop - 6f);
        var ex = Record.Exception(() =>
            ViewportUnderlinePlacementAssert.FollowsTightVisibleBandRules(in tooHigh, baseline, sizePx, inkTop,
                inkBottom));
        Assert.NotNull(ex);
    }

    [Fact]
    public void FollowsTightVisibleBandRules_fails_when_underline_passes_through_visible_band()
    {
        const float baseline = 100f;
        const float sizePx = 20f;
        const float inkTop = 90f;
        const float inkBottom = 112f;
        TextDecorationMetrics.EstimateTightVisibleInkBandViewport(baseline, sizePx, inkTop, inkBottom, out var tightTop,
            out var tightBottom);
        var mid = (tightTop + tightBottom) * 0.5f;
        var thick = Underline(mid, halfHeight: (tightBottom - tightTop) * 0.6f);
        var ex = Record.Exception(() =>
            ViewportUnderlinePlacementAssert.FollowsTightVisibleBandRules(in thick, baseline, sizePx, inkTop,
                inkBottom));
        Assert.NotNull(ex);
    }

    [Fact]
    public void FollowsTightVisibleBandRules_fails_when_underline_more_than_allowed_gap_below_visible_bottom()
    {
        const float baseline = 100f;
        const float sizePx = 20f;
        const float inkTop = 90f;
        const float inkBottom = 112f;
        var tightTop = MathF.Max(inkTop, baseline - sizePx * TextDecorationMetrics.ViewportUnderlineTightBandAboveBaselineEm);
        var approxBottom = TextDecorationMetrics.ViewportUnderlineApproxInkBottomPx(baseline, sizePx, inkBottom);
        var visibleH = approxBottom - tightTop;
        var maxGap = TextDecorationMetrics.MaxUnderlineGapBelowVisibleLinePx(visibleH);
        var halfH = 0.5f;
        // Stroke top == approxBottom + maxGap + 1px is beyond the allowed ceil(10%) slack (+ test epsilon).
        var centerY = approxBottom + maxGap + halfH + 1f;
        var tooLow = Underline(centerY, halfH);
        var ex = Record.Exception(() =>
            ViewportUnderlinePlacementAssert.FollowsTightVisibleBandRules(in tooLow, baseline, sizePx, inkTop,
                inkBottom));
        Assert.NotNull(ex);
    }
}
