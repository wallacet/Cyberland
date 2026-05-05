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

        var dw = stretchX
            ? constraints.MaxWidth
            : _layout.TotalWidth + Padding.Horizontal + Margin.Horizontal;

        var dh = stretchY
            ? constraints.MaxHeight
            : _layout.TotalHeight + Padding.Vertical + Margin.Vertical;

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
        float sortKey = 450f)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(cache);

        if (_layout is null || _layout.Lines.Count == 0)
            return;

        var inner = ComputedBounds.Deflate(Padding);

        foreach (var line in _layout.Lines)
        {
            var lh = line.MaxLineHeight(fonts);
            var baselineY = inner.Y + line.LineTop + lh * 0.82f;

            foreach (var seg in line.Segments)
            {
                var baselineLeft = new Vector2D<float>(inner.X + seg.PenStart, baselineY);
                DrawRun(renderer, fonts, cache, seg.Text, seg.Style, baselineLeft, sortKey, space);
            }
        }
    }

    /// <inheritdoc />
    protected override void DrawSelfVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey) =>
        DrawGlyphs(renderer, fonts, cache, space, sortKey);

    private static void DrawRun(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        string text,
        TextStyle style,
        Vector2D<float> baselineLeft,
        float sortKey,
        CoordinateSpace space)
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
                space);

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
                space);
        }
        finally
        {
            pool.Return(buf);
        }
    }
}
