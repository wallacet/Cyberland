using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Controls;

/// <summary>
/// Vertical scroll container with viewport clipping and wheel routing from <see cref="Cyberland.Engine.Scene.Systems.UiDocumentFrameSystem"/>.
/// </summary>
public class UiScrollView : UiElement
{
    /// <summary>Hosted content (stack rows inside this panel).</summary>
    public UiPanel Content { get; }

    private float _verticalOffset;

    /// <summary>Vertical scroll offset in pixels (+Y down content moves up when positive).</summary>
    public float VerticalOffset
    {
        get => _verticalOffset;
        set
        {
            if (_verticalOffset == value)
                return;
            _verticalOffset = value;
            InvalidateLayout();
        }
    }

    /// <summary>Pixels per wheel notch applied along Y.</summary>
    public float WheelScrollPixels { get; set; } = 32f;

    /// <summary>Creates a clipped viewport with an inner vertical stack host.</summary>
    public UiScrollView()
    {
        ClipMode = UiClipMode.IntersectParent;
        Content = new UiPanel();
        // Default collapsed anchors would ignore the arranged slot → 0×0 ComputedBounds; HitTest bails out before
        // descendants while DrawVisuals still lays out children from the slot width/height.
        UiLayoutPresets.StretchAll(Content);
        AddChild(Content);
    }

    /// <summary>
    /// Applies Silk wheel delta Y before the next layout pass (positive delta scrolls content down / reveals lower rows).
    /// </summary>
    public void ApplyWheel(float wheelY)
    {
        var next = VerticalOffset - wheelY * WheelScrollPixels;
        if (next == VerticalOffset)
            return;
        VerticalOffset = next;
    }

    /// <summary>Clamps <see cref="VerticalOffset"/> after measurement so content cannot scroll past its extents.</summary>
    public void ClampVerticalOffset()
    {
        var inner = ComputedBounds.Deflate(Padding);
        var contentH = Content.MeasuredSize.Y + Content.Margin.Vertical;
        var maxOff = MathF.Max(0f, contentH - inner.Height);
        VerticalOffset = Math.Clamp(VerticalOffset, 0f, maxOff);
    }

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        var stretchX = AnchorMax.X - AnchorMin.X > UiLayoutConstants.AxisEpsilon;
        var stretchY = AnchorMax.Y - AnchorMin.Y > UiLayoutConstants.AxisEpsilon;

        var innerMaxW = constraints.MaxWidth - Padding.Horizontal - Margin.Horizontal;

        var cw = MathF.Max(0f, innerMaxW - Content.Margin.Horizontal);
        Content.Measure(UiSizeConstraints.Loose(cw, float.PositiveInfinity));

        var dw = stretchX
            ? constraints.MaxWidth
            : Content.MeasuredSize.X + Padding.Horizontal + Margin.Horizontal + Content.Margin.Horizontal;

        var dh = stretchY
            ? constraints.MaxHeight
            : Content.MeasuredSize.Y + Padding.Vertical + Margin.Vertical + Content.Margin.Vertical;

        return constraints.ClampSize(new Vector2D<float>(dw, dh));
    }

    /// <inheritdoc />
    public override void Arrange(in UiRect allocationMarginBoxAbsolute)
    {
        base.Arrange(allocationMarginBoxAbsolute);
        if (!Visible)
            return;

        ClampVerticalOffset();

        var inner = ComputedBounds.Deflate(Padding);
        var contentW = MathF.Max(0f, inner.Width - Content.Margin.Horizontal);
        var contentH = Content.MeasuredSize.Y + Content.Margin.Vertical;
        Content.Arrange(new UiRect(inner.X, inner.Y - VerticalOffset, contentW, contentH));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Layout positions <see cref="Content"/> with a vertical offset, so measured bounds extend outside the viewport.
    /// Raster clips <see cref="Content"/> to the padded viewport rect (same rule as pointer routing).
    /// </remarks>
    public override void DrawVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float accumulatedSortKey,
        in UiRect inheritedClip)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(cache);
        if (!Visible)
            return;

        var selfClip = ComputedBounds.Intersect(inheritedClip);
        var mine = accumulatedSortKey + SortKey;
        DrawSelfVisuals(renderer, fonts, cache, space, mine, selfClip, inheritedClip);

        var viewportInner = ComputedBounds.Deflate(Padding).Intersect(inheritedClip);
        Content.DrawVisuals(renderer, fonts, cache, space, mine + 1e-4f, viewportInner);
    }
}
