namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Tunables for underline/strikethrough placement in <see cref="TextRenderer"/> (viewport ink-aware paths, OT vs fallback).
/// Single place for em fractions and pixel floors so gameplay tuning does not scatter magic numbers.
/// </summary>
internal static class TextDecorationMetrics
{
    /// <summary>Decor lines submit after glyph sort keys; keep deterministic ordering vs glyphs.</summary>
    public const float SortKeyUnderlineDelta = 0.1f;

    public const float SortKeyStrikethroughDelta = 0.15f;

    /// <summary>Default half-height of decoration strokes when OpenType thickness is unavailable.</summary>
    public const float DefaultDecorHalfThicknessMinPx = 1f;

    public const float DefaultDecorHalfThicknessEm = 0.06f;

    /// <summary>Fallback underline center distance below baseline (+Y down viewport) when OT metrics are missing.</summary>
    public const float FallbackUnderlineCenterDownMinPx = 1.5f;

    public const float FallbackUnderlineCenterDownEm = 0.12f;

    /// <summary>Minimum gap between typographic baseline and underline center after OT/fallback (viewport baseline clamp).</summary>
    public const float BaselineUnderlineClampGapMinPx = 1.25f;

    public const float BaselineUnderlineClampGapEm = 0.055f;

    /// <summary>Fallback strikethrough offset from baseline when OT metrics are missing (font-up convention before ySign).</summary>
    public const float FallbackStrikethroughFontUpEm = 0.08f;

    public const float OpenTypeDecorHalfThicknessMinPx = 0.5f;

    /// <summary>
    /// When viewport underline has no glyph ink span, minimum distance from baseline to stroke center (tall MSDF quads).
    /// </summary>
    public const float ViewportUnderlineBaselineFloorMinPx = 10f;

    public const float ViewportUnderlineBaselineFloorEm = 0.55f;

    /// <summary>Gap from glyph ink bottom to the top edge of the underline stroke (viewport +Y down).</summary>
    public const float ViewportUnderlineGapBelowInkMinPx = 1.5f;

    public const float ViewportUnderlineGapBelowInkEm = 0.075f;

    /// <summary>
    /// Threshold: quad depth below baseline at or below this × size selects the caps-heavy fraction; MSDF padding often
    /// extends much farther than real ink, so this stays generous (well above one descender depth) and reserves the deep
    /// fraction for unusually tall below-baseline boxes.
    /// </summary>
    public const float ViewportUnderlineTightBandDepthShallowBelowBaselineEm = 1.85f;

    /// <summary>Caps-heavy HUD rows: approximate visible ink bottom as baseline + this × em (clamped to quad bounds).</summary>
    public const float ViewportUnderlineTightBandBelowBaselineShallowEm = 0.24f;

    /// <summary>Deeper glyph boxes: allow more ink below baseline before clamping visible bottom.</summary>
    public const float ViewportUnderlineTightBandBelowBaselineDeepEm = 0.48f;

    /// <summary>Approximate ink top as baseline − this × em, clamped into quad bounds (+Y down).</summary>
    public const float ViewportUnderlineTightBandAboveBaselineEm = 0.88f;

    /// <summary>
    /// Latin HUD rows: approximate maximum ink extent below the typographic baseline (descenders). Used with MSDF quads to
    /// keep underline stroke tops below likely glyph ink when the tight-band heuristic is misaligned (+Y down viewport).
    /// </summary>
    public const float ViewportUnderlineBaselineInkDescenderMaxEm = 0.90f;

    /// <summary>
    /// Additional clearance between approximate ink bottom and underline stroke top in viewport space (+Y down) so antialias
    /// edges do not visually merge into a faux strikethrough in caps-heavy HUD text.
    /// </summary>
    public const float ViewportUnderlineInkClearanceMinPx = 2f;

    public const float ViewportUnderlineInkClearanceEm = 0.04f;

    /// <summary>Viewport strikethrough is snapped to ink vertical midpoint; tests allow this deviation from literal mid.</summary>
    public const float StrikethroughInkMidToleranceMinPx = 2f;

    public const float StrikethroughInkMidToleranceEm = 0.12f;

    public static float DefaultDecorLineHalfHeight(float sizePixels) =>
        MathF.Max(DefaultDecorHalfThicknessMinPx, sizePixels * DefaultDecorHalfThicknessEm);

    /// <summary>
    /// Minimum underline center Y (+Y down) so stroke top clears baseline + approximate Latin descender ink.
    /// </summary>
    public static float ViewportUnderlineMinCenterClearBaselineDescenderInkPx(float sizePixels, float underlineHalfHeightPx) =>
        MathF.Max(3f, sizePixels * ViewportUnderlineBaselineInkDescenderMaxEm) +
        MathF.Max(ViewportUnderlineInkClearanceMinPx, sizePixels * ViewportUnderlineInkClearanceEm) +
        underlineHalfHeightPx;

    public static float ViewportUnderlineMinCenterBelowBaselinePx(float sizePixels) =>
        MathF.Max(ViewportUnderlineBaselineFloorMinPx, sizePixels * ViewportUnderlineBaselineFloorEm);

    public static float ViewportUnderlineGapBelowInkPx(float sizePixels) =>
        MathF.Max(ViewportUnderlineGapBelowInkMinPx, sizePixels * ViewportUnderlineGapBelowInkEm);

    /// <summary>
    /// Approximate lower edge of visible ink in viewport rows (+Y down): clamp MSDF quad bottom to a baseline-relative
    /// descender ceiling so transparent padding does not count as drawn glyph ink.
    /// </summary>
    public static float ViewportUnderlineApproxInkBottomPx(
        float baselineYDown,
        float sizePixels,
        float inkMaxBottomVp) =>
        MathF.Min(inkMaxBottomVp, baselineYDown + MathF.Max(3f, sizePixels * ViewportUnderlineBaselineInkDescenderMaxEm));

