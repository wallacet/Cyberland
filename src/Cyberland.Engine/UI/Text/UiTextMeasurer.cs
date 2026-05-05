using Cyberland.Engine.Rendering.Text;
using SixLabors.Fonts;

namespace Cyberland.Engine.UI.Text;

/// <summary>
/// Layout-only horizontal measurement using SixLabors <see cref="TextMeasurer"/> with the same DPI mapping as
/// <see cref="FontLibrary.CreateFontAtPixelSize"/>.
/// </summary>
internal static class UiTextMeasurer
{
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
        var b = TextMeasurer.MeasureBounds("MgjpqyАЙ", opts);
        return MathF.Max(style.SizePixels * 0.5f, b.Height);
    }
}
