using System;
using System.Buffers;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Submits text glyphs through the dedicated batched text path (LTR, packed glyph atlas textures).
/// Ordinary HUD should use <see cref="Scene.Systems.TextRenderSystem"/> with <see cref="Scene.BitmapText"/>;
/// call these methods only for custom drawing.
/// </summary>
/// <remarks>
/// The world-space overloads (<c>DrawLiteral</c>, <c>DrawLocalized</c>, <c>DrawRuns</c>) take a baseline-left
/// in world pixels (+Y up) and submit glyph sprites in <see cref="CoordinateSpace.WorldSpace"/>; the renderer
/// applies the active camera transform. The <c>*Screen</c> overloads take a viewport-pixel baseline-left
/// (+Y down, top-left, extent <see cref="IRenderer.ActiveCameraViewportSize"/>) and submit glyphs in
/// <see cref="CoordinateSpace.ViewportSpace"/> so HUD text stays locked to the camera's virtual canvas.
/// Localization keys use <see cref="LocalizationManager.Get"/> (missing keys echo the key).
/// </remarks>
public static class TextRenderer
{
    /// <summary>
    /// Per-glyph depth spacing so text submission keeps deterministic order when sort keys tie.
    /// </summary>
    private const float GlyphOrdinalDepthHintEpsilon = 1e-5f;

    /// <summary>
    /// Baseline-relative glyph offsets from the atlas are authored in a +Y-up sense; screen-locked spaces store positions
    /// with +Y down (must match the active camera / swapchain projection in the renderer).
    /// </summary>
    private static bool ScreenSpaceYDown(CoordinateSpace space) =>
        space is CoordinateSpace.ViewportSpace or CoordinateSpace.SwapchainSpace;

    /// <summary>Draws a literal string with a single style in world space.</summary>
    public static void DrawLiteral(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        in TextStyle style,
        string text,
        Vector2D<float> baselineLeftWorld,
        float sortKey = 450f) =>
        DrawLiteral(renderer, fonts, cache, in style, text, baselineLeftWorld, CoordinateSpace.WorldSpace, sortKey);

    /// <summary>Draws a literal string with a single style in <paramref name="space"/> (world or viewport).</summary>
    public static void DrawLiteral(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        in TextStyle style,
        string text,
        Vector2D<float> baselineLeft,
        CoordinateSpace space,
        float sortKey = 450f)
    {
        if (renderer is null || string.IsNullOrEmpty(text))
            return;

        _ = SubmitGlyphRunWithDecorations(renderer, fonts, cache, text, in style, baselineLeft, 0f, sortKey, space, false,
            default);
    }

    /// <summary>Draws a localized string in world space.</summary>
    public static void DrawLocalized(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager localization,
        in TextStyle style,
        string key,
        Vector2D<float> baselineLeftWorld,
        float sortKey = 450f) =>
        DrawLocalized(renderer, fonts, cache, localization, in style, key, baselineLeftWorld,
            CoordinateSpace.WorldSpace, sortKey);

    /// <summary>Draws a localized string in <paramref name="space"/> (world or viewport).</summary>
    public static void DrawLocalized(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager localization,
        in TextStyle style,
        string key,
        Vector2D<float> baselineLeft,
        CoordinateSpace space,
        float sortKey = 450f)
    {
        if (renderer is null || localization is null || string.IsNullOrEmpty(key))
            return;

        var resolved = localization.Get(key);
        if (string.IsNullOrEmpty(resolved))
            return;

        _ = SubmitGlyphRunWithDecorations(renderer, fonts, cache, resolved, in style, baselineLeft, 0f, sortKey, space, false,
            default);
    }

    /// <summary>
    /// Draws a literal string with <paramref name="baselineLeftScreen"/> in viewport pixels (+Y down, top-left,
    /// extent <see cref="IRenderer.ActiveCameraViewportSize"/>); glyphs are submitted in viewport space so they
    /// stay locked to the camera's virtual canvas.
    /// </summary>
    public static void DrawLiteralScreen(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        in TextStyle style,
        string text,
        Vector2D<float> baselineLeftScreen,
        float sortKey = 450f) =>
        DrawLiteral(renderer, fonts, cache, in style, text, baselineLeftScreen, CoordinateSpace.ViewportSpace, sortKey);

