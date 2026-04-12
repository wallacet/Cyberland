namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// One CPU-side RGBA atlas page with shelf packing. GPU texture id is assigned on first full-page upload.
/// </summary>
internal sealed class GlyphAtlasPage
{
    internal const int SizePx = 2048;
    internal const int PadPx = 2;

    /// <summary>Premultiplied RGBA8, row-major, top row first (matches texture sampling).</summary>
    internal readonly byte[] Pixels = new byte[SizePx * SizePx * 4];

    internal int TextureId = -1;

    private int _cursorX = PadPx;
    private int _cursorY = PadPx;
    private int _rowH;

    /// <summary>Tries to reserve a <paramref name="gw"/>×<paramref name="gh"/> rect; opens a new shelf row or fails if the page is full.</summary>
    internal bool TryAllocate(int gw, int gh, out int ox, out int oy)
    {
        ox = 0;
        oy = 0;
        if (gw < 1 || gh < 1)
            return false;
        if (gw + PadPx > SizePx || gh + PadPx > SizePx)
            return false;

        if (_cursorX + gw + PadPx > SizePx)
        {
            _cursorX = PadPx;
            _cursorY += _rowH + PadPx;
            _rowH = 0;
        }

        if (_cursorY + gh + PadPx > SizePx)
            return false;

        ox = _cursorX;
        oy = _cursorY;
        _cursorX += gw + PadPx;
        _rowH = Math.Max(_rowH, gh);
        return true;
    }

    internal static void BlitPremultiplied(byte[] dest, int dstW, int dx, int dy, ReadOnlySpan<byte> src, int w, int h)
    {
        for (var row = 0; row < h; row++)
        {
            var srcRow = src.Slice(row * w * 4, w * 4);
            var dstOff = ((dy + row) * dstW + dx) * 4;
            srcRow.CopyTo(dest.AsSpan(dstOff, w * 4));
        }
    }
}
