using System.Reflection;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
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
    public void UiTextBlock_DrawRun_empty_string_returns_via_reflection()
    {
        var (fonts, cache) = TestFonts();
        var r = new RecordingRenderer();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var m = typeof(UiTextBlock).GetMethod("DrawRun",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(m);
        m.Invoke(null,
        [
            r,
            fonts,
            cache,
            "",
            st,
            new Vector2D<float>(10f, 20f),
            450f,
            CoordinateSpace.ViewportSpace
        ]);
        Assert.Empty(r.Sprites);
    }
}
