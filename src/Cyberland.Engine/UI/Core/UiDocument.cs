using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Rendering;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Owns a root <see cref="UiPanel"/> and runs a full layout pass plus optional debug drawing via <see cref="UiRenderContext"/>.
/// </summary>
public sealed class UiDocument
{
    /// <summary>Creates a document whose root stretches to available space.</summary>
    public UiDocument()
    {
        Root = new UiPanel();
        UiLayoutPresets.StretchAll(Root);
    }

    /// <summary>Layout root (typically a <see cref="UiPanel"/>).</summary>
    public UiPanel Root { get; }

    /// <summary>Runs measure then arrange using a viewport-local rect starting at the origin.</summary>
    public void MeasureArrange(Vector2D<float> availableSize) =>
        MeasureArrange(new UiRect(0f, 0f, availableSize.X, availableSize.Y));

    /// <summary>Runs measure then arrange within an absolute document-space rectangle (+Y down).</summary>
    public void MeasureArrange(in UiRect rootAvailableRect)
    {
        var constraints = UiSizeConstraints.Loose(rootAvailableRect.Width, rootAvailableRect.Height);
        Root.Measure(constraints);
        Root.Arrange(rootAvailableRect);
    }

    /// <summary>Assigns <see cref="UiTextBlock.Fonts"/> on every <see cref="UiTextBlock"/> in the tree.</summary>
    public void PropagateFonts(FontLibrary fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        PropagateFontsRecursive(Root, fonts);
    }

    /// <summary>Assigns <see cref="UiTextBlock.Localization"/> on every <see cref="UiTextBlock"/> in the tree.</summary>
    public void PropagateLocalization(LocalizationManager? localization) =>
        PropagateLocalizationRecursive(Root, localization);

    /// <summary>
    /// Submits retained visuals (panels, images, text) using the last <c>MeasureArrange</c> results.
    /// </summary>
    /// <param name="renderer">Destination renderer.</param>
    /// <param name="fonts">Font library for text measurement/submission.</param>
    /// <param name="cache">Glyph cache shared with the frame pipeline.</param>
    /// <param name="space">Coordinate space for submitted draws.</param>
    /// <param name="sortKeyBase">Base HUD sort key for this document.</param>
    /// <param name="rootClip">Document-space clip, normally the arranged root rectangle (e.g. full viewport).</param>
    public void DrawVisuals(IRenderer renderer, FontLibrary fonts, TextGlyphCache cache, CoordinateSpace space,
        float sortKeyBase, in UiRect rootClip)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(cache);
        Root.DrawVisuals(renderer, fonts, cache, space, sortKeyBase, rootClip);
    }

    /// <inheritdoc cref="UiElement.HitTest"/>
    public UiElement? HitTest(Vector2D<float> point, in UiRect documentClip) =>
        Root.HitTest(point, documentClip);

    /// <summary>Walks the arranged tree for debug/visual submission.</summary>
    public void Draw(UiRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Root.Draw(context);
    }

    private static void PropagateFontsRecursive(UiElement element, FontLibrary fonts)
    {
        if (element is UiTextBlock tb)
            tb.Fonts = fonts;

        foreach (var child in element.Children)
            PropagateFontsRecursive(child, fonts);
    }

    private static void PropagateLocalizationRecursive(UiElement element, LocalizationManager? localization)
    {
        if (element is UiTextBlock tb)
            tb.Localization = localization;

        foreach (var child in element.Children)
            PropagateLocalizationRecursive(child, localization);
    }
}
