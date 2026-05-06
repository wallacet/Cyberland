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
    private bool _fontsAndLocalizationStale = true;

    /// <summary>When true, <see cref="Scene.Systems.UiDocumentFrameSystem"/> runs <see cref="MeasureArrange(in UiRect)"/>.</summary>
    internal bool LayoutDirty { get; private set; } = true;

    /// <summary>
    /// Marks retained-UI visuals stale for incremental layout bookkeeping. Draw submission still runs each frame in
    /// <see cref="Scene.Systems.UiDocumentFrameSystem"/> because renderer queues are reset every render tick.
    /// </summary>
    internal bool VisualDirty { get; private set; } = true;

    /// <summary>Last root rectangle passed to <see cref="MeasureArrange(in UiRect)"/> (for resize detection).</summary>
    internal UiRect LastArrangedRootRect { get; private set; }

    /// <summary>Creates a document whose root stretches to available space.</summary>
    public UiDocument()
    {
        Root = new UiPanel();
        UiLayoutPresets.StretchAll(Root);
        Root.AttachDocument(this);
    }

    /// <summary>Layout root (typically a <see cref="UiPanel"/>).</summary>
    public UiPanel Root { get; }

    /// <summary>
    /// Marks font/localization pointers stale so the next <see cref="Scene.Systems.UiDocumentFrameSystem"/> pass
    /// re-runs <see cref="PropagateFonts"/> / <see cref="PropagateLocalization"/> (e.g. after adding new
    /// <see cref="UiTextBlock"/> nodes at runtime).
    /// </summary>
    public void InvalidateFontsAndLocalization()
    {
        _fontsAndLocalizationStale = true;
        NotifyLayoutDirty();
    }

    internal void NotifyLayoutDirty()
    {
        LayoutDirty = true;
        VisualDirty = true;
    }

    internal void NotifyVisualDirty() => VisualDirty = true;

    internal void ClearFrameDirtyFlagsAfterMeasure(in UiRect rootRect)
    {
        LayoutDirty = false;
        LastArrangedRootRect = rootRect;
    }

    internal void ClearVisualDirty() => VisualDirty = false;

    /// <summary>Engine use: applies propagation once per stale cycle before layout.</summary>
    internal void PrepareFontsAndLocalizationIfNeeded(FontLibrary fonts, LocalizationManager? localization)
    {
        if (!_fontsAndLocalizationStale)
            return;
        _fontsAndLocalizationStale = false;
        PropagateFonts(fonts);
        PropagateLocalization(localization);
    }

    /// <summary>Runs measure then arrange using a viewport-local rect starting at the origin.</summary>
    public void MeasureArrange(Vector2D<float> availableSize) =>
        MeasureArrange(new UiRect(0f, 0f, availableSize.X, availableSize.Y));

    /// <summary>Runs measure then arrange within an absolute document-space rectangle (+Y down).</summary>
    public void MeasureArrange(in UiRect rootAvailableRect)
    {
        var constraints = UiSizeConstraints.Loose(rootAvailableRect.Width, rootAvailableRect.Height);
        Root.Measure(constraints);
        Root.Arrange(rootAvailableRect);
        ClearFrameDirtyFlagsAfterMeasure(rootAvailableRect);
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
