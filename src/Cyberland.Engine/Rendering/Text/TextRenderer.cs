using System.Buffers;
using System.Text;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Submits bitmap text as UI-layer <see cref="SpriteDrawRequest"/> quads (LTR, packed glyph atlas textures).
/// Ordinary HUD should use <see cref="Scene.Systems.TextRenderSystem"/> with <see cref="Scene.BitmapText"/>;
/// call these methods only for custom drawing.
/// </summary>
/// <remarks>
/// The world-space overloads (<c>DrawLiteral</c>, <c>DrawLocalized</c>, <c>DrawRuns</c>) take a baseline-left
/// in world pixels (+Y up) and submit glyph sprites in <see cref="SpriteCoordinateSpace.World"/>; the renderer
/// applies the active camera transform. The <c>*Screen</c> overloads take a viewport-pixel baseline-left
/// (+Y down, top-left, extent <see cref="IRenderer.ActiveCameraViewportSize"/>) and submit glyphs in
/// <see cref="SpriteCoordinateSpace.Viewport"/> so HUD text stays locked to the camera's virtual canvas.
/// Localization keys use <see cref="LocalizationManager.Get"/> (missing keys echo the key).
/// </remarks>
public static class TextRenderer
{
    /// <summary>
    /// Per-glyph <see cref="SpriteDrawRequest.DepthHint"/> spacing so <see cref="SpriteDrawSorter"/> has a stable tie-break
    /// when every glyph in a run shares the same <see cref="SpriteDrawRequest.SortKey"/> (Array.Sort is not stable).
    /// </summary>
    private const float GlyphOrdinalDepthHintEpsilon = 1e-5f;

    /// <summary>Draws a literal string with a single style in world space.</summary>
    public static void DrawLiteral(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        in TextStyle style,
        string text,
        Vector2D<float> baselineLeftWorld,
        float sortKey = 450f) =>
        DrawLiteral(renderer, fonts, cache, in style, text, baselineLeftWorld, SpriteCoordinateSpace.World, sortKey);

    /// <summary>Draws a literal string with a single style in <paramref name="space"/> (world or viewport).</summary>
    public static void DrawLiteral(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        in TextStyle style,
        string text,
        Vector2D<float> baselineLeft,
        SpriteCoordinateSpace space,
        float sortKey = 450f)
    {
        if (renderer is null || string.IsNullOrEmpty(text))
            return;

        var pen = FillGlyphRunAndSubmit(renderer, fonts, cache, text, in style, baselineLeft, 0f, sortKey, space);
        SubmitTextDecorations(renderer, in style, baselineLeft, 0f, pen, sortKey, renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId, space);
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
            SpriteCoordinateSpace.World, sortKey);

    /// <summary>Draws a localized string in <paramref name="space"/> (world or viewport).</summary>
    public static void DrawLocalized(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager localization,
        in TextStyle style,
        string key,
        Vector2D<float> baselineLeft,
        SpriteCoordinateSpace space,
        float sortKey = 450f)
    {
        if (renderer is null || localization is null || string.IsNullOrEmpty(key))
            return;

        var resolved = localization.Get(key);
        if (string.IsNullOrEmpty(resolved))
            return;

        var pen = FillGlyphRunAndSubmit(renderer, fonts, cache, resolved, in style, baselineLeft, 0f, sortKey, space);
        SubmitTextDecorations(renderer, in style, baselineLeft, 0f, pen, sortKey, renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId, space);
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
        DrawLiteral(renderer, fonts, cache, in style, text, baselineLeftScreen, SpriteCoordinateSpace.Viewport, sortKey);

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
            SpriteCoordinateSpace.Viewport, sortKey);

    /// <summary>Draws multiple runs (mixed colors, styles, and localized segments) in world space.</summary>
    public static void DrawRuns(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager? localization,
        ReadOnlySpan<TextRun> runs,
        Vector2D<float> baselineLeftWorld,
        float sortKey = 450f) =>
        DrawRuns(renderer, fonts, cache, localization, runs, baselineLeftWorld, SpriteCoordinateSpace.World, sortKey);

