using System.Buffers;
using System.Text;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Submits bitmap text as UI-layer <see cref="SpriteDrawRequest"/> quads (LTR, packed glyph atlas textures).
/// Ordinary HUD should use <see cref="Scene.Systems.TextRenderSystem"/> with <see cref="Scene.BitmapText"/>; call these methods only for custom drawing.
/// </summary>
/// <remarks>
/// <see cref="DrawLiteral"/>, <see cref="DrawLocalized"/>, and <see cref="DrawRuns"/> take a <b>world-space</b> baseline-left (+Y up).
/// <c>*Screen</c> overloads take baseline-left in <b>screen pixels</b> (top-left origin, +Y down) and convert via <see cref="WorldScreenSpace"/>.
/// Localization keys use <see cref="LocalizationManager.Get"/> (missing keys echo the key).
/// </remarks>
public static class TextRenderer
{
    /// <summary>
    /// Per-glyph <see cref="SpriteDrawRequest.DepthHint"/> spacing so <see cref="SpriteDrawSorter"/> has a stable tie-break
    /// when every glyph in a run shares the same <see cref="SpriteDrawRequest.SortKey"/> (Array.Sort is not stable).
    /// </summary>
    private const float GlyphOrdinalDepthHintEpsilon = 1e-5f;

    /// <summary>Draws a literal string with a single style.</summary>
    public static void DrawLiteral(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        in TextStyle style,
        string text,
        Vector2D<float> baselineLeftWorld,
        float sortKey = 450f)
    {
        if (renderer is null || string.IsNullOrEmpty(text))
            return;

        var pen = FillGlyphRunAndSubmit(renderer, fonts, cache, text, in style, baselineLeftWorld, 0f, sortKey);
        SubmitTextDecorations(renderer, in style, baselineLeftWorld, 0f, pen, sortKey, renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId);
    }

    /// <summary>Draws a localized string resolved through <paramref name="localization"/>.</summary>
    public static void DrawLocalized(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager localization,
        in TextStyle style,
        string key,
        Vector2D<float> baselineLeftWorld,
        float sortKey = 450f)
    {
        if (renderer is null || localization is null || string.IsNullOrEmpty(key))
            return;

        var resolved = localization.Get(key);
        if (string.IsNullOrEmpty(resolved))
            return;

        var pen = FillGlyphRunAndSubmit(renderer, fonts, cache, resolved, in style, baselineLeftWorld, 0f, sortKey);
        SubmitTextDecorations(renderer, in style, baselineLeftWorld, 0f, pen, sortKey, renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId);
    }

    /// <summary>Like <see cref="DrawLiteral"/> but <paramref name="baselineLeftScreen"/> is in framebuffer pixels (top-left, +Y down).</summary>
    public static void DrawLiteralScreen(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        in TextStyle style,
        string text,
        Vector2D<float> baselineLeftScreen,
        Vector2D<int> framebufferSize,
        float sortKey = 450f)
    {
        var w = WorldScreenSpace.ScreenPixelToWorldCenter(baselineLeftScreen, framebufferSize);
        DrawLiteral(renderer, fonts, cache, in style, text, w, sortKey);
    }

    /// <summary>Like <see cref="DrawLocalized"/> but <paramref name="baselineLeftScreen"/> is in framebuffer pixels (top-left, +Y down).</summary>
    public static void DrawLocalizedScreen(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager localization,
        in TextStyle style,
        string key,
        Vector2D<float> baselineLeftScreen,
        Vector2D<int> framebufferSize,
        float sortKey = 450f)
    {
        var w = WorldScreenSpace.ScreenPixelToWorldCenter(baselineLeftScreen, framebufferSize);
        DrawLocalized(renderer, fonts, cache, localization, in style, key, w, sortKey);
    }

