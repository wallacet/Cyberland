using System.Collections.Generic;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Default vertical stack container: stacks visible children top-to-bottom with optional <see cref="Spacing"/>.
/// </summary>
public class UiPanel : UiElement
{
    private Vector4D<float> _backgroundColor;
    private TextureId _backgroundTextureId = TextureId.MaxValue;

    /// <summary>
    /// Straight RGBA tint for an axis-aligned background quad; alpha 0 skips submission (see <see cref="BackgroundTextureId"/>).
    /// </summary>
    public Vector4D<float> BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor == value)
                return;
            _backgroundColor = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// When not <see cref="TextureId.MaxValue"/>, used as the albedo for the background quad; otherwise <see cref="IRenderer.WhiteTextureId"/>.
    /// </summary>
    public TextureId BackgroundTextureId
    {
        get => _backgroundTextureId;
        set
        {
            if (_backgroundTextureId == value)
                return;
            _backgroundTextureId = value;
            InvalidateVisual();
        }
    }

    private float _spacing;

    /// <summary>Extra vertical gap between successive visible children.</summary>
    public float Spacing
    {
        get => _spacing;
        set
        {
            if (_spacing == value)
                return;
            _spacing = value;
            InvalidateLayout();
        }
    }

    /// <inheritdoc />
    protected override void DrawSelfVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey,
        in UiRect viewportClip,
        in UiRect inheritedClip)
    {
        _ = fonts;
        _ = cache;
        _ = inheritedClip;
        if (BackgroundColor.W <= 1e-4f)
            return;

        if (viewportClip.Width <= 1e-4f || viewportClip.Height <= 1e-4f)
            return;

        var tex = BackgroundTextureId == TextureId.MaxValue ? renderer.WhiteTextureId : BackgroundTextureId;
        UiVisualSubmission.SubmitFilledQuad(renderer, ComputedBounds, space, tex, BackgroundColor, sortKey, viewportClip);
    }

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        var innerMaxW = constraints.MaxWidth - Padding.Horizontal - Margin.Horizontal;
        var innerMaxH = ClampInnerMaxHeightForBand(this,
            constraints.MaxHeight - Padding.Vertical - Margin.Vertical);

        const float eps = 1e-4f;
        const float collapsedHeightFloorMaxPx = 256f;
        var stretchX = AnchorMax.X - AnchorMin.X > eps;
        var stretchY = AnchorMax.Y - AnchorMin.Y > eps;
        var topStretchBand = stretchX && !stretchY && MathF.Abs(SizeDelta.X) <= eps && SizeDelta.Y > eps;

        // Fixed-height tiles must not give StretchAll labels the parent's slack (e.g. 44px chrome band) when
        // SizeDelta.Y is 38–40px — measured height would exceed Arrange, pinning captions to the top of the slot.
        var childVertBudget = innerMaxH;
        if (!stretchY && SizeDelta.Y > eps && SizeDelta.Y <= collapsedHeightFloorMaxPx)
        {
            var contentH = MathF.Max(0f, SizeDelta.Y - Padding.Vertical);
            childVertBudget = MathF.Min(innerMaxH, contentH);
        }

        // Giving every child the full innerMaxH makes each StretchAll descendant measure to the full viewport;
        // summed heights then exceed the parent and push siblings off-screen. Measure fixed-height siblings first,
        // then split the remaining vertical budget across children that stretch on Y (see UiLayoutPresets.StretchAll).
        var stretchers = new List<UiElement>();
        float consumed = 0f;
        var anyFixedMeasured = false;

        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            if (StretchesVertically(child))
            {
                stretchers.Add(child);
                continue;
            }

            if (anyFixedMeasured)
                consumed += Spacing;
            anyFixedMeasured = true;

            var childMaxW = MathF.Max(0f, innerMaxW - child.Margin.Horizontal);
            child.Measure(UiSizeConstraints.Loose(childMaxW, childVertBudget));
            consumed += child.MeasuredSize.Y + child.Margin.Vertical;
        }

        if (stretchers.Count > 0)
        {
            var gaps = (anyFixedMeasured ? 1 : 0) + Math.Max(0, stretchers.Count - 1);
            var remaining = childVertBudget - consumed - gaps * Spacing;
            if (!float.IsPositiveInfinity(childVertBudget))
                remaining = MathF.Max(0f, remaining);

            var perStretch = float.IsPositiveInfinity(remaining)
                ? float.PositiveInfinity
                : remaining / stretchers.Count;

            foreach (var child in stretchers)
            {
                var childMaxW = MathF.Max(0f, innerMaxW - child.Margin.Horizontal);
                child.Measure(UiSizeConstraints.Loose(childMaxW, perStretch));
            }
        }

        float maxChildW = 0f;
        float sumH = 0f;
        var firstVisible = true;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            if (!firstVisible)
                sumH += Spacing;
            firstVisible = false;

            maxChildW = MathF.Max(maxChildW, child.MeasuredSize.X + child.Margin.Horizontal);
            sumH += child.MeasuredSize.Y + child.Margin.Vertical;
        }

        // Leaf panels (chrome rows, colored strips) keep intrinsic stretch/collapsed sizing from anchors.
        if (firstVisible)
            return base.MeasureCore(in constraints);

        var dw = maxChildW + Padding.Horizontal + Margin.Horizontal;
        var dh = sumH + Padding.Vertical + Margin.Vertical;
        // UiLayoutPresets.TopStretch fixes Arrange border height to SizeDelta.Y; intrinsic sumH can be shorter, which
        // made vertical stacks advance too little and the next sibling overlapped this panel's background (IdleGold cards).
        if (topStretchBand)
            dh = SizeDelta.Y + Margin.Vertical;

        // TopLeftFixed tiles (buttons, radio pills) must reserve SizeDelta on collapsed axes so horizontal-stack
        // cursor advances match Arrange bounds — otherwise siblings paint on top of each other (IdleGold chrome).
        // Cap the height floor: large SizeDelta.Y is often a layout viewport; measured height must stay intrinsic there.
        if (!stretchX && SizeDelta.X > eps)
            dw = MathF.Max(dw, SizeDelta.X + Margin.Horizontal);
        if (!stretchY && SizeDelta.Y > eps && !topStretchBand && SizeDelta.Y <= collapsedHeightFloorMaxPx)
            dh = MathF.Max(dh, SizeDelta.Y + Margin.Vertical);

        return constraints.ClampSize(new Vector2D<float>(dw, dh));
    }

    private static bool StretchesVertically(UiElement child)
    {
        const float eps = 1e-4f;
        return child.AnchorMax.Y - child.AnchorMin.Y > eps;
    }

    /// <inheritdoc />
    public override void Arrange(in UiRect allocationMarginBoxAbsolute)
    {
        base.Arrange(allocationMarginBoxAbsolute);
        if (!Visible)
            return;

        var inner = ComputedBounds.Deflate(Padding);
        float y = inner.Y;
        var first = true;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            if (!first)
                y += Spacing;
            first = false;

            var mh = child.MeasuredSize.Y + child.Margin.Vertical;
            var slot = new UiRect(inner.X, y, inner.Width, mh);
            child.Arrange(slot);
            y += mh;
        }
    }
}