    /// <summary>Draws multiple runs in <paramref name="space"/> (world or viewport).</summary>
    public static void DrawRuns(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager? localization,
        ReadOnlySpan<TextRun> runs,
        Vector2D<float> baselineLeft,
        SpriteCoordinateSpace space,
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
            pen = FillGlyphRunAndSubmit(renderer, fonts, cache, text, in st, baselineLeft, pen, sortKey, space);
            var runEnd = pen;
            SubmitTextDecorations(renderer, in st, baselineLeft, runStart, runEnd, sortKey, renderer.WhiteTextureId,
                renderer.DefaultNormalTextureId, space);
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
        DrawRuns(renderer, fonts, cache, localization, runs, baselineLeftScreen, SpriteCoordinateSpace.Viewport, sortKey);

    /// <summary>
    /// Fills <paramref name="destination"/> with glyph quads (no submit). Returns glyph count and final pen.
    /// </summary>
    internal static int FillGlyphRunSprites(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        string text,
        in TextStyle style,
        Vector2D<float> baselineLeft,
        float initialPen,
        float sortKey,
        Span<SpriteDrawRequest> destination,
        out float penAfter,
        SpriteCoordinateSpace space = SpriteCoordinateSpace.World)
    {
        penAfter = initialPen;
        if (string.IsNullOrEmpty(text) || destination.Length == 0)
            return 0;

        var defN = renderer.DefaultNormalTextureId;
        var span = text.AsSpan();
        var n = 0;
        var pen = initialPen;
        // Viewport space is +Y down: glyph baseline grows down, so the per-glyph Y-offset (which is authored in
        // +Y up "baseline-to-center") must flip sign to land at the correct pixel row.
        var ySign = space == SpriteCoordinateSpace.Viewport ? -1f : 1f;
        for (var i = 0; i < span.Length;)
        {
            if (Rune.DecodeFromUtf16(span[i..], out var r, out var len) != OperationStatus.Done)
                break;
            var g = span.Slice(i, len);
            i += len;

            if (!cache.TryGetGlyph(renderer, fonts, in style, r.Value, g, out var cg))
            {
                pen += FallbackAdvanceWhenGlyphUnavailable(in style);
                continue;
            }

            if (n >= destination.Length)
                break;

            var cx = baselineLeft.X + pen + cg.OffsetPenToCenterX;
            var cy = baselineLeft.Y + cg.OffsetPenToCenterYWorld * ySign;
            var ordinal = n;
            destination[n++] = CreateGlyphSpriteRequest(cg, cx, cy, in style, sortKey, defN, ordinal, space);
            pen += cg.AdvancePx;
        }

        penAfter = pen;
        return n;
    }

    private static float FillGlyphRunAndSubmit(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        string text,
        in TextStyle style,
        Vector2D<float> baselineLeft,
        float pen,
        float sortKey,
        SpriteCoordinateSpace space)
    {
        // Callers skip empty strings; avoid a redundant branch the gate cannot hit.
        var span = text.AsSpan();

        var pool = ArrayPool<SpriteDrawRequest>.Shared;
        var buf = pool.Rent(span.Length);
        try
        {
            var dest = buf.AsSpan(0, span.Length);
            var n = FillGlyphRunSprites(renderer, fonts, cache, text, in style, baselineLeft, pen, sortKey, dest,
                out var penAfter, space);
            if (n > 0)
                renderer.SubmitSprites(dest[..n]);
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
        SpriteCoordinateSpace space = SpriteCoordinateSpace.World) =>
        AddDecorations(renderer, in style, baselineLeft, penStart, penEnd, sortKey, whiteTex, defNormal, space);

    /// <summary>Approximate em advance when a glyph cannot be cached so the rest of the string still lays out.</summary>
    private static float FallbackAdvanceWhenGlyphUnavailable(in TextStyle style) =>
        MathF.Max(4f, style.SizePixels * 0.35f);

    private static SpriteDrawRequest CreateGlyphSpriteRequest(
        TextGlyphCache.CachedGlyph g,
        float centerX,
        float centerY,
        in TextStyle style,
        float sortKey,
        TextureId defNormal,
        int glyphOrdinalInRun,
        SpriteCoordinateSpace space)
    {
        var cm = style.Color;
        return new SpriteDrawRequest
        {
            CenterWorld = new Vector2D<float>(centerX, centerY),
            HalfExtentsWorld = new Vector2D<float>(g.WidthPx * 0.5f, g.HeightPx * 0.5f),
            RotationRadians = 0f,
            Layer = (int)SpriteLayer.Ui,
            SortKey = sortKey,
            AlbedoTextureId = g.TextureId,
            NormalTextureId = defNormal,
            EmissiveTextureId = TextureId.MaxValue,
            ColorMultiply = cm,
            Alpha = cm.W,
            EmissiveTint = default,
            EmissiveIntensity = 0f,
            DepthHint = glyphOrdinalInRun * GlyphOrdinalDepthHintEpsilon,
            UvRect = g.UvRect,
            Transparent = true,
            Space = space
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
        SpriteCoordinateSpace space)
    {
        if (penEnd <= penStart)
            return;

        var cm = style.Color;
        var lineHalfH = MathF.Max(1f, style.SizePixels * 0.06f);
        // Viewport space grows +Y down so underlines sit just below the baseline (larger Y), strike mid-line
        // goes slightly up (smaller Y). World space uses the opposite sign.
        var underlineOffset = MathF.Max(1.5f, style.SizePixels * 0.12f);
        var strikeOffset = style.SizePixels * 0.08f;
        var ySign = space == SpriteCoordinateSpace.Viewport ? -1f : 1f;
        var underlineY = baselineLeft.Y - underlineOffset * ySign;
        var strikeY = baselineLeft.Y + strikeOffset * ySign;

        if (style.Underline)
            SubmitLine(renderer, baselineLeft.X + penStart, baselineLeft.X + penEnd, underlineY, lineHalfH,
                cm, sortKey + 0.1f, whiteTex, defNormal, space);

        if (style.Strikethrough)
            SubmitLine(renderer, baselineLeft.X + penStart, baselineLeft.X + penEnd, strikeY, lineHalfH,
                cm, sortKey + 0.15f, whiteTex, defNormal, space);
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
        SpriteCoordinateSpace space)
    {
        var midX = (xStart + xEnd) * 0.5f;
        var halfW = MathF.Max(0.5f, (xEnd - xStart) * 0.5f);
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
            Transparent = true,
            Space = space
        };
        renderer.SubmitSprite(in req);
    }
}