    /// <summary>Draws multiple runs (mixed colors, styles, and localized segments).</summary>
    public static void DrawRuns(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager? localization,
        ReadOnlySpan<TextRun> runs,
        Vector2D<float> baselineLeftWorld,
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
            pen = FillGlyphRunAndSubmit(renderer, fonts, cache, text, in st, baselineLeftWorld, pen, sortKey);
            var runEnd = pen;
            SubmitTextDecorations(renderer, in st, baselineLeftWorld, runStart, runEnd, sortKey, renderer.WhiteTextureId,
                renderer.DefaultNormalTextureId);
        }
    }

    /// <summary>Like <see cref="DrawRuns"/> but <paramref name="baselineLeftScreen"/> is in framebuffer pixels (top-left, +Y down).</summary>
    public static void DrawRunsScreen(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        LocalizationManager? localization,
        ReadOnlySpan<TextRun> runs,
        Vector2D<float> baselineLeftScreen,
        Vector2D<int> framebufferSize,
        float sortKey = 450f)
    {
        var w = WorldScreenSpace.ScreenPixelToWorldCenter(baselineLeftScreen, framebufferSize);
        DrawRuns(renderer, fonts, cache, localization, runs, w, sortKey);
    }

    /// <summary>
    /// Fills <paramref name="destination"/> with glyph quads (no submit). Returns glyph count and final pen.
    /// </summary>
    internal static int FillGlyphRunSprites(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        string text,
        in TextStyle style,
        Vector2D<float> baselineLeftWorld,
        float initialPen,
        float sortKey,
        Span<SpriteDrawRequest> destination,
        out float penAfter)
    {
        penAfter = initialPen;
        if (string.IsNullOrEmpty(text) || destination.Length == 0)
            return 0;

        var defN = renderer.DefaultNormalTextureId;
        var span = text.AsSpan();
        var n = 0;
        var pen = initialPen;
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

            var cx = baselineLeftWorld.X + pen + cg.OffsetPenToCenterX;
            var cy = baselineLeftWorld.Y + cg.OffsetPenToCenterYWorld;
            var ordinal = n;
            destination[n++] = CreateGlyphSpriteRequest(cg, cx, cy, in style, sortKey, defN, ordinal);
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
        Vector2D<float> baselineLeftWorld,
        float pen,
        float sortKey)
    {
        // Callers skip empty strings; avoid a redundant branch the gate cannot hit.
        var span = text.AsSpan();

        var pool = ArrayPool<SpriteDrawRequest>.Shared;
        var buf = pool.Rent(span.Length);
        try
        {
            var dest = buf.AsSpan(0, span.Length);
            var n = FillGlyphRunSprites(renderer, fonts, cache, text, in style, baselineLeftWorld, pen, sortKey, dest,
                out var penAfter);
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
        Vector2D<float> baselineLeftWorld,
        float penStart,
        float penEnd,
        float sortKey,
        TextureId whiteTex,
        TextureId defNormal) =>
        AddDecorations(renderer, in style, baselineLeftWorld, penStart, penEnd, sortKey, whiteTex, defNormal);

    /// <summary>Approximate em advance when a glyph cannot be cached so the rest of the string still lays out.</summary>
    private static float FallbackAdvanceWhenGlyphUnavailable(in TextStyle style) =>
        MathF.Max(4f, style.SizePixels * 0.35f);

    private static SpriteDrawRequest CreateGlyphSpriteRequest(
        TextGlyphCache.CachedGlyph g,
        float centerWorldX,
        float centerWorldY,
        in TextStyle style,
        float sortKey,
        TextureId defNormal,
        int glyphOrdinalInRun)
    {
        var cm = style.Color;
        return new SpriteDrawRequest
        {
            CenterWorld = new Vector2D<float>(centerWorldX, centerWorldY),
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
            Transparent = true
        };
    }

    private static void AddDecorations(
        IRenderer renderer,
        in TextStyle style,
        Vector2D<float> baselineLeftWorld,
        float penStart,
        float penEnd,
        float sortKey,
        TextureId whiteTex,
        TextureId defNormal)
    {
        if (penEnd <= penStart)
            return;

        var cm = style.Color;
        var lineHalfH = MathF.Max(1f, style.SizePixels * 0.06f);
        var underlineY = baselineLeftWorld.Y - MathF.Max(1.5f, style.SizePixels * 0.12f);
        var strikeY = baselineLeftWorld.Y + style.SizePixels * 0.08f;

        if (style.Underline)
            SubmitLine(renderer, baselineLeftWorld.X + penStart, baselineLeftWorld.X + penEnd, underlineY, lineHalfH,
                cm, sortKey + 0.1f, whiteTex, defNormal);

        if (style.Strikethrough)
            SubmitLine(renderer, baselineLeftWorld.X + penStart, baselineLeftWorld.X + penEnd, strikeY, lineHalfH,
                cm, sortKey + 0.15f, whiteTex, defNormal);
    }

    private static void SubmitLine(
        IRenderer renderer,
        float worldXStart,
        float worldXEnd,
        float worldY,
        float halfHeight,
        Vector4D<float> color,
        float sortKey,
        TextureId whiteTex,
        TextureId defN)
    {
        var midX = (worldXStart + worldXEnd) * 0.5f;
        var halfW = MathF.Max(0.5f, (worldXEnd - worldXStart) * 0.5f);
        var req = new SpriteDrawRequest
        {
            CenterWorld = new Vector2D<float>(midX, worldY),
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
            Transparent = true
        };
        renderer.SubmitSprite(in req);
    }
}