    /// <summary>Draws a localized string with <paramref name="baselineLeftScreen"/> in viewport pixels (+Y down, top-left).</summary>
    public static void DrawLocalizedScreen(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager localization,
        in TextStyle style,
        string key,
        Vector2D<float> baselineLeftScreen,
        float sortKey = 450f) =>
        DrawLocalized(renderer, fonts, cache, localization, in style, key, baselineLeftScreen,
            CoordinateSpace.ViewportSpace, sortKey);

    /// <summary>Draws multiple runs (mixed colors, styles, and localized segments) in world space.</summary>
    public static void DrawRuns(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager? localization,
        ReadOnlySpan<TextRun> runs,
        Vector2D<float> baselineLeftWorld,
        float sortKey = 450f) =>
        DrawRuns(renderer, fonts, cache, localization, runs, baselineLeftWorld, CoordinateSpace.WorldSpace, sortKey);

    /// <summary>Draws multiple runs in <paramref name="space"/> (world or viewport).</summary>
    public static void DrawRuns(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager? localization,
        ReadOnlySpan<TextRun> runs,
        Vector2D<float> baselineLeft,
        CoordinateSpace space,
        float sortKey = 450f)
    {
        if (renderer is null || runs.Length == 0)
            return;

        float pen = 0f;
        foreach (var run in runs)
        {
            var text = run.IsLocalizationKey && localization is not null
                ? localization.Get(run.Content)
                : run.Content;

            if (string.IsNullOrEmpty(text))
                continue;

            var st = run.Style;
            var runStart = pen;
            pen = SubmitGlyphRunWithDecorations(renderer, fonts, cache, text, in st, baselineLeft, runStart, sortKey,
                space, false, default);
        }
    }

    /// <summary>Draws multiple runs with <paramref name="baselineLeftScreen"/> in viewport pixels (+Y down, top-left).</summary>
    public static void DrawRunsScreen(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager? localization,
        ReadOnlySpan<TextRun> runs,
        Vector2D<float> baselineLeftScreen,
        float sortKey = 450f) =>
        DrawRuns(renderer, fonts, cache, localization, runs, baselineLeftScreen, CoordinateSpace.ViewportSpace, sortKey);

    /// <summary>
    /// Fills <paramref name="destination"/> with glyph draw entries (no submit). Returns glyph count and final pen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At most one emitted quad per successful <see cref="Rune.DecodeFromUtf16"/> step when the glyph raster/cache hits — so
    /// <c>return value ≤ text.Length</c> and <c>return value ≤ destination.Length</c> (whichever is tighter). BitmapText always
    /// passes <c>destination.Length == text.Length</c> from <see cref="Scene.Systems.TextRuntimeBuilder.BuildGlyphSprites"/>.
    /// </para>
    /// <para>
    /// If <paramref name="destination"/> is shorter than <c>text.Length</c>, layout stops once the buffer fills (tests rely on
    /// that); do not use a shorter span in production paths.
    /// </para>
    /// </remarks>
    internal static int FillGlyphRunGlyphs(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        string text,
        in TextStyle style,
        Vector2D<float> baselineLeft,
        float initialPen,
        float sortKey,
        Span<TextGlyphDrawRequest> destination,
        out float penAfter,
        CoordinateSpace space = CoordinateSpace.WorldSpace,
        bool applyViewportClip = false,
        UiRect viewportClip = default)
    {
        penAfter = initialPen;
        if (string.IsNullOrEmpty(text) || destination.Length == 0)
            return 0;

        var span = text.AsSpan();
        var n = 0;
        var pen = initialPen;
        // BitmapText passes CoordinateSpace through to SpriteDrawRequest.Space.
        // SwapchainSpace uses the same +Y-down authoring convention as ViewportSpace (see CameraProjection); mixing it
        // with world-style +Y up here doubled vertical spacing vs DrawSpriteSwapchainUi and could leave junk outside clip.
        var ySign = ScreenSpaceYDown(space) ? -1f : 1f;
        // Snap HUD baseline to whole pixels so MSDF sprites avoid chronic blur from fractional screen origins (layout
        // still uses float advances in pen space).
        var baselineOrigin = SnapBaselineForSpace(baselineLeft, space);
        // DecodeFromUtf16 (not EnumerateRunes): ill-formed UTF-16 stops decoding without emitting a replacement glyph —
        // EnumerateRunes would substitute U+FFFD and change golden tests / lone-surrogate behavior.
        for (var i = 0; i < span.Length;)
        {
            if (Rune.DecodeFromUtf16(span[i..], out var r, out var len) != OperationStatus.Done)
                break;
            var g = span.Slice(i, len);
            i += len;

            // Raster/cache miss: advance pen so the rest of the line lays out; do NOT increment n — no quad is emitted.
            // Decoration paths using penAfter can therefore extend past the last visible glyph (underline longer than ink).
            if (!cache.TryGetGlyph(renderer, fonts, in style, r.Value, g, out var cg))
            {
                pen += FallbackAdvanceWhenGlyphUnavailable(in style);
                continue;
            }

            if (n >= destination.Length)
                break;

            var cx = baselineOrigin.X + pen + cg.OffsetPenToCenterX;
            var cy = baselineOrigin.Y + cg.OffsetPenToCenterYWorld * ySign;
            var ordinal = n;
            destination[n++] = CreateGlyphDrawRequest(cg, cx, cy, in style, sortKey, ordinal, space,
                applyViewportClip, viewportClip);
            pen += cg.AdvancePx;
        }

        penAfter = pen;
        return n;
    }

