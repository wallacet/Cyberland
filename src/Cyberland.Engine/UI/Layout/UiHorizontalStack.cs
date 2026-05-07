using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Layout;

/// <summary>
/// Horizontal sequence layout: main axis left-to-right with <see cref="Spacing"/> and cross-axis alignment.
/// </summary>
public class UiHorizontalStack : UiElement
{
    /// <summary>Gap between successive visible children on the main axis.</summary>
    public float Spacing { get; set; }

    /// <summary>How children align against the vertical track.</summary>
    public UiCrossAlignment CrossAlignment { get; set; }

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        var innerMaxW = constraints.MaxWidth - Padding.Horizontal - Margin.Horizontal;
        var innerMaxH = ClampInnerMaxHeightForBand(this,
            constraints.MaxHeight - Padding.Vertical - Margin.Vertical);

        float sumW = 0f;
        float maxCross = 0f;
        var first = true;
        var anyVisible = false;
        var children = Children;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.Visible)
                continue;
            anyVisible = true;

            if (!first)
                sumW += Spacing;
            first = false;

            var childMaxH = MathF.Max(0f, innerMaxH - child.Margin.Vertical);
            var childConstraints = UiSizeConstraints.Loose(float.PositiveInfinity, childMaxH);
            child.Measure(childConstraints);

            sumW += child.MeasuredSize.X + child.Margin.Horizontal;
            var cross = child.MeasuredSize.Y + child.Margin.Vertical;
            maxCross = MathF.Max(maxCross, cross);
        }

        if (CrossAlignment == UiCrossAlignment.Stretch && anyVisible)
            maxCross = MathF.Max(maxCross, innerMaxH);

        var dw = sumW + Padding.Horizontal + Margin.Horizontal;
        var dh = maxCross + Padding.Vertical + Margin.Vertical;
        return constraints.ClampSize(new Vector2D<float>(dw, dh));
    }

    /// <inheritdoc />
    public override void Arrange(in UiRect allocationMarginBoxAbsolute)
    {
        base.Arrange(allocationMarginBoxAbsolute);
        if (!Visible)
            return;

        var inner = ComputedBounds.Deflate(Padding);
        float cursorX = inner.X;
        var first = true;
        var children = Children;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.Visible)
                continue;

            if (!first)
                cursorX += Spacing;
            first = false;

            var mh = child.MeasuredSize.X + child.Margin.Horizontal;
            var mv = child.MeasuredSize.Y + child.Margin.Vertical;

            float boxTop;
            float boxHeight;
            switch (CrossAlignment)
            {
                case UiCrossAlignment.Start:
                    boxTop = inner.Y;
                    boxHeight = mv;
                    break;
                case UiCrossAlignment.End:
                    boxTop = inner.Y + inner.Height - mv;
                    boxHeight = mv;
                    break;
                case UiCrossAlignment.Center:
                    boxTop = inner.Y + (inner.Height - mv) * 0.5f;
                    boxHeight = mv;
                    break;
                case UiCrossAlignment.Stretch:
                default:
                    boxTop = inner.Y;
                    boxHeight = inner.Height;
                    break;
            }

            var slot = new UiRect(cursorX, boxTop, mh, boxHeight);
            child.Arrange(slot);
            cursorX += mh;
        }
    }
}
