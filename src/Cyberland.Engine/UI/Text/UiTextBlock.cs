using System.Buffers;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Text;

/// <summary>
/// Multi-line wrapped text with optional <see cref="TextRun"/> spans; measures via SixLabors and draws via
/// <see cref="TextRenderer"/> glyph emission.
/// </summary>
public class UiTextBlock : UiElement
{
    private UiTextLayoutEngine? _layout;
    private readonly UiTextLayoutCache _cache = new();

    /// <summary>Whole-block literal copy when <see cref="Runs"/> is null or empty.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Style for literal <see cref="Text"/> mode.</summary>
    public TextStyle DefaultStyle { get; set; }

    /// <summary>Optional styled spans (paragraph breaks use <c>\n\n</c> inside run text).</summary>
    public List<TextRun>? Runs { get; set; }

    /// <summary>Extra vertical gap between paragraphs (after <c>\n\n</c> splits).</summary>
    public float ParagraphSpacing { get; set; }

    /// <summary>Extra pixels added after each wrapped/hard line.</summary>
    public float LineSpacingExtra { get; set; }

    /// <summary>
    /// How each laid-out line is shifted on X inside the content rectangle when the line is narrower than the box.
    /// </summary>
    public UiTextHorizontalAlignment HorizontalAlignment { get; set; }

    /// <summary>
    /// How the measured layout block is shifted on Y when the laid-out text is shorter than the content rectangle
    /// (after <see cref="UiElement.Padding"/>).
    /// </summary>
    public UiTextVerticalAlignment VerticalAlignment { get; set; }

    /// <summary>Whether to binary-search a smaller uniform scale when layout overflows the content box.</summary>
    public UiTextFitMode FitMode { get; set; }

    /// <summary>Axes checked by <see cref="FitMode"/> (<see cref="UiTextFitMode.ShrinkToFit"/>).</summary>
    public UiTextFitTarget FitTarget { get; set; } = UiTextFitTarget.Box;

    /// <summary>Lower pixel bound for quantized shrink search (inclusive).</summary>
    public float MinFitSizePixels { get; set; } = 8f;

    /// <summary>
    /// Fonts used for measurement and drawing; when null, <see cref="MeasureCore"/> falls back to the base
    /// <see cref="UiElement"/> sizing path.
    /// </summary>
    public FontLibrary? Fonts { get; set; }

    /// <summary>Localization source for runs marked as keys during layout measurement.</summary>
    public LocalizationManager? Localization { get; set; }

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        if (Fonts is null)
            return base.MeasureCore(in constraints);

        var innerMaxW = constraints.MaxWidth - Padding.Horizontal - Margin.Horizontal;
        var innerMaxH = constraints.MaxHeight - Padding.Vertical - Margin.Vertical;

        if (FitMode == UiTextFitMode.ShrinkToFit)
            _layout = MeasureShrinkToFit(innerMaxW, innerMaxH);
        else
        {
            var fp = UiTextLayoutEngine.ComputeFingerprint(
                Text,
                DefaultStyle,
                Runs,
                innerMaxW,
                ParagraphSpacing,
                LineSpacingExtra);

            if (!_cache.TryGet(fp, out var layout))
            {
                IReadOnlyList<TextRun>? runs = Runs is { Count: > 0 } ? Runs : null;
                layout = UiTextLayoutEngine.Build(
                    Fonts,
                    Localization,
                    runs is null ? Text : null,
                    DefaultStyle,
                    runs,
                    innerMaxW,
                    ParagraphSpacing,
                    LineSpacingExtra);
                _cache.Store(fp, layout);
            }

            _layout = layout!;
        }

