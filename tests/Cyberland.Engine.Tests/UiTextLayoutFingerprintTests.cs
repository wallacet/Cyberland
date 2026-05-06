using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;

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
            100f,
            0f,
            0f);
        var b = UiTextLayoutEngine.ComputeFingerprint(
            "x",
            new TextStyle("f", 14f, new Vector4D<float>(0f, 1f, 0f, 1f)),
            null,
            100f,
            0f,
            0f);
        Assert.NotEqual(a, b);

        var c = UiTextLayoutEngine.ComputeFingerprint(
            "x",
            new TextStyle("f", 14f, new Vector4D<float>(1f, 0f, 0f, 1f), Bold: true),
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
        var a = UiTextLayoutEngine.ComputeFingerprint("", default, runsA, 200f, 0f, 0f);
        var b = UiTextLayoutEngine.ComputeFingerprint("", default, runsB, 200f, 0f, 0f);
        Assert.NotEqual(a, b);
    }
}