    /// <summary>Rents a glyph buffer for <paramref name="text"/>, submits quads, then decorations (underline shares ink geometry for viewport guard).</summary>
    private static float SubmitGlyphRunWithDecorations(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        string text,
        in TextStyle style,
        Vector2D<float> baselineLeft,
        float initialPen,
        float sortKey,
        CoordinateSpace space,
        bool applyViewportClip,
        UiRect viewportClip)
    {
        var span = text.AsSpan();
        var pool = ArrayPool<TextGlyphDrawRequest>.Shared;
        var buf = pool.Rent(span.Length);
        try
        {
            var dest = buf.AsSpan(0, span.Length);
            var n = FillGlyphRunGlyphs(renderer, fonts, cache, text, in style, baselineLeft, initialPen, sortKey, dest,
                out var penAfter, space, applyViewportClip, viewportClip);
            var ink = n > 0 ? dest[..n] : ReadOnlySpan<TextGlyphDrawRequest>.Empty;
            if (n > 0)
                renderer.SubmitTextGlyphs(ink);
            SubmitTextDecorations(renderer, in style, baselineLeft, initialPen, penAfter, sortKey, renderer.WhiteTextureId,
                renderer.DefaultNormalTextureId, space, applyViewportClip, viewportClip, fonts, ink);
            return penAfter;
        }
        finally
        {
            pool.Return(buf);
        }
    }

    internal static void SubmitTextDecorations(
        IRenderer renderer,
        in TextStyle style,
        Vector2D<float> baselineLeft,
        float penStart,
        float penEnd,
        float sortKey,
        TextureId whiteTex,
        TextureId defNormal,
        CoordinateSpace space = CoordinateSpace.WorldSpace,
        bool applyViewportClip = false,
        UiRect viewportClip = default,
        FontLibrary? fonts = null,
        ReadOnlySpan<TextGlyphDrawRequest> underlineInkGuardGlyphs = default) =>
        AddDecorations(renderer, in style, baselineLeft, penStart, penEnd, sortKey, whiteTex, defNormal, space,
            applyViewportClip, viewportClip, fonts, underlineInkGuardGlyphs);

    /// <summary>
    /// Recovers typographic baseline Y from a submitted glyph quad (inverse of <see cref="FillGlyphRunGlyphs"/> vertical math).
    /// Decoration strips use this when the transform-authored baseline and actual glyph centers could diverge.
    /// Uses <see cref="TextGlyphDrawRequest.Space"/> so recovery cannot disagree with the quad's own coordinate system.
    /// </summary>
    internal static float RecoverBaselineYFromGlyph(in TextGlyphDrawRequest g)
    {
        var ySign = ScreenSpaceYDown(g.Space) ? -1f : 1f;
        return g.Center.Y - g.OffsetPenToCenterYWorld * ySign;
    }

    /// <summary>Approximate em advance when a glyph cannot be cached so the rest of the string still lays out.</summary>
    private static float FallbackAdvanceWhenGlyphUnavailable(in TextStyle style) =>
        MathF.Max(4f, style.SizePixels * 0.35f);

    /// <summary>
    /// Viewport/swapchain HUD uses the opaque sprite path (G-buffer) so glyph stacks do not pass through WBOIT.
    /// Weighted OIT + overlapping semi-transparent quads produced persistent HUD smear in practice; world/local text
    /// stays on the transparent path for correct compositing over the lit scene.
    /// </summary>
    private static bool TransparentSpriteForSpace(CoordinateSpace space) =>
        space is not CoordinateSpace.ViewportSpace and not CoordinateSpace.SwapchainSpace;

