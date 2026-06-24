using Cyberland.Engine.Rendering;
using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.Assets;

/// <summary>
/// Computes nine-quad layout for 9-slice UI panels in viewport space (+Y down).
/// </summary>
public static class NineSliceLayout
{
    /// <summary>One axis-aligned quad slice with destination rect and source UV rect.</summary>
    public readonly record struct SliceQuad(
        Vector2D<float> Center,
        Vector2D<float> HalfExtents,
        Vector4D<float> UvRect);

    /// <summary>
    /// Builds up to nine quads for a stretched panel. Returns fewer when insets are empty or invalid.
    /// </summary>
    public static int BuildQuads(
        in UiRect destRect,
        in Vector4D<float> sourceUv,
        int sourcePixelWidth,
        int sourcePixelHeight,
        in NineSliceInsets insets,
        Span<SliceQuad> output)
    {
        if (output.Length < 9 || insets.IsEmpty || !insets.FitsSource(sourcePixelWidth, sourcePixelHeight))
            return 0;

        var destW = destRect.Width;
        var destH = destRect.Height;
        if (destW <= 0f || destH <= 0f)
            return 0;

        var left = MathF.Min(insets.Left, destW);
        var right = MathF.Min(insets.Right, destW);
        var top = MathF.Min(insets.Top, destH);
        var bottom = MathF.Min(insets.Bottom, destH);

        var centerW = MathF.Max(destW - left - right, 0f);
        var centerH = MathF.Max(destH - top - bottom, 0f);

        var suW = sourceUv.Z - sourceUv.X;
        var suH = sourceUv.W - sourceUv.Y;
        var uLeft = insets.Left / (float)sourcePixelWidth * suW;
        var uRight = insets.Right / (float)sourcePixelWidth * suW;
        var vTop = insets.Top / (float)sourcePixelHeight * suH;
        var vBottom = insets.Bottom / (float)sourcePixelHeight * suH;

        var uCenter0 = sourceUv.X + uLeft;
        var uCenter1 = sourceUv.Z - uRight;
        var vCenter0 = sourceUv.Y + vTop;
        var vCenter1 = sourceUv.W - vBottom;

        var x0 = destRect.X;
        var x1 = destRect.X + left;
        var x2 = destRect.X + left + centerW;
        var x3 = destRect.X + destW;
        var y0 = destRect.Y;
        var y1 = destRect.Y + top;
        var y2 = destRect.Y + top + centerH;
        var y3 = destRect.Y + destH;

        var count = 0;
        Add(ref count, output, x0, x1, y0, y1, sourceUv.X, uCenter0, sourceUv.Y, vCenter0);
        Add(ref count, output, x1, x2, y0, y1, uCenter0, uCenter1, sourceUv.Y, vCenter0);
        Add(ref count, output, x2, x3, y0, y1, uCenter1, sourceUv.Z, sourceUv.Y, vCenter0);
        Add(ref count, output, x0, x1, y1, y2, sourceUv.X, uCenter0, vCenter0, vCenter1);
        Add(ref count, output, x1, x2, y1, y2, uCenter0, uCenter1, vCenter0, vCenter1);
        Add(ref count, output, x2, x3, y1, y2, uCenter1, sourceUv.Z, vCenter0, vCenter1);
        Add(ref count, output, x0, x1, y2, y3, sourceUv.X, uCenter0, vCenter1, sourceUv.W);
        Add(ref count, output, x1, x2, y2, y3, uCenter0, uCenter1, vCenter1, sourceUv.W);
        Add(ref count, output, x2, x3, y2, y3, uCenter1, sourceUv.Z, vCenter1, sourceUv.W);
        return count;
    }

    private static void Add(
        ref int count,
        Span<SliceQuad> output,
        float x0, float x1, float y0, float y1,
        float u0, float u1, float v0, float v1)
    {
        var w = x1 - x0;
        var h = y1 - y0;
        if (w <= 1e-4f || h <= 1e-4f)
            return;
        output[count++] = new SliceQuad(
            new Vector2D<float>(x0 + w * 0.5f, y0 + h * 0.5f),
            new Vector2D<float>(w * 0.5f, h * 0.5f),
            new Vector4D<float>(u0, v0, u1, v1));
    }
}
