using Cyberland.Engine.Localization;
using Cyberland.Engine.Diagnostics;
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
    private const int MaxShrinkToFitIterations = 24;
    private sealed class CachedDrawRun
    {
        public string Text = string.Empty;
        public TextStyle Style;
        public Vector2D<float> BaselineLeft;
        public float SortKey;
        public CoordinateSpace Space;
        public bool ApplyViewportClip;
        public UiRect ViewportClip;
        public TextGlyphDrawRequest[] Glyphs = Array.Empty<TextGlyphDrawRequest>();
        public int GlyphCount;
        public float PenAfter;
    }

    private UiTextLayoutEngine? _layout;
    private readonly UiTextLayoutCache _cache = new();
    private TextGlyphDrawRequest[]? _drawGlyphScratch;
    private readonly List<CachedDrawRun> _drawRunCache = new();
    private bool _drawRunReplayValid;
    private CoordinateSpace _drawRunReplaySpace;
    private float _drawRunReplaySortKey;
    private bool _drawRunReplayViewportClipEnabled;
    private UiRect _drawRunReplayViewportClip;
    private UiRect _drawRunReplayInnerBounds;
    private long _drawRunReplayGlyphContentVersion = -1;
    private string _text = string.Empty;
    private TextStyle _defaultStyle;
    private List<TextRun>? _runs;
    private float _paragraphSpacing;
    private float _lineSpacingExtra;
    private UiTextHorizontalAlignment _horizontalAlignment;
    private UiTextVerticalAlignment _verticalAlignment;
    private UiTextFitMode _fitMode;
    private UiTextFitTarget _fitTarget = UiTextFitTarget.Box;
    private float _minFitSizePixels = 8f;
    private FontLibrary? _fonts;
    private LocalizationManager? _localization;

    private int _shrinkFitCacheKey;
    private UiTextLayoutEngine? _shrinkFitCachedLayout;

    /// <summary>Whole-block literal copy when <see cref="Runs"/> is null or empty.</summary>
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_text, value, StringComparison.Ordinal))
                return;
            _text = value;
            // Must clear draw-run replay (via this InvalidateLayout), not only base — same bounds can replay stale glyphs.
            InvalidateLayout();
        }
    }

    /// <summary>Style for literal <see cref="Text"/> mode.</summary>
    public TextStyle DefaultStyle
    {
        get => _defaultStyle;
        set
        {
            if (_defaultStyle.Equals(value))
                return;
            _defaultStyle = value;
            InvalidateLayout();
        }
    }

    /// <summary>Optional styled spans (paragraph breaks use <c>\n\n</c> inside run text).</summary>
    public List<TextRun>? Runs
    {
        get => _runs;
        set
        {
            if (ReferenceEquals(_runs, value))
                return;
            _runs = value;
            InvalidateLayout();
        }
    }

    /// <summary>Extra vertical gap between paragraphs (after <c>\n\n</c> splits).</summary>
    public float ParagraphSpacing
    {
        get => _paragraphSpacing;
        set
        {
            if (MathF.Abs(_paragraphSpacing - value) < 1e-4f)
                return;
            _paragraphSpacing = value;
            InvalidateLayout();
        }
    }

    /// <summary>Extra pixels added after each wrapped/hard line.</summary>
    public float LineSpacingExtra
    {
        get => _lineSpacingExtra;
        set
        {
            if (MathF.Abs(_lineSpacingExtra - value) < 1e-4f)
                return;
            _lineSpacingExtra = value;
            InvalidateLayout();
        }
    }

    /// <summary>
    /// How each laid-out line is shifted on X inside the content rectangle when the line is narrower than the box.
    /// </summary>
    public UiTextHorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            if (_horizontalAlignment == value)
                return;
            _horizontalAlignment = value;
            InvalidateLayout();
        }
    }

    /// <summary>
    /// How the measured layout block is shifted on Y when the laid-out text is shorter than the content rectangle
    /// (after <see cref="UiElement.Padding"/>).
    /// </summary>
    public UiTextVerticalAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set
        {
            if (_verticalAlignment == value)
                return;
            _verticalAlignment = value;
            InvalidateLayout();
        }
    }

    /// <summary>Whether to binary-search a smaller uniform scale when layout overflows the content box.</summary>
    public UiTextFitMode FitMode
    {
        get => _fitMode;
        set
        {
            if (_fitMode == value)
                return;
            _fitMode = value;
            InvalidateLayout();
        }
    }

    /// <summary>Axes checked by <see cref="FitMode"/> (<see cref="UiTextFitMode.ShrinkToFit"/>).</summary>
    public UiTextFitTarget FitTarget
    {
        get => _fitTarget;
        set
        {
            if (_fitTarget == value)
                return;
            _fitTarget = value;
            InvalidateLayout();
        }
    }

    /// <summary>Lower pixel bound for quantized shrink search (inclusive).</summary>
    public float MinFitSizePixels
    {
        get => _minFitSizePixels;
        set
        {
            if (MathF.Abs(_minFitSizePixels - value) < 1e-4f)
                return;
            _minFitSizePixels = value;
            InvalidateLayout();
        }
    }

    /// <summary>
    /// Fonts used for measurement and drawing; when null, <see cref="MeasureCore"/> falls back to the base
    /// <see cref="UiElement"/> sizing path.
    /// </summary>
    public FontLibrary? Fonts
    {
        get => _fonts;
        set
        {
            if (ReferenceEquals(_fonts, value))
                return;
            _fonts = value;
            InvalidateLayout();
        }
    }

    /// <summary>Localization source for runs marked as keys during layout measurement.</summary>
    public LocalizationManager? Localization
    {
        get => _localization;
        set
        {
            if (ReferenceEquals(_localization, value))
                return;
            _localization = value;
            InvalidateLayout();
        }
    }

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        if (Fonts is null)
            return base.MeasureCore(in constraints);

        var innerMaxW = constraints.MaxWidth - Padding.Horizontal - Margin.Horizontal;
        var innerMaxH = constraints.MaxHeight - Padding.Vertical - Margin.Vertical;

        if (FitMode == UiTextFitMode.ShrinkToFit)
            _layout = MeasureShrinkToFitCached(innerMaxW, innerMaxH);
        else
        {
            var fp = UiTextLayoutEngine.ComputeFingerprint(
                Text,
                DefaultStyle,
                Runs,
                Localization,
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
        if (!stretchX && SizeDelta.X > eps)
            dw = MathF.Max(dw, SizeDelta.X + Margin.Horizontal);
        if (!stretchY && SizeDelta.Y > eps && !topStretchBand && SizeDelta.Y <= UiLayoutConstants.CollapsedHeightFloorMaxPx)
            dh = MathF.Max(dh, SizeDelta.Y + Margin.Vertical);

        return constraints.ClampSize(new Vector2D<float>(dw, dh));
    }

    private UiTextLayoutEngine MeasureShrinkToFitCached(float innerMaxW, float innerMaxH)
    {
        var key = BuildShrinkFitCacheKey(innerMaxW, innerMaxH);
        if (key == _shrinkFitCacheKey && _shrinkFitCachedLayout is not null)
            return _shrinkFitCachedLayout;

        var layout = MeasureShrinkToFit(innerMaxW, innerMaxH);
        _shrinkFitCacheKey = key;
        _shrinkFitCachedLayout = layout;
        return layout;
    }

    private int BuildShrinkFitCacheKey(float innerMaxW, float innerMaxH)
    {
        var h = new HashCode();
        h.Add(UiTextLayoutEngine.ComputeFingerprint(
            Text,
            DefaultStyle,
            Runs,
            Localization,
            innerMaxW,
            ParagraphSpacing,
            LineSpacingExtra));
        h.Add((int)MathF.Round(innerMaxH * 4f));
        h.Add((int)FitTarget);
        h.Add(FontLibrary.QuantizeEmSizePixels(MinFitSizePixels));
        return h.ToHashCode();
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
        while (lo < hi && guard++ < MaxShrinkToFitIterations)
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

    /// <summary>Drops cached layout lines and marks the owning <see cref="UiDocument"/> dirty.</summary>
    public new void InvalidateLayout()
    {
        _cache.Clear();
        _shrinkFitCacheKey = 0;
        _shrinkFitCachedLayout = null;
        _drawRunReplayValid = false;
        _drawRunCache.Clear();
        base.InvalidateLayout();
    }

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
#if DEBUG
        using var drawScope = FrameProfilerScope.Enter("ui.textblock.draw_total");
#endif
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(cache);

        if (_layout is null || _layout.Lines.Count == 0)
            return;

        if (clipGlyphs && (viewportClip.Width <= 1e-4f || viewportClip.Height <= 1e-4f))
            return;

        var inner = ComputedBounds.Deflate(Padding);
        var applyVpClip = clipGlyphs && space is CoordinateSpace.ViewportSpace or CoordinateSpace.SwapchainSpace;
        if (_drawRunReplayValid &&
            _drawRunReplaySpace == space &&
            _drawRunReplaySortKey == sortKey &&
            _drawRunReplayViewportClipEnabled == applyVpClip &&
            _drawRunReplayViewportClip.Equals(viewportClip) &&
            _drawRunReplayInnerBounds.Equals(inner) &&
            _drawRunReplayGlyphContentVersion == TextGlyphCache.ContentVersion)
        {
            ReplayCachedRuns(renderer, fonts);
            return;
        }
        _drawRunCache.Clear();

        // TotalHeight can include slack vs tight line boxes; center using ink extent so single-line captions sit
        // visually centered in buttons (IdleGold nav / gather actions).
        var extentH = LayoutInkExtentHeight(fonts, _layout);
        var blockShiftY = VerticalAlignment switch
        {
            UiTextVerticalAlignment.Center => MathF.Max(0f, (inner.Height - extentH) * 0.5f),
            UiTextVerticalAlignment.End => MathF.Max(0f, inner.Height - extentH),
            UiTextVerticalAlignment.CenterInk =>
                TryGetLayoutInkMinMax(fonts, _layout, out var inkMid0, out var inkMid1)
                    ? MathF.Max(0f, inner.Height * 0.5f - (inkMid0 + inkMid1) * 0.5f)
                    : MathF.Max(0f, (inner.Height - extentH) * 0.5f),
            UiTextVerticalAlignment.EndInk =>
                TryGetLayoutInkMinMax(fonts, _layout, out _, out var inkEndMax)
                    ? MathF.Max(0f, inner.Height - inkEndMax)
                    : MathF.Max(0f, inner.Height - extentH),
            _ => 0f
        };

        foreach (var line in _layout.Lines)
        {
            var baselineFromLineTop = BaselineFromLineTopForDraw(fonts, line);
            var baselineY = inner.Y + blockShiftY + line.LineTop + baselineFromLineTop;

            var lineShift = HorizontalAlignment switch
            {
                UiTextHorizontalAlignment.Center =>
                    MathF.Max(0f, (inner.Width - LineContentAdvance(line)) * 0.5f),
                UiTextHorizontalAlignment.End => MathF.Max(0f, inner.Width - LineContentAdvance(line)),
                _ => 0f
            };

            foreach (var seg in line.Segments)
            {
                var baselineLeft = new Vector2D<float>(inner.X + lineShift + seg.PenStart, baselineY);
                DrawRun(renderer, fonts, cache, seg.Text, seg.Style, baselineLeft, sortKey, space, applyVpClip,
                    viewportClip);
            }
        }

        _drawRunReplayValid = true;
        _drawRunReplaySpace = space;
        _drawRunReplaySortKey = sortKey;
        _drawRunReplayViewportClipEnabled = applyVpClip;
        _drawRunReplayViewportClip = viewportClip;
        _drawRunReplayInnerBounds = inner;
        _drawRunReplayGlyphContentVersion = TextGlyphCache.ContentVersion;
    }

    private void ReplayCachedRuns(IRenderer renderer, FontLibrary fonts)
    {
        for (var i = 0; i < _drawRunCache.Count; i++)
        {
            var cached = _drawRunCache[i];
            if (cached.GlyphCount > 0)
                renderer.SubmitTextGlyphs(cached.Glyphs.AsSpan(0, cached.GlyphCount));

            var decorBaseline = cached.BaselineLeft;
            ReadOnlySpan<TextGlyphDrawRequest> inkReplay = default;
            if (cached.GlyphCount > 0 && cached.Glyphs is not null)
            {
                inkReplay = cached.Glyphs.AsSpan(0, cached.GlyphCount);
                if (cached.Space is not CoordinateSpace.ViewportSpace and not CoordinateSpace.SwapchainSpace)
                {
                    var y = TextRenderer.RecoverBaselineYFromGlyph(in inkReplay[0]);
                    decorBaseline = new Vector2D<float>(cached.BaselineLeft.X, y);
                }
            }

            TextRenderer.SubmitTextDecorations(
                renderer,
                in cached.Style,
                decorBaseline,
                0f,
                cached.PenAfter,
                cached.SortKey,
                renderer.WhiteTextureId,
                renderer.DefaultNormalTextureId,
                cached.Space,
                cached.ApplyViewportClip,
                cached.ViewportClip,
                fonts,
                inkReplay);
        }
    }

    private static float LayoutInkExtentHeight(FontLibrary fonts, UiTextLayoutEngine layout)
    {
        float bottom = 0f;
        foreach (var line in layout.Lines)
            bottom = MathF.Max(bottom, line.LineTop + line.MaxLineHeight(fonts));

        return bottom;
    }

    /// <summary>
    /// Baseline distance from line top (+Y down), matching layout flush and draw fallback rules.
    /// </summary>
    private static float BaselineFromLineTopForDraw(FontLibrary fonts, UiTextLayoutLine line) =>
        line.BaselineFromLineTopPx > 0f
            ? line.BaselineFromLineTopPx
            : (line.MaxLineHeightPx > 0f ? line.MaxLineHeightPx : line.MaxLineHeight(fonts)) * 0.82f;

    /// <summary>
    /// Min/max Y of reference ink in layout coordinates (line tops + baseline + SixLabors bounds Top/Bottom).
    /// </summary>
    private static bool TryGetLayoutInkMinMax(FontLibrary fonts, UiTextLayoutEngine layout, out float inkMin,
        out float inkMax)
    {
        inkMin = float.PositiveInfinity;
        inkMax = float.NegativeInfinity;
        var any = false;
        foreach (var line in layout.Lines)
        {
            var baselineFromLineTop = BaselineFromLineTopForDraw(fonts, line);
            if (!UiTextMeasurer.TryGetLineReferenceInkTopBottom(fonts, line, out var refTop, out var refBottom))
                continue;
            any = true;
            var top = line.LineTop + baselineFromLineTop + refTop;
            var bottom = line.LineTop + baselineFromLineTop + refBottom;
            inkMin = MathF.Min(inkMin, top);
            inkMax = MathF.Max(inkMax, bottom);
        }

        if (!any)
        {
            inkMin = 0f;
            inkMax = 0f;
            return false;
        }

        return true;
    }

    private static float LineContentAdvance(UiTextLayoutLine line) => line.LineAdvance;

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

    private void DrawRun(
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
        var write = new CachedDrawRun();

        if (string.IsNullOrEmpty(text))
        {
            _drawRunCache.Add(write);
            return;
        }

        EnsureDrawScratchCapacity(text.Length);
        var dest = _drawGlyphScratch!.AsSpan(0, text.Length);
        var n = TextRenderer.FillGlyphRunGlyphs(
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
            renderer.SubmitTextGlyphs(dest[..n]);

        var decorBaseline = baselineLeft;
        var ink = ReadOnlySpan<TextGlyphDrawRequest>.Empty;
        if (n > 0)
        {
            ink = dest[..n];
            if (space is not CoordinateSpace.ViewportSpace and not CoordinateSpace.SwapchainSpace)
                decorBaseline = new Vector2D<float>(baselineLeft.X, TextRenderer.RecoverBaselineYFromGlyph(in ink[0]));
        }

        TextRenderer.SubmitTextDecorations(
            renderer,
            in style,
            decorBaseline,
            0f,
            penAfter,
            sortKey,
            renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId,
            space,
            applyVpClip,
            viewportClip,
            fonts,
            ink);

        write.Text = text;
        write.Style = style;
        write.BaselineLeft = baselineLeft;
        write.SortKey = sortKey;
        write.Space = space;
        write.ApplyViewportClip = applyVpClip;
        write.ViewportClip = viewportClip;
        write.PenAfter = penAfter;
        write.GlyphCount = n;
        if (n > 0)
        {
            if (write.Glyphs.Length < n)
                write.Glyphs = new TextGlyphDrawRequest[Math.Max(64, n)];
            dest[..n].CopyTo(write.Glyphs);
        }

        _drawRunCache.Add(write);
    }

    private void EnsureDrawScratchCapacity(int needed)
    {
        if (_drawGlyphScratch is not null && _drawGlyphScratch.Length >= needed)
            return;

        _drawGlyphScratch = new TextGlyphDrawRequest[Math.Max(64, needed)];
    }
}
