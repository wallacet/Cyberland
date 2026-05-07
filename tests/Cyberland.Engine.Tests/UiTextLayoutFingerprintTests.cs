using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Localization;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;
using System.Text;

namespace Cyberland.Engine.Tests;

public sealed class UiTextLayoutFingerprintTests
{
    [Fact]
    public void ComputeFingerprint_includes_default_style_color_and_flags()
    {
        var a = UiTextLayoutEngine.ComputeFingerprint(
            "x",
            new TextStyle("f", 14f, new Vector4D<float>(1f, 0f, 0f, 1f)),
            null,
            null,
            100f,
            0f,
            0f);
        var b = UiTextLayoutEngine.ComputeFingerprint(
            "x",
            new TextStyle("f", 14f, new Vector4D<float>(0f, 1f, 0f, 1f)),
            null,
            null,
            100f,
            0f,
            0f);
        Assert.NotEqual(a, b);

        var c = UiTextLayoutEngine.ComputeFingerprint(
            "x",
            new TextStyle("f", 14f, new Vector4D<float>(1f, 0f, 0f, 1f), Bold: true),
            null,
            null,
            100f,
            0f,
            0f);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void ComputeFingerprint_includes_run_style_color()
    {
        var runsA = new List<TextRun>
        {
            new TextRun("hi", new TextStyle("f", 12f, new Vector4D<float>(1f, 0f, 0f, 1f)), false)
        };
        var runsB = new List<TextRun>
        {
            new TextRun("hi", new TextStyle("f", 12f, new Vector4D<float>(0f, 0f, 1f, 1f)), false)
        };
        var a = UiTextLayoutEngine.ComputeFingerprint("", default, runsA, null, 200f, 0f, 0f);
        var b = UiTextLayoutEngine.ComputeFingerprint("", default, runsB, null, 200f, 0f, 0f);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeFingerprint_includes_quantized_max_content_width()
    {
        var a = UiTextLayoutEngine.ComputeFingerprint(
            "x",
            new TextStyle("f", 14f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            null,
            null,
            100f,
            0f,
            0f);
        var b = UiTextLayoutEngine.ComputeFingerprint(
            "x",
            new TextStyle("f", 14f, new Vector4D<float>(1f, 1f, 1f, 1f)),
            null,
            null,
            100.5f,
            0f,
            0f);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeFingerprint_includes_paragraph_and_line_spacing()
    {
        var style = new TextStyle("f", 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var a = UiTextLayoutEngine.ComputeFingerprint("hello", style, null, null, 200f, 0f, 0f);
        var b = UiTextLayoutEngine.ComputeFingerprint("hello", style, null, null, 200f, 6f, 0f);
        var c = UiTextLayoutEngine.ComputeFingerprint("hello", style, null, null, 200f, 0f, 2f);

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void ComputeFingerprint_includes_run_localization_key_flag_and_content()
    {
        var style = new TextStyle("f", 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var a = UiTextLayoutEngine.ComputeFingerprint(
            "",
            default,
            [new TextRun("ui.key", style, true)],
            null,
            200f,
            0f,
            0f);
        var b = UiTextLayoutEngine.ComputeFingerprint(
            "",
            default,
            [new TextRun("ui.key", style, false)],
            null,
            200f,
            0f,
            0f);
        var c = UiTextLayoutEngine.ComputeFingerprint(
            "",
            default,
            [new TextRun("ui.other", style, true)],
            null,
            200f,
            0f,
            0f);

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void ComputeFingerprint_localization_keys_hash_resolved_text()
    {
        var style = new TextStyle("f", 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var locA = new LocalizationManager();
        var locB = new LocalizationManager();
        locA.MergeJson(Encoding.UTF8.GetBytes("""
                                              { "ui.key": "Hello" }
                                              """));
        locB.MergeJson(Encoding.UTF8.GetBytes("""
                                              { "ui.key": "Bonjour" }
                                              """));

        var a = UiTextLayoutEngine.ComputeFingerprint(
            "",
            default,
            [new TextRun("ui.key", style, true)],
            locA,
            200f,
            0f,
            0f);
        var b = UiTextLayoutEngine.ComputeFingerprint(
            "",
            default,
            [new TextRun("ui.key", style, true)],
            locB,
            200f,
            0f,
            0f);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void UiTextLayoutLine_MaxLineHeight_measures_when_cached_height_is_zero()
    {
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var line = new UiTextLayoutLine();
        line.Segments.Add(("hello", new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f)), 0f));

        var measured = line.MaxLineHeight(fonts);
        Assert.True(measured > 0f);
    }
}
