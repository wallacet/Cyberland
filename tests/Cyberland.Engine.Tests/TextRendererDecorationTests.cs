using System.Collections.Generic;
using System.Linq;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class TextRendererDecorationTests
{
    private const float BaselineY = 220f;
    private static readonly Vector4D<float> White = new(1f, 1f, 1f, 1f);

    public static IEnumerable<object[]> BuiltinUnderlineCases()
    {
        // Common shipped builtin sizes across UI/mono, including bold style requests used by demo HUDs.
        yield return [BuiltinFonts.UiSans, 12f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 13f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 14f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 15f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 16f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 18f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 20f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 22f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 23f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 24f, false, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 14f, true, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 18f, true, false, "GAME OVER"];
        yield return [BuiltinFonts.UiSans, 23f, true, false, "GAME OVER"];
        yield return [BuiltinFonts.Mono, 14f, false, false, "SCORE 10"];
        yield return [BuiltinFonts.Mono, 18f, false, false, "SCORE 10"];
    }

    public static IEnumerable<object[]> BuiltinStrikethroughCases()
    {
        foreach (var c in BuiltinUnderlineCases())
            yield return c;
    }

    [Fact]
    public void SubmitTextDecorations_underline_without_font_library_uses_snapped_baseline_fallback()
    {
        var r = new RecordingRenderer();
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        TextRenderer.SubmitTextDecorations(
            r,
            in style,
            new Vector2D<float>(10.4f, 20.6f),
            0f,
            40f,
            1f,
            r.WhiteTextureId,
            r.DefaultNormalTextureId,
            CoordinateSpace.ViewportSpace,
            false,
            default);

        var u = r.Sprites.Single(s => s.AlbedoTextureId == r.WhiteTextureId);
        var baselineSnappedY = MathF.Round(20.6f);
        var fallbackCenterDown = MathF.Max(TextDecorationMetrics.FallbackUnderlineCenterDownMinPx,
            style.SizePixels * TextDecorationMetrics.FallbackUnderlineCenterDownEm);
        var afterFallback = baselineSnappedY + fallbackCenterDown;
        var minBelowBaseline = TextDecorationMetrics.ViewportUnderlineMinCenterBelowBaselinePx(style.SizePixels);
        Assert.Equal(MathF.Max(afterFallback, baselineSnappedY + minBelowBaseline), u.CenterWorld.Y, 4);
    }

    [Fact]
    public void SubmitTextDecorations_viewport_fallback_underline_ignores_glyph_quad_geometry()
    {
        var r = new RecordingRenderer();
        const float sizePx = 18f;
        var style = new TextStyle(BuiltinFonts.UiSans, sizePx, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        var baselineLeft = new Vector2D<float>(88f, 40f);
        var baselineSnappedY = MathF.Round(baselineLeft.Y);
        var fallbackCenterDown = MathF.Max(TextDecorationMetrics.FallbackUnderlineCenterDownMinPx,
            sizePx * TextDecorationMetrics.FallbackUnderlineCenterDownEm);

        TextRenderer.SubmitTextDecorations(
            r,
            in style,
            baselineLeft,
            0f,
            48f,
            0f,
            r.WhiteTextureId,
            r.DefaultNormalTextureId,
            CoordinateSpace.ViewportSpace,
            false,
            default,
            fonts: null);

        var u = r.Sprites.Single(s => s.AlbedoTextureId == r.WhiteTextureId);
        var afterFallback = baselineSnappedY + fallbackCenterDown;
        var minBelowBaseline = TextDecorationMetrics.ViewportUnderlineMinCenterBelowBaselinePx(sizePx);
        Assert.Equal(MathF.Max(afterFallback, baselineSnappedY + minBelowBaseline), u.CenterWorld.Y, 4);
    }

    [Fact]
    public void RecoverBaselineYFromGlyph_inverts_viewport_glyph_vertical_offset()
    {
        var g = new TextGlyphDrawRequest
        {
            Center = new Vector2D<float>(40f, 118f),
            OffsetPenToCenterYWorld = -10f,
            Space = CoordinateSpace.ViewportSpace
        };
        // cy = baseline + offset * (-1) => 118 = baseline + 10
        Assert.Equal(108f, TextRenderer.RecoverBaselineYFromGlyph(in g), 4);
    }

    [Fact]
    public void RecoverBaselineYFromGlyph_inverts_world_glyph_vertical_offset()
    {
        var g = new TextGlyphDrawRequest
        {
            Center = new Vector2D<float>(10f, 190f),
            OffsetPenToCenterYWorld = -10f,
            Space = CoordinateSpace.WorldSpace
        };
        Assert.Equal(200f, TextRenderer.RecoverBaselineYFromGlyph(in g), 4);
    }

    [Fact]
    public void SubmitTextDecorations_viewport_underline_guard_follows_tight_visible_band_when_quads_offset_from_baseline()
    {
        var r = new RecordingRenderer();
        const float sizePx = 20f;
        var style = new TextStyle(BuiltinFonts.UiSans, sizePx, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        // Baseline authored far above the emitted quads; fallback underline would sit near y≈14 without the guard.
        var baselineLeft = new Vector2D<float>(0f, 12f);
        ReadOnlySpan<TextGlyphDrawRequest> guard = stackalloc TextGlyphDrawRequest[1]
        {
            new TextGlyphDrawRequest
            {
                Center = new Vector2D<float>(20f, 110f),
                HalfExtents = new Vector2D<float>(10f, 22f),
                OffsetPenToCenterYWorld = -10f,
                Space = CoordinateSpace.ViewportSpace
            }
        };

        TextRenderer.SubmitTextDecorations(
            r,
            in style,
            baselineLeft,
            0f,
            40f,
            0f,
            r.WhiteTextureId,
            r.DefaultNormalTextureId,
            CoordinateSpace.ViewportSpace,
            false,
            default,
            fonts: null,
            guard);

        var u = r.Sprites.Single(s => s.AlbedoTextureId == r.WhiteTextureId);
        var inkTop = 110f - 22f;
        var inkBottom = 110f + 22f;
        ViewportUnderlinePlacementAssert.FollowsTightVisibleBandRules(in u, MathF.Round(baselineLeft.Y), sizePx, inkTop,
            inkBottom);
    }

    [Fact]
    public void SubmitTextDecorations_viewport_underline_ignores_ink_guard_when_glyph_space_mismatches()
    {
        var r = new RecordingRenderer();
        const float sizePx = 20f;
        var style = new TextStyle(BuiltinFonts.UiSans, sizePx, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        var baselineLeft = new Vector2D<float>(0f, 12f);
        ReadOnlySpan<TextGlyphDrawRequest> guard = stackalloc TextGlyphDrawRequest[1]
        {
            new TextGlyphDrawRequest
            {
                Center = new Vector2D<float>(20f, 110f),
                HalfExtents = new Vector2D<float>(10f, 22f),
                Space = CoordinateSpace.WorldSpace
            }
        };

        TextRenderer.SubmitTextDecorations(
            r,
            in style,
            baselineLeft,
            0f,
            40f,
            0f,
            r.WhiteTextureId,
            r.DefaultNormalTextureId,
            CoordinateSpace.ViewportSpace,
            false,
            default,
            fonts: null,
            guard);

        var u = r.Sprites.Single(s => s.AlbedoTextureId == r.WhiteTextureId);
        var baselineSnappedY = MathF.Round(baselineLeft.Y);
        var fallbackCenterDown = MathF.Max(TextDecorationMetrics.FallbackUnderlineCenterDownMinPx,
            sizePx * TextDecorationMetrics.FallbackUnderlineCenterDownEm);
        var afterFallback = baselineSnappedY + fallbackCenterDown;
        var minBelowBaseline = TextDecorationMetrics.ViewportUnderlineMinCenterBelowBaselinePx(sizePx);
        Assert.Equal(MathF.Max(afterFallback, baselineSnappedY + minBelowBaseline), u.CenterWorld.Y, 4);
    }

    [Fact]
    public void UnderlineGapBelowInkViewportPx_matches_preferred_when_ink_span_missing_or_non_positive()
    {
        var preferred = TextDecorationMetrics.ViewportUnderlineGapBelowInkPx(20f);
        Assert.Equal(preferred, TextDecorationMetrics.UnderlineGapBelowInkViewportPx(20f, 0f));
        Assert.Equal(preferred, TextDecorationMetrics.UnderlineGapBelowInkViewportPx(20f, -5f));
        Assert.Equal(preferred, TextDecorationMetrics.UnderlineGapBelowInkViewportPx(20f, float.NaN));
    }

    [Theory]
    [InlineData(44f, 5f)]
    [InlineData(12f, 2f)]
    [InlineData(100f, 10f)]
    [InlineData(9f, 1f)]
    public void MaxUnderlineGapBelowVisibleLinePx_is_ceil_tenth_height_with_one_px_floor(float visibleH, float expectedGap)
    {
        Assert.Equal(expectedGap, TextDecorationMetrics.MaxUnderlineGapBelowVisibleLinePx(visibleH), 3);
    }

    [Theory]
    [InlineData(20f, 1.2f, 21.2f)]
    [InlineData(10f, 0.6f, 11.6f)]
    public void ViewportUnderlineMinCenterClearBaselineDescenderInkPx_center_below_stroke_top_clearance(
        float sizePx,
        float underlineHalfH,
        float expectedCenterDeltaFromBaseline)
    {
        Assert.Equal(expectedCenterDeltaFromBaseline,
            TextDecorationMetrics.ViewportUnderlineMinCenterClearBaselineDescenderInkPx(sizePx, underlineHalfH), 3);
    }

    [Fact]
    public void MaxUnderlineGapBelowVisibleLinePx_non_positive_returns_legacy_min_px()
    {
        Assert.Equal(TextDecorationMetrics.ViewportUnderlineGapBelowInkMinPx,
            TextDecorationMetrics.MaxUnderlineGapBelowVisibleLinePx(0f));
        Assert.Equal(TextDecorationMetrics.ViewportUnderlineGapBelowInkMinPx,
            TextDecorationMetrics.MaxUnderlineGapBelowVisibleLinePx(float.NaN));
    }

    [Theory]
    [InlineData(44f)]
    [InlineData(12f)]
    public void UnderlineGapBelowInkViewportPx_positive_span_matches_max_gap_helper(float inkSpanPx)
    {
        Assert.Equal(TextDecorationMetrics.MaxUnderlineGapBelowVisibleLinePx(inkSpanPx),
            TextDecorationMetrics.UnderlineGapBelowInkViewportPx(16f, inkSpanPx), 3);
    }

    [Fact]
    public void EstimateTightVisibleInkBandViewport_collapsed_band_falls_back_to_full_quad()
    {
        // Baseline far above quads makes proposed top > proposed bottom; helper must return full quad band.
        TextDecorationMetrics.EstimateTightVisibleInkBandViewport(
            baselineYDown: 12f,
            sizePixels: 20f,
            inkMinTopVp: 88f,
            inkMaxBottomVp: 132f,
            out var top,
            out var bottom);

        Assert.Equal(88f, top, 3);
        Assert.Equal(132f, bottom, 3);
    }

    [Fact]
    public void ResolveViewportUnderlineCenterWithInkBand_invalid_ink_band_returns_preferred_center()
    {
        var resolved = TextDecorationMetrics.ResolveViewportUnderlineCenterWithInkBand(
            baselineYDown: 100f,
            sizePixels: 20f,
            preferredCenterY: 150f,
            underlineHalfHeightPx: 0.5f,
            inkMinTopVp: 20f,
            inkMaxBottomVp: 20f);
        Assert.Equal(150f, resolved, 3);
    }

    [Fact]
    public void ResolveViewportUnderlineCenterWithInkBand_nan_baseline_uses_fallback_visible_height_path()
    {
        // NaN baseline keeps finite ink inputs but produces NaN visibleHeight before fallback branch.
        var resolved = TextDecorationMetrics.ResolveViewportUnderlineCenterWithInkBand(
            baselineYDown: float.NaN,
            sizePixels: 20f,
            preferredCenterY: 102f,
            underlineHalfHeightPx: 0.5f,
            inkMinTopVp: 90f,
            inkMaxBottomVp: 110f);
        Assert.True(float.IsFinite(resolved));
    }

    [Fact]
    public void ResolveViewportUnderlineCenterWithInkBand_when_clearance_exceeds_max_gap_uses_clearance_floor()
    {
        // Very large size and tiny visible height => maxGap(1px) < clearance; method should clamp to clearance floor.
        var halfH = 0.5f;
        var resolved = TextDecorationMetrics.ResolveViewportUnderlineCenterWithInkBand(
            baselineYDown: 100f,
            sizePixels: 200f,
            preferredCenterY: 101f,
            underlineHalfHeightPx: halfH,
            inkMinTopVp: 100f,
            inkMaxBottomVp: 101f);
        var approxBottom = TextDecorationMetrics.ViewportUnderlineApproxInkBottomPx(100f, 200f, 101f);
        var expectedStrokeTop = approxBottom + TextDecorationMetrics.ViewportUnderlineMinInkClearancePx(200f);
        Assert.Equal(expectedStrokeTop + halfH, resolved, 3);
    }

    [Fact]
    public void ResolveViewportUnderlineCenterWithInkBand_non_finite_visible_height_executes_fallback_branch()
    {
        var resolved = TextDecorationMetrics.ResolveViewportUnderlineCenterWithInkBand(
            baselineYDown: 0f,
            sizePixels: 20f,
            preferredCenterY: 5f,
            underlineHalfHeightPx: 0.5f,
            inkMinTopVp: -float.MaxValue,
            inkMaxBottomVp: float.MaxValue);
        Assert.False(float.IsNaN(resolved));
    }

    [Fact]
    public void SubmitTextDecorations_viewport_underline_tall_msdf_quad_follows_visible_band_heuristic()
    {
        var r = new RecordingRenderer();
        const float sizePx = 20f;
        var style = new TextStyle(BuiltinFonts.UiSans, sizePx, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        // Baseline aligns with typical HUD; quad extends far below baseline like baked MSDF padding.
        var baselineLeft = new Vector2D<float>(0f, 100f);
        ReadOnlySpan<TextGlyphDrawRequest> guard = stackalloc TextGlyphDrawRequest[1]
        {
            new TextGlyphDrawRequest
            {
                Center = new Vector2D<float>(80f, 118f),
                HalfExtents = new Vector2D<float>(48f, 38f),
                OffsetPenToCenterYWorld = -18f,
                Space = CoordinateSpace.ViewportSpace
            }
        };

        TextRenderer.SubmitTextDecorations(
            r,
            in style,
            baselineLeft,
            0f,
            160f,
            0f,
            r.WhiteTextureId,
            r.DefaultNormalTextureId,
            CoordinateSpace.ViewportSpace,
            false,
            default,
            fonts: null,
            guard);

        var u = r.Sprites.Single(s => s.AlbedoTextureId == r.WhiteTextureId);
        var inkTop = 118f - 38f;
        var inkBottom = 118f + 38f;
        ViewportUnderlinePlacementAssert.FollowsTightVisibleBandRules(in u, MathF.Round(baselineLeft.Y), sizePx, inkTop,
            inkBottom);
    }

    [Fact]
    public void SubmitTextDecorations_viewport_underline_matches_visible_band_when_baseline_aligns_with_quads()
    {
        var r = new RecordingRenderer();
        const float sizePx = 20f;
        var style = new TextStyle(BuiltinFonts.UiSans, sizePx, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        var baselineLeft = new Vector2D<float>(0f, 100f);
        ReadOnlySpan<TextGlyphDrawRequest> guard = stackalloc TextGlyphDrawRequest[1]
        {
            new TextGlyphDrawRequest
            {
                Center = new Vector2D<float>(20f, 100f),
                HalfExtents = new Vector2D<float>(10f, 10f),
                OffsetPenToCenterYWorld = -10f,
                Space = CoordinateSpace.ViewportSpace
            }
        };

        TextRenderer.SubmitTextDecorations(
            r,
            in style,
            baselineLeft,
            0f,
            40f,
            0f,
            r.WhiteTextureId,
            r.DefaultNormalTextureId,
            CoordinateSpace.ViewportSpace,
            false,
            default,
            fonts: null,
            guard);

        var u = r.Sprites.Single(s => s.AlbedoTextureId == r.WhiteTextureId);
        ViewportUnderlinePlacementAssert.FollowsTightVisibleBandRules(in u, MathF.Round(baselineLeft.Y), sizePx, 90f, 110f);
    }

    [Fact]
    public void FontLibrary_TryGetOpenTypeTextDecorationLayout_builtin_reports_positive_underline_delta_down()
    {
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var style = new TextStyle(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 1f, 1f, 1f));
        Assert.True(fonts.TryGetOpenTypeTextDecorationLayout(in style, out var layout));
        Assert.True(layout.UnderlineCenterDeltaPositiveDownPx > 0f);
        Assert.Equal(-layout.UnderlineCenterDeltaPositiveDownPx, layout.UnderlineCenterOffsetPenFontUp);
        Assert.Equal(-layout.StrikethroughCenterDeltaPositiveDownPx, layout.StrikethroughCenterOffsetPenFontUp);
        Assert.InRange(layout.UnderlineThicknessPx, 0.5f, 8f);
        Assert.InRange(layout.StrikethroughThicknessPx, 0.5f, 8f);
        Assert.True(float.IsFinite(layout.StrikethroughCenterDeltaPositiveDownPx));
    }

    [Fact]
    public void FontLibrary_TryGetOpenTypeTextDecorationLayout_returns_false_when_family_missing()
    {
        var fonts = new FontLibrary();
        var style = new TextStyle("cyberland-no-such-font", 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        Assert.False(fonts.TryGetOpenTypeTextDecorationLayout(in style, out _));
    }

    [Fact]
    public void SubmitTextDecorations_strikethrough_without_fonts_uses_em_fraction_heuristic()
    {
        var r = new RecordingRenderer();
        const float sizePx = 20f;
        var style = new TextStyle(BuiltinFonts.UiSans, sizePx, new Vector4D<float>(1f, 1f, 1f, 1f), Strikethrough: true);
        TextRenderer.SubmitTextDecorations(
            r,
            in style,
            new Vector2D<float>(0f, 100f),
            0f,
            10f,
            0f,
            r.WhiteTextureId,
            r.DefaultNormalTextureId,
            CoordinateSpace.ViewportSpace,
            false,
            default,
            fonts: null);

        var strike = r.Sprites.Single(s => s.AlbedoTextureId == r.WhiteTextureId);
        var fallbackStrikeCenterDown = -sizePx * TextDecorationMetrics.FallbackStrikethroughFontUpEm;
        Assert.Equal(100f + fallbackStrikeCenterDown, strike.CenterWorld.Y, 4);
    }

    [Theory]
    [MemberData(nameof(BuiltinUnderlineCases))]
    public void DrawLiteralScreen_builtin_common_sizes_underline_respects_visible_band_heuristic(
        string family,
        float sizePx,
        bool bold,
        bool italic,
        string text)
    {
        var renderer = new RecordingRenderer { MirrorTextGlyphsIntoSprites = false };
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();
        var style = new TextStyle(family, sizePx, White, Bold: bold, Italic: italic, Underline: true);

        try
        {
            TextRenderer.DrawLiteralScreen(
                renderer,
                fonts,
                cache,
                in style,
                text,
                new Vector2D<float>(36f, BaselineY),
                sortKey: 450f);

            Assert.NotEmpty(renderer.TextGlyphs);
            var underline = Assert.Single(renderer.Sprites);
            var glyphTop = renderer.TextGlyphs.Min(g => g.Center.Y - g.HalfExtents.Y);
            var glyphBottom = renderer.TextGlyphs.Max(g => g.Center.Y + g.HalfExtents.Y);
            var baselineSnapped = MathF.Round(BaselineY);
            ViewportUnderlinePlacementAssert.FollowsTightVisibleBandRules(in underline, baselineSnapped, sizePx,
                glyphTop, glyphBottom);
        }
        finally
        {
            cache.Shutdown();
        }
    }

    [Theory]
    [MemberData(nameof(BuiltinStrikethroughCases))]
    public void DrawLiteralScreen_builtin_common_sizes_strikethrough_stays_near_glyph_center(
        string family,
        float sizePx,
        bool bold,
        bool italic,
        string text)
    {
        var renderer = new RecordingRenderer { MirrorTextGlyphsIntoSprites = false };
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();
        var style = new TextStyle(family, sizePx, White, Bold: bold, Italic: italic, Strikethrough: true);

        try
        {
            TextRenderer.DrawLiteralScreen(
                renderer,
                fonts,
                cache,
                in style,
                text,
                new Vector2D<float>(36f, BaselineY),
                sortKey: 450f);

            Assert.NotEmpty(renderer.TextGlyphs);
            var strike = Assert.Single(renderer.Sprites);
            var glyphTop = renderer.TextGlyphs.Min(g => g.Center.Y - g.HalfExtents.Y);
            var glyphBottom = renderer.TextGlyphs.Max(g => g.Center.Y + g.HalfExtents.Y);
            var glyphMid = (glyphTop + glyphBottom) * 0.5f;
            var distance = MathF.Abs(strike.CenterWorld.Y - glyphMid);
            var allowed = TextDecorationMetrics.StrikethroughMaxDeviationFromInkMidPx(sizePx);
            Assert.True(
                distance <= allowed + 1e-3f,
                $"family={family} size={sizePx} bold={bold} italic={italic} strikeY={strike.CenterWorld.Y} glyphMid={glyphMid} delta={distance} allowed={allowed}");
        }
        finally
        {
            cache.Shutdown();
        }
    }
}
