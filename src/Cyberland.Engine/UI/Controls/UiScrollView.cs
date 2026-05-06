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

    private Vector2D<float> _contentOffset;

    /// <summary>Vertical scroll offset in pixels (+Y down content moves up when positive).</summary>
    public Vector2D<float> ContentOffset
    {
        get => _contentOffset;
        set
        {
            if (_contentOffset == value)
                return;
            _contentOffset = value;
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
        var next = new Vector2D<float>(ContentOffset.X, ContentOffset.Y - wheelY * WheelScrollPixels);
        if (next == ContentOffset)
            return;
        ContentOffset = next;
    }

    /// <summary>Clamps <see cref="ContentOffset"/> after measurement so content cannot scroll past its extents.</summary>
    public void ClampContentOffset()
    {
        var inner = ComputedBounds.Deflate(Padding);
        var contentH = Content.MeasuredSize.Y + Content.Margin.Vertical;
        var maxOff = MathF.Max(0f, contentH - inner.Height);
        ContentOffset = new Vector2D<float>(0f, Math.Clamp(ContentOffset.Y, 0f, maxOff));
    }

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        const float eps = 1e-4f;
        var stretchX = AnchorMax.X - AnchorMin.X > eps;
        var stretchY = AnchorMax.Y - AnchorMin.Y > eps;

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

        ClampContentOffset();

        var inner = ComputedBounds.Deflate(Padding);
        var contentW = MathF.Max(0f, inner.Width - Content.Margin.Horizontal);
        var contentH = Content.MeasuredSize.Y + Content.Margin.Vertical;
        Content.Arrange(new UiRect(inner.X - ContentOffset.X, inner.Y - ContentOffset.Y, contentW, contentH));
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
