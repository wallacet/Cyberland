using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class UiTextFitTests
{
    private static (FontLibrary Fonts, TextGlyphCache Cache) Fonts()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        return (lib, new TextGlyphCache());
    }

    [Fact]
    public void ShrinkToFit_Box_reduces_font_until_height_fits_short_viewport()
    {
        var (fonts, _) = Fonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            FitMode = UiTextFitMode.ShrinkToFit,
            FitTarget = UiTextFitTarget.Box,
            MinFitSizePixels = 6f,
            Text =
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore.",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 28f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);

        block.Measure(UiSizeConstraints.Loose(140f, 48f));

        Assert.True(block.MeasuredSize.Y <= 48.01f);
    }

    [Fact]
    public void ShrinkToFit_WidthOnly_shrinks_unbreakable_word_for_narrow_column()
    {
        var (fonts, _) = Fonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            FitMode = UiTextFitMode.ShrinkToFit,
            FitTarget = UiTextFitTarget.WidthOnly,
            Text = new string('M', 80),
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 36f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);

        block.Measure(UiSizeConstraints.Loose(90f, 400f));

        Assert.True(block.MeasuredSize.X <= 90.01f);
    }

    [Fact]
    public void ShrinkToFit_when_nominal_already_fits_keeps_quantized_nominal_size()
    {
        var (fonts, _) = Fonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            FitMode = UiTextFitMode.ShrinkToFit,
            Text = "hello",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(400f, 400f));

        var plain = new UiTextBlock
        {
            Fonts = fonts,
            FitMode = UiTextFitMode.None,
            Text = "hello",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(plain);
        plain.Measure(UiSizeConstraints.Loose(400f, 400f));

        Assert.True(Math.Abs(plain.MeasuredSize.Y - block.MeasuredSize.Y) < 1f);
    }

    [Fact]
    public void ShrinkToFit_swaps_quant_bounds_when_MinFit_exceeds_nominal()
    {
        var (fonts, _) = Fonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            FitMode = UiTextFitMode.ShrinkToFit,
            FitTarget = UiTextFitTarget.Box,
            MinFitSizePixels = 40f,
            Text = "tiny",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(200f, 200f));
        Assert.True(block.MeasuredSize.Y > 0f);
    }

    [Fact]
    public void ShrinkToFit_with_runs_uses_largest_run_size_as_reference()
    {
        var (fonts, _) = Fonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            FitMode = UiTextFitMode.ShrinkToFit,
            FitTarget = UiTextFitTarget.Box,
            MinFitSizePixels = 6f,
            Runs =
            [
                new TextRun("a", new TextStyle(BuiltinFonts.UiSans, 10f, new Vector4D<float>(1f, 1f, 1f, 1f))),
                new TextRun("b", new TextStyle(BuiltinFonts.Mono, 32f, new Vector4D<float>(1f, 1f, 1f, 1f)))
            ]
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(80f, 40f));
        Assert.True(block.MeasuredSize.Y <= 40.01f);
    }

    [Fact]
    public void ShrinkToFit_extreme_small_viewport_returns_minimum_quantized_layout_without_throw()
    {
        var (fonts, cache) = Fonts();
        var block = new UiTextBlock
        {
            Fonts = fonts,
            FitMode = UiTextFitMode.ShrinkToFit,
            FitTarget = UiTextFitTarget.Box,
            MinFitSizePixels = 6f,
            Text = "overflow",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 48f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.StretchAll(block);
        block.Measure(UiSizeConstraints.Loose(12f, 12f));
        block.Arrange(new UiRect(0f, 0f, 12f, 12f));

        var r = new RecordingRenderer();
        block.DrawGlyphs(r, fonts, cache, CoordinateSpace.ViewportSpace);
        Assert.True(r.Sprites.Count >= 1);
    }
}