    private static TextGlyphDrawRequest CreateGlyphDrawRequest(
        TextGlyphCache.CachedGlyph g,
        float centerX,
        float centerY,
        in TextStyle style,
        float sortKey,
        int glyphOrdinalInRun,
        CoordinateSpace space,
        bool applyViewportClip,
        UiRect viewportClip)
    {
        var cm = style.Color;
        var clip = applyViewportClip && ScreenSpaceYDown(space);
        return new TextGlyphDrawRequest
        {
            Center = new Vector2D<float>(centerX, centerY),
            HalfExtents = new Vector2D<float>(g.WidthPx * 0.5f, g.HeightPx * 0.5f),
            OffsetPenToCenterYWorld = g.OffsetPenToCenterYWorld,
            SortKey = sortKey,
            TextureId = g.TextureId,
            MsdfPixelRange = g.MsdfPixelRange,
            Color = cm,
            DepthHint = glyphOrdinalInRun * GlyphOrdinalDepthHintEpsilon,
            UvRect = g.UvRect,
            Space = space,
            ViewportClipEnabled = clip,
            ViewportClipRect = viewportClip
        };
    }

    private static void AddDecorations(
        IRenderer renderer,
        in TextStyle style,
        Vector2D<float> baselineLeft,
        float penStart,
        float penEnd,
        float sortKey,
        TextureId whiteTex,
        TextureId defNormal,
        CoordinateSpace space,
        bool applyViewportClip,
        UiRect viewportClip,
        FontLibrary? fonts,
        ReadOnlySpan<TextGlyphDrawRequest> underlineInkGuardGlyphs)
    {
        if (penEnd <= penStart)
            return;

        var cm = style.Color;
        var defaultDecorLineHalfH = TextDecorationMetrics.DefaultDecorLineHalfHeight(style.SizePixels);
        var baselineOrigin = SnapBaselineForSpace(baselineLeft, space);
        // Same vertical convention as FillGlyphRunGlyphs: cy = baselineOrigin.Y + OffsetPenToCenterYWorld * ySign.
        var ySign = ScreenSpaceYDown(space) ? -1f : 1f;

        float inkMinTopVp = float.NaN, inkMaxBottomVp = float.NaN;
        if (underlineInkGuardGlyphs.Length > 0)
        {
            var t = float.PositiveInfinity;
            var b = float.NegativeInfinity;
            foreach (ref readonly var gr in underlineInkGuardGlyphs)
            {
                if (gr.Space != space)
                    continue;
                t = MathF.Min(t, gr.Center.Y - gr.HalfExtents.Y);
                b = MathF.Max(b, gr.Center.Y + gr.HalfExtents.Y);
            }

            if (t < float.PositiveInfinity)
            {
                inkMinTopVp = t;
                inkMaxBottomVp = b;
            }
        }
        var hasViewportInkBand = ScreenSpaceYDown(space) && !float.IsNaN(inkMinTopVp) && !float.IsNaN(inkMaxBottomVp);

        FontLibrary.OpenTypeTextDecorationLayout ot = default;
        var hasOtLayout = (style.Underline || style.Strikethrough) && fonts is not null &&
                          fonts.TryGetOpenTypeTextDecorationLayout(in style, out ot);

        float underlineLineHalfH = defaultDecorLineHalfH;
        var underlineY = baselineOrigin.Y;
        if (style.Underline)
        {
            if (hasOtLayout)
            {
                underlineLineHalfH = MathF.Max(TextDecorationMetrics.OpenTypeDecorHalfThicknessMinPx,
                    ot.UnderlineThicknessPx * 0.5f);
                underlineY = baselineOrigin.Y + ot.UnderlineCenterOffsetPenFontUp * ySign;
            }
            else
            {
                var fallbackCenterDown = MathF.Max(TextDecorationMetrics.FallbackUnderlineCenterDownMinPx,
                    style.SizePixels * TextDecorationMetrics.FallbackUnderlineCenterDownEm);
                var fallbackUnderlineFontUp = -fallbackCenterDown;
                underlineY = baselineOrigin.Y + fallbackUnderlineFontUp * ySign;
            }

            // Hard clamp: OpenType / fallback mistakes must never draw an "underline" strip above the typographic baseline
            // in HUD (+Y down) or above it in world (+Y up). Uses Max/Min so the branch always runs (coverage-safe).
            var minGap = TextDecorationMetrics.BaselineUnderlineMinimumGapPx(style.SizePixels);
            if (ScreenSpaceYDown(space))
                underlineY = MathF.Max(underlineY, baselineOrigin.Y + minGap);
            else
                underlineY = MathF.Min(underlineY, baselineOrigin.Y - minGap);

            // With viewport glyph bounds, resolve underline in one clamped step:
            // - stroke top stays below approximate ink bottom by a small clearance
            // - stroke does not drift more than ceil(10% of visible line height) below that bottom
            if (hasViewportInkBand)
            {
                underlineY = TextDecorationMetrics.ResolveViewportUnderlineCenterWithInkBand(
                    baselineOrigin.Y,
                    style.SizePixels,
                    underlineY,
                    underlineLineHalfH,
                    inkMinTopVp,
                    inkMaxBottomVp);
            }
            else if (ScreenSpaceYDown(space))
            {
                underlineY = MathF.Max(underlineY,
                    baselineOrigin.Y + TextDecorationMetrics.ViewportUnderlineMinCenterBelowBaselinePx(style.SizePixels));
            }
        }

        float strikeLineHalfH = defaultDecorLineHalfH;
        var strikeY = baselineOrigin.Y;
        if (style.Strikethrough)
        {
            if (hasOtLayout)
            {
                strikeLineHalfH = MathF.Max(TextDecorationMetrics.OpenTypeDecorHalfThicknessMinPx,
                    ot.StrikethroughThicknessPx * 0.5f);
                strikeY = baselineOrigin.Y + ot.StrikethroughCenterOffsetPenFontUp * ySign;
            }
            else
            {
                var fallbackStrikeFontUp = style.SizePixels * TextDecorationMetrics.FallbackStrikethroughFontUpEm;
                strikeY = baselineOrigin.Y + fallbackStrikeFontUp * ySign;
            }

            if (hasViewportInkBand)
            {
                // Viewport HUD text should read as a true strikethrough on the visible glyph body, not typographic x-height.
                strikeY = (inkMinTopVp + inkMaxBottomVp) * TextDecorationMetrics.InkBandVerticalMidpointFactor;
            }
        }

        if (style.Underline)
        {
            SubmitLine(renderer, baselineOrigin.X + penStart, baselineOrigin.X + penEnd, underlineY, underlineLineHalfH,
                cm, sortKey + TextDecorationMetrics.SortKeyUnderlineDelta, whiteTex, defNormal, space, applyViewportClip,
                viewportClip);
        }

        if (style.Strikethrough)
            SubmitLine(renderer, baselineOrigin.X + penStart, baselineOrigin.X + penEnd, strikeY, strikeLineHalfH,
                cm, sortKey + TextDecorationMetrics.SortKeyStrikethroughDelta, whiteTex, defNormal, space,
                applyViewportClip, viewportClip);
    }

