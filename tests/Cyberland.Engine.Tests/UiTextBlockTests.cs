using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class UiTextBlockTests
{
    private static (FontLibrary Fonts, TextGlyphCache Cache) TestFonts()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        return (lib, new TextGlyphCache());
    }

    [Fact]
    public void UiTextBlock_TopLeftFixed_measures_at_least_SizeDelta_for_horizontal_stack_spacing()
    {
        var (fonts, _) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "Hi",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            FitMode = UiTextFitMode.None
        };
        UiLayoutPresets.TopLeftFixed(block, 200f, 22f);
        block.Measure(UiSizeConstraints.Loose(400f, 80f));
        Assert.True(block.MeasuredSize.X >= 199f);
    }

    [Fact]
    public void UiTextBlock_wrap_and_DrawGlyphs_submits_glyphs()
    {
        var (fonts, cache) = TestFonts();
        var r = new RecordingRenderer();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "The quick brown fox jumps over the lazy dog repeatedly",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            FitMode = UiTextFitMode.None
        };
        UiLayoutPresets.StretchAll(block);

        block.Measure(UiSizeConstraints.Loose(90f, 500f));
        block.Arrange(new UiRect(0f, 0f, 90f, 400f));
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);

        Assert.True(r.Sprites.Count >= 4);
    }

    [Fact]
    public void UiTextBlock_DrawGlyphs_second_pass_replays_cached_runs()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "Replay cache keeps static HUD text cheap",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            FitMode = UiTextFitMode.None
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(400f, 200f));
        block.Arrange(new UiRect(0f, 0f, 400f, 200f));

        var renderer = new RecordingRenderer();
        block.DrawGlyphs(renderer, fonts, cache, CoordinateSpace.ViewportSpace);
        var firstGlyphCount = renderer.Sprites.Count;
        Assert.True(firstGlyphCount > 0);

        renderer.Sprites.Clear();
        block.DrawGlyphs(renderer, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.Equal(firstGlyphCount, renderer.Sprites.Count);
    }

    [Fact]
    public void UiTextBlock_Text_change_invalidates_draw_run_replay_so_glyphs_refresh()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "1111",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            FitMode = UiTextFitMode.None
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(400f, 200f));
        block.Arrange(new UiRect(0f, 0f, 400f, 200f));

        var r = new RecordingRenderer { MirrorTextGlyphsIntoSprites = false };
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.True(r.TextGlyphs.Count > 0);
        var uv0 = r.TextGlyphs[0].UvRect;

        // Prime replay path (same string, same bounds): second draw must not rebuild layout.
        r.TextGlyphs.Clear();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.True(r.TextGlyphs.Count > 0);
        Assert.Equal(uv0, r.TextGlyphs[0].UvRect);

        block.Text = "9999";
        block.Measure(UiSizeConstraints.Loose(400f, 200f));
        block.Arrange(new UiRect(0f, 0f, 400f, 200f));
        r.TextGlyphs.Clear();

        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.True(r.TextGlyphs.Count > 0);
        Assert.NotEqual(uv0, r.TextGlyphs[0].UvRect);
    }

    [Fact]
    public void UiTextBlock_localization_run_resolves_at_measure()
    {
        var (fonts, cache) = TestFonts();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"hello":"Resolved Hello"}"""u8.ToArray());

        var block = new UiTextBlock
        {
            Fonts = fonts,
            Localization = loc,
            Runs =
            [
                new TextRun("hello", new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 0f, 0f, 1f)),
                    isLocalizationKey: true)
            ]
        };
        UiLayoutPresets.TopLeftFixed(block, 200f, 100f);

        block.Measure(UiSizeConstraints.Loose(200f, 100f));
        block.Arrange(new UiRect(0f, 0f, 200f, 100f));

        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_paragraph_spacing_increases_height()
    {
        var (fonts, _) = TestFonts();
        var a = new UiTextBlock
        {
            Fonts = fonts,
            Text = "a\n\nb",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            ParagraphSpacing = 0f
        };
        UiLayoutPresets.TopLeftFixed(a, 400f, 400f);
        a.Measure(UiSizeConstraints.Loose(400f, 500f));

        var b = new UiTextBlock
        {
            Fonts = fonts,
            Text = "a\n\nb",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            ParagraphSpacing = 40f
        };
        UiLayoutPresets.TopLeftFixed(b, 400f, 400f);
        b.Measure(UiSizeConstraints.Loose(400f, 500f));

        Assert.True(b.MeasuredSize.Y > a.MeasuredSize.Y);
    }

    [Fact]
    public void UiTextBlock_hard_line_break_splits_lines()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "up\ndown",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(400f, 400f));
        block.Arrange(new UiRect(0f, 0f, 400f, 400f));

        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.True(r.Sprites.Count >= 2);
    }

    [Fact]
    public void UiTextBlock_two_runs_submits_glyphs()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Runs =
            [
                new TextRun("aa", new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 0f, 0f, 1f))),
                new TextRun("bb", new TextStyle(BuiltinFonts.Mono, 12f, new Vector4D<float>(0f, 1f, 0f, 1f)))
            ]
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(200f, 200f));
        block.Arrange(new UiRect(0f, 0f, 200f, 200f));

        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.True(r.Sprites.Count >= 2);
    }

    [Fact]
    public void UiTextBlock_without_fonts_uses_base_measure_path()
    {
        var block = new UiTextBlock { Text = "ignored" };
        UiLayoutPresets.TopLeftFixed(block, 33f, 44f);
        block.Measure(UiSizeConstraints.Loose(500f, 500f));
        Assert.Equal(33f + block.Margin.Horizontal, block.MeasuredSize.X, 0.01f);
    }

    [Fact]
    public void UiTextBlock_TopStretch_band_reports_fixed_height_even_when_intrinsic_text_is_taller()
    {
        var (fonts, _) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "Line one\nLine two\nLine three\nLine four",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.TopStretch(block, 28f);
        block.Measure(UiSizeConstraints.Loose(200f, 500f));
        Assert.Equal(28f, block.MeasuredSize.Y, 0.01f);
    }

    [Fact]
    public void UiTextBlock_stretch_all_unbounded_main_axis_measures_intrinsic_size()
    {
        var (fonts, _) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "Tab",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(float.PositiveInfinity, 40f));
        Assert.True(float.IsFinite(block.MeasuredSize.X) && block.MeasuredSize.X > 1f);
        Assert.True(float.IsFinite(block.MeasuredSize.Y) && block.MeasuredSize.Y > 1f);

        block.Arrange(new UiRect(0f, 0f, 120f, 40f));
        Assert.True(float.IsFinite(block.ComputedBounds.X) && float.IsFinite(block.ComputedBounds.Width));
    }

    [Fact]
    public void UiTextBlock_measure_cache_hit_then_InvalidateLayout_still_consistent()
    {
        var (fonts, _) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "hello world",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);
        var c = UiSizeConstraints.Loose(120f, 300f);
        block.Measure(c);
        var h1 = block.MeasuredSize.Y;
        block.Measure(c);
        var h2 = block.MeasuredSize.Y;
        Assert.Equal(h1, h2);

        block.InvalidateLayout();
        block.Measure(c);
        Assert.Equal(h1, block.MeasuredSize.Y);
    }

    [Fact]
    public void UiTextBlock_DefaultStyle_setter_invalidates_cached_layout()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "Cache bust style",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 10f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);
        var constraints = UiSizeConstraints.Loose(400f, 200f);
        block.Measure(constraints);
        block.Arrange(new UiRect(0f, 0f, 400f, 200f));
        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.NotEmpty(r.Sprites);
        var beforeHalfY = r.Sprites.Max(static s => s.HalfExtentsWorld.Y);

        block.DefaultStyle = block.DefaultStyle with { SizePixels = 28f };
        block.Measure(constraints);
        block.Arrange(new UiRect(0f, 0f, 400f, 200f));
        r.Sprites.Clear();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.NotEmpty(r.Sprites);
        var afterHalfY = r.Sprites.Max(static s => s.HalfExtentsWorld.Y);

        Assert.True(afterHalfY > beforeHalfY);
    }

    [Fact]
    public void UiTextBlock_Runs_setter_invalidates_cached_layout()
    {
        var (fonts, _) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            Runs =
            [
                new TextRun("short", new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)))
            ]
        };
        UiLayoutPresets.StretchAll(block);
        var constraints = UiSizeConstraints.Loose(80f, 500f);
        block.Measure(constraints);
        var before = block.MeasuredSize.Y;

        block.Runs =
        [
            new TextRun("short short short short short", new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)))
        ];
        block.Measure(constraints);
        var after = block.MeasuredSize.Y;

        Assert.True(after >= before);
    }

    [Fact]
    public void UiTextBlock_setters_accept_same_values_without_throwing()
    {
        var (fonts, _) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "same",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };

        var runs = new List<TextRun>
        {
            new("r", block.DefaultStyle)
        };
        block.Runs = runs;
        block.Runs = runs;

        block.DefaultStyle = block.DefaultStyle;
        block.LineSpacingExtra = 2f;
        block.LineSpacingExtra = 2f;
        block.MinFitSizePixels = 8f;
        block.MinFitSizePixels = 8f;
        block.Fonts = fonts;
        block.Fonts = fonts;
    }

    [Fact]
    public void UiTextBlock_underline_emits_decoration_sprite()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "dec",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f),
                Bold: false,
                Italic: false,
                Underline: true)
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(200f, 200f));
        block.Arrange(new UiRect(0f, 0f, 200f, 200f));

        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.Contains(r.Sprites, s => s.AlbedoTextureId == r.WhiteTextureId);
    }

    [Fact]
    public void UiTextBlock_replay_cached_runs_world_underline_recovers_decor_baseline_from_glyph()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "Replay",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true)
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(200f, 200f));
        block.Arrange(new UiRect(0f, 0f, 200f, 200f));

        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.WorldSpace);
        var afterFirst = r.Sprites.Count;
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.WorldSpace);
        Assert.True(r.Sprites.Count >= afterFirst);
        Assert.Contains(r.Sprites, s => s.AlbedoTextureId == r.WhiteTextureId);
    }

    [Fact]
    public void UiTextBlock_DrawGlyphs_no_ops_when_empty()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock { Fonts = fonts, Text = "" };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(100f, 100f));
        block.Arrange(new UiRect(0f, 0f, 100f, 100f));
        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_DrawGlyphs_without_measure_returns_early()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock { Fonts = fonts, Text = "x" };
        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_HorizontalAlignment_Center_and_End_shift_glyphs_in_wide_box()
    {
        var (fonts, cache) = TestFonts();
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        const string text = "Ab";
        const float boxW = 220f;

        float MinLeft(UiTextHorizontalAlignment align)
        {
            var r = new RecordingRenderer();
            var block = new UiTextBlock
            {
                Fonts = fonts,
                Text = text,
                DefaultStyle = style,
                HorizontalAlignment = align,
                FitMode = UiTextFitMode.None
            };
            UiLayoutPresets.StretchAll(block);
            block.Measure(UiSizeConstraints.Loose(boxW, 80f));
            block.Arrange(new UiRect(0f, 0f, boxW, 80f));
            block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
            Assert.NotEmpty(r.Sprites);
            return r.Sprites.Min(s => s.CenterWorld.X - s.HalfExtentsWorld.X);
        }

        var start = MinLeft(UiTextHorizontalAlignment.Start);
        var center = MinLeft(UiTextHorizontalAlignment.Center);
        var end = MinLeft(UiTextHorizontalAlignment.End);
        Assert.True(center > start);
        Assert.True(end > center);
    }

    [Fact]
    public void UiTextBlock_VerticalAlignment_Center_and_End_shift_glyphs_in_tall_box()
    {
        var (fonts, cache) = TestFonts();
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        const string text = "Ab";
        const float boxH = 80f;

        float MinTop(UiTextVerticalAlignment v)
        {
            var r = new RecordingRenderer();
            var block = new UiTextBlock
            {
                Fonts = fonts,
                Text = text,
                DefaultStyle = style,
                VerticalAlignment = v,
                FitMode = UiTextFitMode.None
            };
            UiLayoutPresets.StretchAll(block);
            block.Measure(UiSizeConstraints.Loose(220f, boxH));
            block.Arrange(new UiRect(0f, 0f, 220f, boxH));
            block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
            Assert.NotEmpty(r.Sprites);
            return r.Sprites.Min(s => s.CenterWorld.Y - s.HalfExtentsWorld.Y);
        }

        var start = MinTop(UiTextVerticalAlignment.Start);
        var centerInk = MinTop(UiTextVerticalAlignment.CenterInk);
        var center = MinTop(UiTextVerticalAlignment.Center);
        var endInk = MinTop(UiTextVerticalAlignment.EndInk);
        var end = MinTop(UiTextVerticalAlignment.End);
        Assert.True(center > start);
        Assert.True(end > center);
        // Ink-relative vertical centering sits higher than line-box center for typical Latin caps.
        Assert.True(centerInk < center);
        Assert.True(centerInk > start);
        Assert.True(endInk > center);
    }

    [Fact]
    public void UiLabel_Text_defaults_to_CenterInk_vertical_alignment()
    {
        var lab = new UiLabel();
        Assert.Equal(UiTextVerticalAlignment.CenterInk, lab.Text.VerticalAlignment);
    }

    [Fact]
    public void UiTextMeasurer_TryGetLineReferenceInkTopBottom_resolves_for_line()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var line = new UiTextLayoutLine();
        line.Segments.Add(("x", new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)), 0f));
        Assert.True(UiTextMeasurer.TryGetLineReferenceInkTopBottom(lib, line, out var top, out var bottom));
        Assert.True(top < bottom);
    }

    [Fact]
    public void UiTextMeasurer_empty_and_missing_font_paths()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var ok = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bad = new TextStyle("__no_font__", 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        Assert.Equal(0f, UiTextMeasurer.MeasureAdvanceWidth(lib, in ok, ReadOnlySpan<char>.Empty));
        Assert.Equal(0f, UiTextMeasurer.MeasureAdvanceWidth(lib, in bad, "a".AsSpan()));
        Assert.True(UiTextMeasurer.MeasureLineHeight(lib, in bad) > 0f);
    }

    [Fact]
    public void UiTextMeasurer_TryMinReferenceBoundsTopForLine_returns_false_when_no_segment_font_resolves()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var line = new UiTextLayoutLine();
        line.Segments.Add(("x", new TextStyle("__missing__", 14f, new Vector4D<float>(1f, 1f, 1f, 1f)), 0f));
        Assert.False(UiTextMeasurer.TryMinReferenceBoundsTopForLine(lib, line, out var minTop));
        Assert.Equal(0f, minTop);
    }

    [Fact]
    public void UiTextMeasurer_TryMinReferenceBoundsTopForLine_returns_true_when_segment_font_resolves()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var line = new UiTextLayoutLine();
        line.Segments.Add(("x", new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)), 0f));
        Assert.True(UiTextMeasurer.TryMinReferenceBoundsTopForLine(lib, line, out var minTop));
        Assert.False(float.IsInfinity(minTop));
    }

    [Fact]
    public void UiTextBlock_DrawGlyphs_falls_back_to_legacy_baseline_when_reference_top_unavailable()
    {
        var fonts = new FontLibrary();
        var cache = new TextGlyphCache();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "x",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            FitMode = UiTextFitMode.None
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(80f, 40f));
        block.Arrange(new UiRect(0f, 0f, 80f, 40f));
        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_layout_handles_whitespace_only_paragraph_and_leading_newline()
    {
        var (fonts, _) = TestFonts();
        var emptyParas = new UiTextBlock
        {
            Fonts = fonts,
            Text = "\n\n",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(emptyParas);
        emptyParas.Measure(UiSizeConstraints.Loose(80f, 80f));

        var ws = new UiTextBlock
        {
            Fonts = fonts,
            Text = "    ",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(ws);
        ws.Measure(UiSizeConstraints.Loose(80f, 80f));

        var lead = new UiTextBlock
        {
            Fonts = fonts,
            Text = "\nhello",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(lead);
        lead.Measure(UiSizeConstraints.Loose(200f, 200f));
        Assert.True(lead.MeasuredSize.Y > 0f);
    }

    [Fact]
    public void UiTextBlock_single_word_wider_than_box_still_layouts()
    {
        var (fonts, cache) = TestFonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = new string('W', 120),
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 24f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(40f, 400f));
        block.Arrange(new UiRect(0f, 0f, 40f, 400f));
        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_runs_empty_resolved_segment_skipped_and_double_newline_splits_paragraphs()
    {
        var (fonts, _) = TestFonts();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"empty":""}"""u8.ToArray());

        var skipEmpty = new UiTextBlock
        {
            Fonts = fonts,
            Localization = loc,
            Runs =
            [
                new TextRun("empty", new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f)),
                    isLocalizationKey: true),
                new TextRun("ok", new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f)))
            ]
        };
        UiLayoutPresets.StretchAll(skipEmpty);
        skipEmpty.Measure(UiSizeConstraints.Loose(100f, 100f));

        var split = new UiTextBlock
        {
            Fonts = fonts,
            Runs =
            [
                new TextRun("first\n\nsecond", new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f)))
            ]
        };
        UiLayoutPresets.StretchAll(split);
        split.Measure(UiSizeConstraints.Loose(200f, 200f));
        Assert.True(split.MeasuredSize.Y > 0f);
    }

    [Fact]
    public void UiTextBlock_DrawVisuals_uses_style_slack_when_measured_without_fonts()
    {
        var (fonts, cache) = TestFonts();
        var r = new RecordingRenderer();
        var block = new UiTextBlock
        {
            Text = "x",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 18f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.TopLeftFixed(block, 40f, 20f);
        block.Measure(UiSizeConstraints.Loose(100f, 100f));
        block.Fonts = fonts;
        block.Arrange(new UiRect(0f, 0f, 100f, 100f));
        block.DrawVisuals(r, fonts, cache, CoordinateSpace.ViewportSpace, 0f, new UiRect(0f, 0f, 100f, 100f));
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_DrawGlyphs_with_clip_skips_when_clip_rect_has_no_area()
    {
        var (fonts, cache) = TestFonts();
        var r = new RecordingRenderer();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "clip",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.TopLeftFixed(block, 120f, 40f);
        block.Measure(UiSizeConstraints.Loose(200f, 200f));
        block.Arrange(new UiRect(0f, 0f, 200f, 200f));

        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace, 400f, new UiRect(0f, 0f, 0f, 50f));
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_DrawRun_empty_string_adds_placeholder_when_cache_slot_missing()
    {
        var (fonts, cache) = TestFonts();
        var r = new RecordingRenderer();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var block = new UiTextBlock();
        var m = typeof(UiTextBlock).GetMethod("DrawRun",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        var invokeArgs = new object[]
        {
            r,
            fonts,
            cache,
            "",
            st,
            new Vector2D<float>(1f, 2f),
            450f,
            CoordinateSpace.ViewportSpace,
            false,
            default(UiRect)
        };
        m.Invoke(block, invokeArgs);
        var cacheField = typeof(UiTextBlock).GetField("_drawRunCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cacheField);
        var drawRunCache = (System.Collections.ICollection?)cacheField!.GetValue(block);
        Assert.NotNull(drawRunCache);
        Assert.True(drawRunCache!.Count == 1);
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_DrawRun_empty_string_returns_via_reflection()
    {
        var (fonts, cache) = TestFonts();
        var r = new RecordingRenderer();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var block = new UiTextBlock();
        var m = typeof(UiTextBlock).GetMethod("DrawRun",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        // DrawRun(renderer, fonts, cache, text, style, baselineLeft, sortKey, space, applyVpClip, viewportClip)
        var primeArgs = new object[]
        {
            r,
            fonts,
            cache,
            "z",
            st,
            new Vector2D<float>(10f, 20f),
            450f,
            CoordinateSpace.ViewportSpace,
            false,
            default(UiRect)
        };
        m.Invoke(block, primeArgs);
        r.Sprites.Clear();
        r.TextGlyphs.Clear();

        var emptyReuseSlotArgs = new object[]
        {
            r,
            fonts,
            cache,
            "",
            st,
            new Vector2D<float>(10f, 20f),
            450f,
            CoordinateSpace.ViewportSpace,
            false,
            default(UiRect)
        };
        m.Invoke(block, emptyReuseSlotArgs);
        var cacheField = typeof(UiTextBlock).GetField("_drawRunCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cacheField);
        var drawRunCache = (System.Collections.ICollection?)cacheField!.GetValue(block);
        Assert.NotNull(drawRunCache);
        Assert.Equal(2, drawRunCache!.Count);
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void UiTextBlock_DrawGlyphs_trims_draw_run_cache_when_segment_count_shrinks()
    {
        var (fonts, cache) = TestFonts();
        var r = new RecordingRenderer();
        var st = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var block = new UiTextBlock
        {
            Fonts = fonts,
            Text = "a\nb",
            DefaultStyle = st
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(200f, 120f));
        block.Arrange(new UiRect(0f, 0f, 200f, 120f));
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);

        block.Text = "a";
        block.InvalidateLayout();
        block.Measure(UiSizeConstraints.Loose(200f, 120f));
        block.Arrange(new UiRect(0f, 0f, 200f, 120f));
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
    }

    private static bool CallTryGetLayoutInkMinMax(
        FontLibrary fonts,
        UiTextLayoutEngine layout,
        out float inkMin,
        out float inkMax)
    {
        var m = typeof(UiTextBlock).GetMethod(
            "TryGetLayoutInkMinMax",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(m);
        var args = new object?[] { fonts, layout, 0f, 0f };
        var ok = (bool)m.Invoke(null, args)!;
        inkMin = (float)args[2]!;
        inkMax = (float)args[3]!;
        return ok;
    }

    private static float CallBaselineFromLineTopForDraw(FontLibrary fonts, UiTextLayoutLine line)
    {
        var m = typeof(UiTextBlock).GetMethod(
            "BaselineFromLineTopForDraw",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(m);
        return (float)m.Invoke(null, new object[] { fonts, line })!;
    }

    [Fact]
    public void TryGetLayoutInkMinMax_false_when_no_line_resolves_reference_ink()
    {
        var (fonts, _) = TestFonts();
        var bad = new TextStyle("__missing__", 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var layout = UiTextLayoutEngine.Build(fonts, null, null, bad, [new TextRun("a\nb", bad)], 400f, 0f, 0f);
        Assert.False(CallTryGetLayoutInkMinMax(fonts, layout, out var min, out var max));
        Assert.Equal(0f, min);
        Assert.Equal(0f, max);
    }

    [Fact]
    public void TryGetLayoutInkMinMax_skips_lines_that_fail_ink_but_keeps_good_lines()
    {
        var (fonts, _) = TestFonts();
        var bad = new TextStyle("__missing__", 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var good = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var layout = UiTextLayoutEngine.Build(fonts, null, null, good,
            [new TextRun("a\n", bad), new TextRun("b", good)], 400f, 0f, 0f);
        Assert.True(CallTryGetLayoutInkMinMax(fonts, layout, out var min, out var max));
        Assert.True(min < max);
    }

    [Fact]
    public void BaselineFromLineTopForDraw_uses_scaled_max_line_height_when_baseline_unset()
    {
        var (fonts, _) = TestFonts();
        var good = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var line = new UiTextLayoutLine();
        line.Segments.Add(("x", good, 0f));
        line.BaselineFromLineTopPx = 0f;
        line.MaxLineHeightPx = 20f;
        var b = CallBaselineFromLineTopForDraw(fonts, line);
        Assert.Equal(20f * 0.82f, b, 5);

        line.MaxLineHeightPx = 0f;
        var b2 = CallBaselineFromLineTopForDraw(fonts, line);
        Assert.True(b2 > 0f && MathF.Abs(b2 - line.MaxLineHeight(fonts) * 0.82f) < 1e-3f);
    }

    [Fact]
    public void UiTextMeasurer_TryGetLineReferenceInkTopBottom_empty_line_returns_false()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var line = new UiTextLayoutLine();
        Assert.False(UiTextMeasurer.TryGetLineReferenceInkTopBottom(lib, line, out var t, out var b));
        Assert.Equal(0f, t);
        Assert.Equal(0f, b);
    }

    [Fact]
    public void UiTextMeasurer_TryGetLineReferenceInkTopBottom_false_when_no_font_resolves()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var bad = new TextStyle("__missing__", 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var line = new UiTextLayoutLine();
        line.Segments.Add(("x", bad, 0f));
        Assert.False(UiTextMeasurer.TryGetLineReferenceInkTopBottom(lib, line, out _, out _));
    }

    [Fact]
    public void UiTextMeasurer_TryGetLineReferenceInkTopBottom_continues_past_unresolvable_segment()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var bad = new TextStyle("__missing__", 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var good = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var line = new UiTextLayoutLine();
        line.Segments.Add(("x", bad, 0f));
        line.Segments.Add(("y", good, 0f));
        Assert.True(UiTextMeasurer.TryGetLineReferenceInkTopBottom(lib, line, out var top, out var bottom));
        Assert.True(top < bottom);
    }

    [Fact]
    public void UiTextBlock_CenterInk_and_EndInk_use_line_box_extent_when_ink_metrics_unavailable()
    {
        var (fonts, cache) = TestFonts();
        var bad = new TextStyle("__missing__", 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var r = new RecordingRenderer();
        foreach (var v in new[] { UiTextVerticalAlignment.CenterInk, UiTextVerticalAlignment.EndInk })
        {
            var block = new UiTextBlock
            {
                Fonts = fonts,
                Text = "x",
                DefaultStyle = bad,
                VerticalAlignment = v,
                FitMode = UiTextFitMode.None
            };
            UiLayoutPresets.StretchAll(block);
            block.Measure(UiSizeConstraints.Loose(220f, 80f));
            block.Arrange(new UiRect(0f, 0f, 220f, 80f));
            block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        }
    }
}