        const float eps = 1e-4f;
        var stretchX = AnchorMax.X - AnchorMin.X > eps;
        var stretchY = AnchorMax.Y - AnchorMin.Y > eps;
        // UiLayoutPresets.TopStretch: stretch X to parent slot, fixed pixel height on Y via SizeDelta.Y.
        // Arrange resolves border height to SizeDelta.Y, but intrinsic text height can exceed that band; reporting
        // intrinsic MeasuredSize.Y makes vertical stacks advance past the real border box so siblings (buttons, etc.)
        // sit too low and no longer match raster/hit targets.
        var topStretchBand = stretchX && !stretchY && MathF.Abs(SizeDelta.X) <= eps && SizeDelta.Y > eps;

        // Horizontal stacks (and similar) measure main-axis children with unbounded maxima. Stretch-all text would
        // otherwise report infinite measured width/height; Arrange then multiplies 0*Infinity when resolving anchors
        // and poisons ComputedBounds with NaNs. Use intrinsic layout sizes until a finite cap exists.
        var intrinsicW = _layout.TotalWidth + Padding.Horizontal + Margin.Horizontal;
        var intrinsicH = _layout.TotalHeight + Padding.Vertical + Margin.Vertical;

        var dw = stretchX
            ? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : intrinsicW)
            : intrinsicW;

        float dh;
        if (stretchY)
            dh = float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight : intrinsicH;
        else if (topStretchBand)
            // Full border height (SizeDelta is the band rect). Outer margin is applied once by parents (MeasuredSize.Y + Margin.Vertical).
            dh = SizeDelta.Y;
        else
            dh = intrinsicH;

        // Match Arrange: collapsed anchors still use SizeDelta width/height; floor measured size so stacked siblings
        // (e.g. gather-card detail + buttons) do not overlap when intrinsic text is narrower than the slot.
        // Cap the height floor so large TopLeftFixed boxes (scroll hosts) still report intrinsic text height.
        const float collapsedHeightFloorMaxPx = 256f;
        if (!stretchX && SizeDelta.X > eps)
            dw = MathF.Max(dw, SizeDelta.X + Margin.Horizontal);
        if (!stretchY && SizeDelta.Y > eps && !topStretchBand && SizeDelta.Y <= collapsedHeightFloorMaxPx)
            dh = MathF.Max(dh, SizeDelta.Y + Margin.Vertical);

        return constraints.ClampSize(new Vector2D<float>(dw, dh));
    }

    private UiTextLayoutEngine MeasureShrinkToFit(float innerMaxW, float innerMaxH)
    {
        var reference = NominalReferencePixels();
        var qLo = FontLibrary.QuantizeEmSizePixels(MinFitSizePixels);
        var qHi = FontLibrary.QuantizeEmSizePixels(reference);
        if (qLo > qHi)
            (qLo, qHi) = (qHi, qLo);

        bool FitsQuant(int q)
        {
            var px = FontLibrary.EmQuantToPixels(q);
            var factor = px / reference;
            var layout = BuildLayoutForFactor(factor, innerMaxW);
            return LayoutFits(layout, innerMaxW, innerMaxH, FitTarget);
        }

        UiTextLayoutEngine BuildAtQuant(int q)
        {
            var px = FontLibrary.EmQuantToPixels(q);
            var factor = px / reference;
            return BuildLayoutForFactor(factor, innerMaxW);
        }

        if (FitsQuant(qHi))
            return BuildAtQuant(qHi);

        if (!FitsQuant(qLo))
            return BuildAtQuant(qLo);

        var lo = qLo;
        var hi = qHi;
        var guard = 0;
        while (lo < hi && guard++ < 24)
        {
            var mid = (lo + hi + 1) / 2;
            if (FitsQuant(mid))
                lo = mid;
            else
                hi = mid - 1;
        }

        return BuildAtQuant(lo);
    }

    private float NominalReferencePixels()
    {
        var m = DefaultStyle.SizePixels;
        if (Runs is { Count: > 0 })
        {
            foreach (var r in Runs)
                m = MathF.Max(m, r.Style.SizePixels);
        }

        return MathF.Max(m, 1f / 256f);
    }

    private static TextStyle ScaleStyle(in TextStyle style, float factor) =>
        new(
            style.FontFamilyId,
            MathF.Max(1f / 256f, style.SizePixels * factor),
            style.Color,
            style.Bold,
            style.Italic,
            style.Underline,
            style.Strikethrough);

    private UiTextLayoutEngine BuildLayoutForFactor(float factor, float maxContentW)
    {
        var ds = ScaleStyle(DefaultStyle, factor);
        List<TextRun>? rrList = null;
        if (Runs is { Count: > 0 })
        {
            rrList = new List<TextRun>(Runs.Count);
            foreach (var r in Runs)
                rrList.Add(new TextRun(r.Content, ScaleStyle(r.Style, factor), r.IsLocalizationKey));
        }

        IReadOnlyList<TextRun>? rr = rrList;

        return UiTextLayoutEngine.Build(
            Fonts!,
            Localization,
            rr is null ? Text : null,
            ds,
            rr,
            maxContentW,
            ParagraphSpacing,
            LineSpacingExtra);
    }

    private static bool LayoutFits(
        UiTextLayoutEngine layout,
        float innerMaxW,
        float innerMaxH,
        UiTextFitTarget target)
    {
        const float eps = 0.75f;
        if (target == UiTextFitTarget.WidthOnly)
            return layout.MaxLineAdvance <= innerMaxW + eps;

        return layout.MaxLineAdvance <= innerMaxW + eps && layout.TotalHeight <= innerMaxH + eps;
    }

    /// <summary>Drops cached layout lines (call after mutating fonts if face metrics change).</summary>
    public void InvalidateLayout() => _cache.Clear();

    /// <summary>
    /// Submits glyph quads for the last measured layout inside <see cref="UiElement.ComputedBounds"/> content rect.
    /// </summary>
    public void DrawGlyphs(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey = 450f) =>
        DrawGlyphsCore(renderer, fonts, cache, space, sortKey, clipGlyphs: false, default);

    /// <summary>Submits glyphs with viewport scissor (retained UI document path).</summary>
    public void DrawGlyphs(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey,
        in UiRect viewportClip) =>
        DrawGlyphsCore(renderer, fonts, cache, space, sortKey, clipGlyphs: true, viewportClip);

    private void DrawGlyphsCore(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey,
        bool clipGlyphs,
        UiRect viewportClip)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(cache);

        if (_layout is null || _layout.Lines.Count == 0)
            return;

        if (clipGlyphs && (viewportClip.Width <= 1e-4f || viewportClip.Height <= 1e-4f))
            return;

        var inner = ComputedBounds.Deflate(Padding);
        var applyVpClip = clipGlyphs && space is CoordinateSpace.ViewportSpace or CoordinateSpace.SwapchainSpace;

        // TotalHeight can include slack vs tight line boxes; center using ink extent so single-line captions sit
        // visually centered in buttons (IdleGold nav / gather actions).
        var extentH = LayoutInkExtentHeight(fonts, _layout);
        var blockShiftY = VerticalAlignment switch
        {
            UiTextVerticalAlignment.Center => MathF.Max(0f, (inner.Height - extentH) * 0.5f),
            UiTextVerticalAlignment.End => MathF.Max(0f, inner.Height - extentH),
            _ => 0f
        };

        foreach (var line in _layout.Lines)
        {
            var lh = line.MaxLineHeight(fonts);
            // Line box top (LineTop) + distance to baseline: use the same reference sample as MeasureLineHeight
            // so single-line text is not pushed low inside the line box (fixed 0.82*lh was consistently bottom-heavy).
            var baselineFromLineTop = UiTextMeasurer.TryMinReferenceBoundsTopForLine(fonts, line, out var minTop)
                ? -minTop
                : lh * 0.82f;
            var baselineY = inner.Y + blockShiftY + line.LineTop + baselineFromLineTop;

            var lineShift = HorizontalAlignment switch
            {
                UiTextHorizontalAlignment.Center => MathF.Max(0f, (inner.Width - LineContentAdvance(fonts, line)) * 0.5f),
                UiTextHorizontalAlignment.End => MathF.Max(0f, inner.Width - LineContentAdvance(fonts, line)),
                _ => 0f
            };

            foreach (var seg in line.Segments)
            {
                var baselineLeft = new Vector2D<float>(inner.X + lineShift + seg.PenStart, baselineY);
                DrawRun(renderer, fonts, cache, seg.Text, seg.Style, baselineLeft, sortKey, space, applyVpClip,
                    viewportClip);
            }
        }
    }

    private static float LayoutInkExtentHeight(FontLibrary fonts, UiTextLayoutEngine layout)
    {
        float bottom = 0f;
        foreach (var line in layout.Lines)
            bottom = MathF.Max(bottom, line.LineTop + line.MaxLineHeight(fonts));

        return bottom;
    }

    private static float LineContentAdvance(FontLibrary fonts, UiTextLayoutLine line)
    {
        float maxEnd = 0f;
        foreach (var seg in line.Segments)
        {
            var w = UiTextMeasurer.MeasureAdvanceWidth(fonts, in seg.Style, seg.Text.AsSpan());
            maxEnd = MathF.Max(maxEnd, seg.PenStart + w);
        }

        return maxEnd;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Layout bounds from measure are a tight text box; glyph quads extend past that for ascenders,
    /// descenders, and side bearings. The GPU scissor used for viewport UI is therefore inflated vertically
    /// (capped by <paramref name="inheritedClip"/>) so ink is not culled while scroll ports still clip.
    /// </remarks>
    protected override void DrawSelfVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey,
        in UiRect viewportClip,
        in UiRect inheritedClip)
    {
        var clip = GlyphViewportScissorClip(in viewportClip, in inheritedClip, fonts, _layout, DefaultStyle);
        DrawGlyphs(renderer, fonts, cache, space, sortKey, clip);
    }

    private static UiRect GlyphViewportScissorClip(
        in UiRect layoutClip,
        in UiRect inheritedClip,
        FontLibrary fonts,
        UiTextLayoutEngine? layout,
        TextStyle defaultStyle)
    {
        float slack;
        if (layout is { Lines.Count: > 0 })
        {
            var m = 0f;
            foreach (var line in layout.Lines)
                m = MathF.Max(m, line.MaxLineHeight(fonts));
            slack = MathF.Min(28f, MathF.Max(2f, m * 0.38f));
        }
        else
            slack = MathF.Min(24f, MathF.Max(2f, defaultStyle.SizePixels * 0.5f));

        var hPad = MathF.Min(14f, MathF.Max(1f, slack * 0.35f));
        return InflateRect(layoutClip, hPad, slack, hPad, slack).Intersect(inheritedClip);
    }

    private static UiRect InflateRect(in UiRect r, float left, float top, float right, float bottom) =>
        new(r.X - left, r.Y - top, r.Width + left + right, r.Height + top + bottom);

    private static void DrawRun(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        string text,
        TextStyle style,
        Vector2D<float> baselineLeft,
        float sortKey,
        CoordinateSpace space,
        bool applyVpClip,
        UiRect viewportClip)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var pool = ArrayPool<SpriteDrawRequest>.Shared;
        var buf = pool.Rent(text.Length);
        try
        {
            var dest = buf.AsSpan(0, text.Length);
            var n = TextRenderer.FillGlyphRunSprites(
                renderer,
                fonts,
                cache,
                text,
                in style,
                baselineLeft,
                0f,
                sortKey,
                dest,
                out var penAfter,
                space,
                applyVpClip,
                viewportClip);

            if (n > 0)
                renderer.SubmitSprites(dest[..n]);

            TextRenderer.SubmitTextDecorations(
                renderer,
                in style,
                baselineLeft,
                0f,
                penAfter,
                sortKey,
                renderer.WhiteTextureId,
                renderer.DefaultNormalTextureId,
                space,
                applyVpClip,
                viewportClip);
        }
        finally
        {
            pool.Return(buf);
        }
    }
}