    /// <summary>
    /// Minimum visual clearance from approximate ink bottom to underline stroke top in viewport rows (+Y down).
    /// </summary>
    public static float ViewportUnderlineMinInkClearancePx(float sizePixels) =>
        MathF.Max(ViewportUnderlineInkClearanceMinPx, sizePixels * ViewportUnderlineInkClearanceEm);

    /// <summary>
    /// Maximum gap from estimated visible ink bottom to underline stroke top: ceil(10% of visible line height), floor 1px.
    /// </summary>
    public static float MaxUnderlineGapBelowVisibleLinePx(float visibleLineHeightPx)
    {
        if (!float.IsFinite(visibleLineHeightPx) || visibleLineHeightPx <= 0f)
            return ViewportUnderlineGapBelowInkMinPx;
        return MathF.Max(1f, MathF.Ceiling(visibleLineHeightPx * 0.1f));
    }

    /// <summary>
    /// MSDF quads pad transparent slack; approximate a tighter visible ink band for underline spacing (+Y down viewport).
    /// Falls back to full quad band when the estimate collapses.
    /// </summary>
    public static void EstimateTightVisibleInkBandViewport(
        float baselineYDown,
        float sizePixels,
        float inkMinTopVp,
        float inkMaxBottomVp,
        out float tightTopVp,
        out float tightBottomVp)
    {
        var depthBelowBaseline = inkMaxBottomVp - baselineYDown;
        var belowBaselineEm = depthBelowBaseline <= sizePixels * ViewportUnderlineTightBandDepthShallowBelowBaselineEm
            ? ViewportUnderlineTightBandBelowBaselineShallowEm
            : ViewportUnderlineTightBandBelowBaselineDeepEm;
        var proposedBottom = baselineYDown + sizePixels * belowBaselineEm;
        tightBottomVp = Math.Min(inkMaxBottomVp, proposedBottom);
        var proposedTop = baselineYDown - sizePixels * ViewportUnderlineTightBandAboveBaselineEm;
        tightTopVp = Math.Max(inkMinTopVp, proposedTop);
        if (tightTopVp >= tightBottomVp - 1e-3f)
        {
            tightTopVp = inkMinTopVp;
            tightBottomVp = inkMaxBottomVp;
        }
    }

    /// <summary>
    /// Single-rule viewport underline placement from glyph bounds (+Y down). Keeps stroke top below approximate ink by a
    /// small clearance while also capping the allowed distance below the visible line (ceil 10% rule).
    /// </summary>
    public static float ResolveViewportUnderlineCenterWithInkBand(
        float baselineYDown,
        float sizePixels,
        float preferredCenterY,
        float underlineHalfHeightPx,
        float inkMinTopVp,
        float inkMaxBottomVp)
    {
        if (!float.IsFinite(inkMinTopVp) || !float.IsFinite(inkMaxBottomVp) || inkMinTopVp >= inkMaxBottomVp - 1e-3f)
            return preferredCenterY;

        var approxInkTop = MathF.Max(inkMinTopVp, baselineYDown - sizePixels * ViewportUnderlineTightBandAboveBaselineEm);
        var approxInkBottom = ViewportUnderlineApproxInkBottomPx(baselineYDown, sizePixels, inkMaxBottomVp);
        if (approxInkBottom <= approxInkTop + 1e-3f)
        {
            approxInkTop = inkMinTopVp;
            approxInkBottom = inkMaxBottomVp;
        }

        var visibleHeight = approxInkBottom - approxInkTop;
        if (!float.IsFinite(visibleHeight) || visibleHeight <= 1e-3f)
            visibleHeight = inkMaxBottomVp - inkMinTopVp;

        var clearance = ViewportUnderlineMinInkClearancePx(sizePixels);
        var maxGap = MaxUnderlineGapBelowVisibleLinePx(visibleHeight);
        if (maxGap < clearance)
            maxGap = clearance;

        var minStrokeTop = approxInkBottom + clearance;
        var maxStrokeTop = approxInkBottom + maxGap;
        var preferredStrokeTop = preferredCenterY - underlineHalfHeightPx;
        var clampedStrokeTop = Math.Clamp(preferredStrokeTop, minStrokeTop, maxStrokeTop);
        return clampedStrokeTop + underlineHalfHeightPx;
    }

    /// <summary>
    /// Gap below ink when no usable glyph band (viewport). With ink span only (legacy callers), prefers visible-height rule when possible.
    /// </summary>
    public static float UnderlineGapBelowInkViewportPx(float sizePixels, float inkSpanPx)
    {
        var preferredWhenNoInkBand = ViewportUnderlineGapBelowInkPx(sizePixels);
        if (!float.IsFinite(inkSpanPx) || inkSpanPx <= 0f)
            return preferredWhenNoInkBand;

        return MaxUnderlineGapBelowVisibleLinePx(inkSpanPx);
    }

    public static float BaselineUnderlineMinimumGapPx(float sizePixels) =>
        MathF.Max(BaselineUnderlineClampGapMinPx, sizePixels * BaselineUnderlineClampGapEm);

    public static float StrikethroughMaxDeviationFromInkMidPx(float sizePixels) =>
        MathF.Max(StrikethroughInkMidToleranceMinPx, sizePixels * StrikethroughInkMidToleranceEm);

    /// <summary>Blend top/bottom ink extents for viewport strikethrough center.</summary>
    public const float InkBandVerticalMidpointFactor = 0.5f;
}