    private static Vector2D<float> SnapBaselineForSpace(Vector2D<float> baselineLeft, CoordinateSpace space)
    {
        if (!ScreenSpaceYDown(space))
            return baselineLeft;

        return new Vector2D<float>(
            MathF.Round(baselineLeft.X),
            MathF.Round(baselineLeft.Y));
    }

    private static void SubmitLine(
        IRenderer renderer,
        float xStart,
        float xEnd,
        float y,
        float halfHeight,
        Vector4D<float> color,
        float sortKey,
        TextureId whiteTex,
        TextureId defN,
        CoordinateSpace space,
        bool applyViewportClip,
        UiRect viewportClip)
    {
        var midX = (xStart + xEnd) * 0.5f;
        var halfW = MathF.Max(0.5f, (xEnd - xStart) * 0.5f);
        var clip = applyViewportClip && ScreenSpaceYDown(space);
        var req = new SpriteDrawRequest
        {
            CenterWorld = new Vector2D<float>(midX, y),
            HalfExtentsWorld = new Vector2D<float>(halfW, halfHeight),
            RotationRadians = 0f,
            Layer = (int)SpriteLayer.Ui,
            SortKey = sortKey,
            AlbedoTextureId = whiteTex,
            NormalTextureId = defN,
            EmissiveTextureId = TextureId.MaxValue,
            ColorMultiply = color,
            Alpha = color.W,
            EmissiveTint = default,
            EmissiveIntensity = 0f,
            DepthHint = 0f,
            UvRect = default,
            Transparent = TransparentSpriteForSpace(space),
            Space = space,
            ViewportClipEnabled = clip,
            ViewportClipRect = viewportClip
        };
        renderer.SubmitSprite(in req);
    }
}
