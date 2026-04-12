using System.Diagnostics.CodeAnalysis;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Rasterizes a single UTF-16 code unit / cluster string to premultiplied RGBA using SixLabors.
/// </summary>
internal static class GlyphRasterizer
{
    /// <summary>
    /// Builds a small RGBA bitmap for one glyph cluster and horizontal advance in pixels.
    /// </summary>
    /// <param name="font">Resolved font face.</param>
    /// <param name="glyph">One grapheme as a string (may be surrogate pair).</param>
    /// <param name="rgba">Packed RGBA8, length <c>width*height*4</c>.</param>
    /// <param name="width">Bitmap width.</param>
    /// <param name="height">Bitmap height.</param>
    /// <param name="advancePx">Horizontal advance to the next pen position.</param>
    /// <param name="offsetPenToCenterX">From current pen X to sprite center X (same coord system as SixLabors bounds).</param>
    /// <param name="offsetPenToCenterYWorld">
    /// From baseline world Y to sprite center Y in **world** space (+Y up). Computed as
    /// <c>-(bounds.Top + bounds.Height/2)</c> when bounds use +Y down from the layout origin on the baseline.
    /// </param>
    public static bool TryCreateGlyphRgba(
        Font font,
        string glyph,
        [NotNullWhen(true)] out byte[]? rgba,
        out int width,
        out int height,
        out float advancePx,
        out float offsetPenToCenterX,
        out float offsetPenToCenterYWorld) =>
        TryCreateGlyphRgba(font, glyph.AsSpan(), out rgba, out width, out height, out advancePx,
            out offsetPenToCenterX, out offsetPenToCenterYWorld);

    /// <summary>Rasterizes one grapheme cluster (1–2 UTF-16 code units) to premultiplied RGBA.</summary>
    public static bool TryCreateGlyphRgba(
        Font font,
        ReadOnlySpan<char> glyph,
        [NotNullWhen(true)] out byte[]? rgba,
        out int width,
        out int height,
        out float advancePx,
        out float offsetPenToCenterX,
        out float offsetPenToCenterYWorld)
    {
        rgba = null;
        width = 1;
        height = 1;
        advancePx = 0f;
        offsetPenToCenterX = 0f;
        offsetPenToCenterYWorld = 0f;

        var opts = new TextOptions(font) { Dpi = 96f };
        if (glyph.IsEmpty)
            return false;

        var glyphStr = new string(glyph);

        // Non-throwing measurers (single codepoint); union advance box with ink bounds for placement.
        var ink = TextMeasurer.MeasureBounds(glyphStr, opts);
        var advRect = TextMeasurer.MeasureAdvance(glyphStr, opts);
        var b = FontRectangle.Union(in advRect, in ink);

        advancePx = advRect.Width > 0f ? advRect.Width : MathF.Max(1f, font.Size * 0.25f);

        width = Math.Max(1, (int)MathF.Ceiling(b.Width));
        height = Math.Max(1, (int)MathF.Ceiling(b.Height));

        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx =>
        {
            ctx.Fill(Color.Transparent);
            var rich = new RichTextOptions(font)
            {
                Dpi = 96f,
                Origin = new PointF(-b.Left, -b.Top)
            };
            ctx.DrawText(rich, glyphStr, Color.White);
        });

        rgba = new byte[width * height * 4];
        image.CopyPixelDataTo(rgba);

        offsetPenToCenterX = b.Left + b.Width * 0.5f;
        offsetPenToCenterYWorld = -(b.Top + b.Height * 0.5f);
        return true;
    }
}
