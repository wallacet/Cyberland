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
    /// <summary>
    /// Straight RGBA tint for an axis-aligned background quad; alpha 0 skips submission (see <see cref="BackgroundTextureId"/>).
    /// </summary>
    public Vector4D<float> BackgroundColor { get; set; }

    /// <summary>
    /// When not <see cref="TextureId.MaxValue"/>, used as the albedo for the background quad; otherwise <see cref="IRenderer.WhiteTextureId"/>.
    /// </summary>
    public TextureId BackgroundTextureId { get; set; } = TextureId.MaxValue;

    /// <summary>Extra vertical gap between successive visible children.</summary>
    public float Spacing { get; set; }

    /// <inheritdoc />
    protected override void DrawSelfVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey)
    {
        _ = fonts;
        _ = cache;
        if (BackgroundColor.W <= 1e-4f)
            return;

        var tex = BackgroundTextureId == TextureId.MaxValue ? renderer.WhiteTextureId : BackgroundTextureId;
        UiVisualSubmission.SubmitFilledQuad(renderer, ComputedBounds, space, tex, BackgroundColor, sortKey);
    }

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        var innerMaxW = constraints.MaxWidth - Padding.Horizontal - Margin.Horizontal;
        var innerMaxH = constraints.MaxHeight - Padding.Vertical - Margin.Vertical;

        float maxChildW = 0f;
        float sumH = 0f;
        var first = true;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            if (!first)
                sumH += Spacing;
            first = false;

            var childMaxW = MathF.Max(0f, innerMaxW - child.Margin.Horizontal);
            var childConstraints = UiSizeConstraints.Loose(childMaxW, innerMaxH);
            child.Measure(childConstraints);

            maxChildW = MathF.Max(maxChildW, child.MeasuredSize.X + child.Margin.Horizontal);
            sumH += child.MeasuredSize.Y + child.Margin.Vertical;
        }

        // Leaf panels (chrome rows, colored strips) keep intrinsic stretch/collapsed sizing from anchors.
        if (first)
            return base.MeasureCore(in constraints);

        var dw = maxChildW + Padding.Horizontal + Margin.Horizontal;
        var dh = sumH + Padding.Vertical + Margin.Vertical;
        return constraints.ClampSize(new Vector2D<float>(dw, dh));
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
