using Cyberland.Demo.FontTest.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Ecs;
using Cyberland.Engine.UI.Layout;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;

namespace Cyberland.Demo.FontTest;

/// <summary>Retained UI matrix for FontTest; camera and root entity come from <c>Scenes/demo_fonttest.json</c>.</summary>
public sealed partial class Mod
{
    private static readonly Vector4D<float> JostSampleColor = new(0.98f, 0.82f, 0.52f, 1f);

    // Uppercase/lowercase, digits, punctuation, and whitespace samples.
    private const string Sample =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 !?.,:;+-=*/()[]{}<> \"'`~ @#$%^&|  \t  ";

    internal static void BuildFontTestUiDocument(ModLoadContext context)
    {
        var host = context.Host;
        var rootEntity = context.World.RequireSingleEntityWith<FontTestUiRootTag>("FontTest UI root");

        var doc = new UiDocument();
        doc.Root.BackgroundColor = new Vector4D<float>(0.04f, 0.06f, 0.12f, 1f);

        var scroll = new UiScrollView { WheelScrollPixels = 44f };
        UiLayoutPresets.StretchAll(scroll);
        scroll.Content.Margin = new UiThickness(18f, 16f, 22f, 20f);

        var col = new UiVerticalStack { Spacing = 10f };
        UiLayoutPresets.StretchAll(col);

        AddTitle(col,
            "Built-in font matrix (UiSans / Mono) — mouse wheel to scroll. One row per size/style/decoration.");

        foreach (var (style, label) in BuildBuiltinRowStyles())
            AddSampleRow(col, style, label);

        AddTitle(col,
            "Custom path: Jost (SIL OFL) — mod Content TTF + RegisterFamilyFromVirtualPathsAsync; MSDF atlases in Content/Fonts/Baked (not engine builtins). Includes size 17 (non-standard vs built-in grid).");

        foreach (var (style, label) in BuildJostRowStyles())
            AddSampleRow(col, style, label);

        scroll.Content.AddChild(col);
        doc.Root.AddChild(scroll);

        host.UiDocuments.Register(rootEntity, doc);
    }

    private static void AddTitle(UiVerticalStack col, string text)
    {
        var title = new UiTextBlock
        {
            Text = text,
            DefaultStyle = new TextStyle(
                BuiltinFonts.UiSans,
                15f,
                new Vector4D<float>(0.88f, 0.93f, 1f, 1f),
                Bold: true)
        };
        UiLayoutPresets.TopStretch(title, 52f);
        title.VerticalAlignment = UiTextVerticalAlignment.CenterInk;
        col.AddChild(title);
    }

    private static void AddSampleRow(UiVerticalStack col, in TextStyle style, string label)
    {
        var line = new UiTextBlock
        {
            Text = $"{label}: {Sample}",
            DefaultStyle = style
        };
        UiLayoutPresets.TopStretch(line, RowSlotHeight(style));
        line.VerticalAlignment = UiTextVerticalAlignment.Start;
        col.AddChild(line);
    }

    private static float RowSlotHeight(in TextStyle style)
    {
        var em = style.SizePixels;
        var h = em * 1.55f + 10f;
        if (style.Underline)
            h += 10f;
        if (style.Strikethrough)
            h += 8f;
        return MathF.Max(36f, MathF.Ceiling(h));
    }

    private static List<(TextStyle Style, string Label)> BuildBuiltinRowStyles()
    {
        var baseStyles = new List<(string Family, float Size, bool Bold, bool Italic, string Label)>
        {
            (BuiltinFonts.UiSans, 12f, false, false, "UiSans 12 Regular"),
            (BuiltinFonts.UiSans, 13f, false, false, "UiSans 13 Regular"),
            (BuiltinFonts.UiSans, 14f, false, false, "UiSans 14 Regular"),
            (BuiltinFonts.UiSans, 15f, false, false, "UiSans 15 Regular"),
            (BuiltinFonts.UiSans, 16f, false, false, "UiSans 16 Regular"),
            (BuiltinFonts.UiSans, 18f, false, false, "UiSans 18 Regular"),
            (BuiltinFonts.UiSans, 20f, false, false, "UiSans 20 Regular"),
            (BuiltinFonts.UiSans, 22f, false, false, "UiSans 22 Regular"),
            (BuiltinFonts.UiSans, 23f, false, false, "UiSans 23 Regular"),
            (BuiltinFonts.UiSans, 24f, false, false, "UiSans 24 Regular"),
            (BuiltinFonts.UiSans, 14f, true, false, "UiSans 14 Bold"),
            (BuiltinFonts.UiSans, 18f, true, false, "UiSans 18 Bold"),
            (BuiltinFonts.UiSans, 23f, true, false, "UiSans 23 Bold"),
            (BuiltinFonts.Mono, 14f, false, false, "Mono 14 Regular"),
            (BuiltinFonts.Mono, 18f, false, false, "Mono 18 Regular")
        };

        return ExpandDecorations(baseStyles, new Vector4D<float>(0.9f, 0.95f, 1f, 1f));
    }

    private static List<(TextStyle Style, string Label)> BuildJostRowStyles()
    {
        var baseStyles = new List<(string Family, float Size, bool Bold, bool Italic, string Label)>
        {
            (FontTestFonts.JostFamilyId, 12f, false, false, "Jost 12 Regular"),
            (FontTestFonts.JostFamilyId, 13f, false, false, "Jost 13 Regular"),
            (FontTestFonts.JostFamilyId, 14f, false, false, "Jost 14 Regular"),
            (FontTestFonts.JostFamilyId, 15f, false, false, "Jost 15 Regular"),
            (FontTestFonts.JostFamilyId, 16f, false, false, "Jost 16 Regular"),
            (FontTestFonts.JostFamilyId, 17f, false, false, "Jost 17 Regular"),
            (FontTestFonts.JostFamilyId, 18f, false, false, "Jost 18 Regular"),
            (FontTestFonts.JostFamilyId, 20f, false, false, "Jost 20 Regular"),
            (FontTestFonts.JostFamilyId, 22f, false, false, "Jost 22 Regular"),
            (FontTestFonts.JostFamilyId, 23f, false, false, "Jost 23 Regular"),
            (FontTestFonts.JostFamilyId, 24f, false, false, "Jost 24 Regular"),
            (FontTestFonts.JostFamilyId, 14f, true, false, "Jost 14 Bold"),
            (FontTestFonts.JostFamilyId, 18f, true, false, "Jost 18 Bold"),
            (FontTestFonts.JostFamilyId, 23f, true, false, "Jost 23 Bold")
        };

        return ExpandDecorations(baseStyles, JostSampleColor);
    }

    private static List<(TextStyle Style, string Label)> ExpandDecorations(
        List<(string Family, float Size, bool Bold, bool Italic, string Label)> baseStyles,
        Vector4D<float> color)
    {
        var decorations = new (bool Underline, bool Strike, string Suffix)[]
        {
            (false, false, "Plain"),
            (true, false, "Underline"),
            (false, true, "Strike"),
            (true, true, "Underline+Strike")
        };

        var result = new List<(TextStyle Style, string Label)>(baseStyles.Count * decorations.Length);
        foreach (var b in baseStyles)
        {
            foreach (var d in decorations)
            {
                var style = new TextStyle(
                    b.Family,
                    b.Size,
                    color,
                    Bold: b.Bold,
                    Italic: b.Italic,
                    Underline: d.Underline,
                    Strikethrough: d.Strike);
                result.Add((style, $"{b.Label} {d.Suffix}"));
            }
        }

        return result;
    }
}
