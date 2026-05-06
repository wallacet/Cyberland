using Cyberland.Engine.Rendering.Text;
using SixLabors.Fonts;

namespace Cyberland.Engine.UI.Text;

/// <summary>
/// Layout-only horizontal measurement using SixLabors <see cref="TextMeasurer"/> with the same DPI mapping as
/// <see cref="FontLibrary.CreateFontAtPixelSize"/>.
/// </summary>
internal static class UiTextMeasurer
{
    /// <summary>Sample string for line-height and baseline-top metrics (must stay aligned with <see cref="MeasureLineHeight"/>).</summary>
    internal const string LineHeightReferenceSample = "MgjpqyАЙ";

    /// <summary>Returns advance width in pixels for <paramref name="text"/> or 0 when the font cannot resolve.</summary>
    public static float MeasureAdvanceWidth(FontLibrary fonts, in TextStyle style, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0f;

        if (!fonts.TryCreateFont(in style, out var font, out _))
            return 0f;

        var opts = new TextOptions(font) { Dpi = 96f };
        var adv = TextMeasurer.MeasureAdvance(text.ToString(), opts);
        return adv.Width;
    }

    /// <summary>Approximate line height for one line (ascenders + descenders sample).</summary>
    public static float MeasureLineHeight(FontLibrary fonts, in TextStyle style)
    {
        if (!fonts.TryCreateFont(in style, out var font, out _))
            return MathF.Max(1f, style.SizePixels * 1.15f);

        var opts = new TextOptions(font) { Dpi = 96f };
        var b = TextMeasurer.MeasureBounds(LineHeightReferenceSample, opts);
        return MathF.Max(style.SizePixels * 0.5f, b.Height);
    }

    /// <summary>
    /// For a laid-out line, the minimum <c>FontRectangle.Top</c> of the reference sample across segments
    /// (most negative = dominant ascender). SixLabors <c>MeasureBounds</c> uses a baseline-left origin (+Y down);
    /// <c>-minTop</c> is the distance from line box top (<see cref="UiTextLayoutLine.LineTop"/>) to the alphabetic baseline.
    /// </summary>
    internal static bool TryMinReferenceBoundsTopForLine(FontLibrary fonts, UiTextLayoutLine line, out float minTop)
    {
        minTop = float.PositiveInfinity;
        foreach (var seg in line.Segments)
        {
            if (!fonts.TryCreateFont(in seg.Style, out var font, out _))
                continue;

            var opts = new TextOptions(font) { Dpi = 96f };
            var b = TextMeasurer.MeasureBounds(LineHeightReferenceSample, opts);
            minTop = MathF.Min(minTop, b.Top);
        }

        if (float.IsPositiveInfinity(minTop))
        {
            minTop = 0f;
            return false;
        }

        return true;
    }
}
