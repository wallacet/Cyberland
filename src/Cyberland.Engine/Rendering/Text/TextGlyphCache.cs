using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using SixLabors.Fonts;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Glyph bitmap cache backed by packed RGBA atlas pages (one GPU texture per page, sub-rectangle updates for new ink).
/// </summary>
/// <remarks>
/// <para>
/// Rasterization and GPU upload run under an internal lock; prefer resolving text on the main thread during mod load
/// for large warm-ups. <see cref="Clear"/> drops CPU maps and font reuse entries; atlas GPU textures remain resident
/// until process teardown unless a future renderer API releases slots.
/// </para>
/// <para>
/// Face/size keys use <see cref="FontLibrary.QuantizeEmSizePixels"/> so jittery float sizes map to stable buckets.
/// </para>
/// </remarks>
public sealed class TextGlyphCache
{
    private readonly object _lock = new();

    /// <summary>Cached GPU glyph with layout metrics and atlas UVs (min.xy, max.zw).</summary>
    public readonly struct CachedGlyph
    {
        /// <summary>Atlas page texture id from <see cref="IRenderer.RegisterTextureRgba"/>.</summary>
        public int TextureId { get; init; }

        /// <summary>Bitmap width in pixels.</summary>
        public int WidthPx { get; init; }

        /// <summary>Bitmap height in pixels.</summary>
        public int HeightPx { get; init; }

        /// <summary>Horizontal offset from pen to sprite center (pixels).</summary>
        public float OffsetPenToCenterX { get; init; }

        /// <summary>Vertical offset from baseline world Y to sprite center (world +Y up).</summary>
        public float OffsetPenToCenterYWorld { get; init; }

        /// <summary>Advance to next pen position (pixels).</summary>
        public float AdvancePx { get; init; }

        /// <summary>Normalized UV rectangle (min.xy, max.zw). Zero means full texture (legacy single-glyph textures).</summary>
        public Vector4D<float> UvRect { get; init; }
    }

    // Tuple key avoids unused generated record accessors in coverage for the private key type.
    private readonly Dictionary<(string FamilyId, FontFaceKind Face, int SizeQuant, int Codepoint), CachedGlyph> _glyphs =
        new();

    private readonly Dictionary<(string FamilyId, FontFaceKind Face, int SizeQuant), Font> _fontCache = new();

    private readonly List<GlyphAtlasPage> _pages = new();

    /// <summary>
    /// Clears CPU-side glyph maps, font instance reuse, and atlas bookkeeping (does not destroy GPU textures).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _glyphs.Clear();
            _fontCache.Clear();
            _pages.Clear();
        }
    }

    /// <summary>
    /// Resolves a glyph through the cache, rasterizing and uploading on miss.
    /// </summary>
    /// <returns>False if the family could not be resolved or rasterization failed.</returns>
    public bool TryGetGlyph(
        IRenderer renderer,
        FontLibrary fonts,
        in TextStyle style,
        int codePoint,
        string utf16Glyph,
        out CachedGlyph glyph) =>
        TryGetGlyph(renderer, fonts, in style, codePoint, utf16Glyph.AsSpan(), out glyph);

    /// <summary>Span-based grapheme (1–2 chars) to avoid per-codepoint string allocations on hot paths.</summary>
    public bool TryGetGlyph(
        IRenderer renderer,
        FontLibrary fonts,
        in TextStyle style,
        int codePoint,
        ReadOnlySpan<char> utf16Glyph,
        out CachedGlyph glyph)
    {
        glyph = default;
        if (renderer is null)
            return false;

        if (utf16Glyph.IsEmpty)
            return false;

        if (!fonts.TryGetFamily(style.FontFamilyId, out var fam) || fam is null)
            return false;

        var faceKind = FontLibrary.SelectFace(style.Bold, style.Italic, fam);
        var sizeQ = FontLibrary.QuantizeEmSizePixels(style.SizePixels);
        var key = (style.FontFamilyId, faceKind, sizeQ, codePoint);

        lock (_lock)
        {
            if (_glyphs.TryGetValue(key, out var existing))
            {
                glyph = existing;
                return true;
            }

            var font = GetOrCreateFontLocked(fam, in style, faceKind, sizeQ);

            if (!GlyphRasterizer.TryCreateGlyphRgba(font, utf16Glyph, out var rgba, out var w, out var h, out var adv,
                    out var cx, out var cyW)
                || !TryPackAndUpload(renderer, rgba!, w, h, out var texId, out var uv))
                return false;

            var built = new CachedGlyph
            {
                TextureId = texId,
                WidthPx = w,
                HeightPx = h,
                OffsetPenToCenterX = cx,
                OffsetPenToCenterYWorld = cyW,
                AdvancePx = adv,
                UvRect = uv
            };
            _glyphs[key] = built;
            glyph = built;
            return true;
        }
    }

    private Font GetOrCreateFontLocked(
        FontLibrary.RegisteredFamily fam,
        in TextStyle style,
        FontFaceKind faceKind,
        int sizeQuant)
    {
        var fk = (style.FontFamilyId, faceKind, sizeQuant);
        if (_fontCache.TryGetValue(fk, out var f))
            return f;

        var face = fam.GetFace(faceKind);
        var font = FontLibrary.CreateFontAtPixelSize(face, FontLibrary.EmQuantToPixels(sizeQuant));
        _fontCache[fk] = font;
        return font;
    }

    /// <summary>Tests and atlas packing: rejects dimensions that do not fit a fresh 2048² page.</summary>
    internal bool TryPackAndUpload(
        IRenderer renderer,
        byte[] rgba,
        int w,
        int h,
        out int textureId,
        out Vector4D<float> uvRect)
    {
        textureId = -1;
        uvRect = default;

        GlyphAtlasPage? page = _pages.Count > 0 ? _pages[^1] : null;
        if (page is null || !page.TryAllocate(w, h, out var ox, out var oy))
        {
            page = new GlyphAtlasPage();
            _pages.Add(page);
            if (!page.TryAllocate(w, h, out ox, out oy))
                return false;
        }

        GlyphAtlasPage.BlitPremultiplied(page.Pixels, GlyphAtlasPage.SizePx, ox, oy, rgba, w, h);

        var invW = 1f / GlyphAtlasPage.SizePx;
        var invH = 1f / GlyphAtlasPage.SizePx;
        uvRect = new Vector4D<float>(ox * invW, oy * invH, (ox + w) * invW, (oy + h) * invH);

        if (page.TextureId < 0)
        {
            var id = renderer.RegisterTextureRgba(page.Pixels, GlyphAtlasPage.SizePx, GlyphAtlasPage.SizePx);
            if (id < 0)
                return false;
            page.TextureId = id;
            textureId = id;
            return true;
        }

        if (!renderer.TryUploadTextureRgbaSubregion(page.TextureId, ox, oy, w, h, rgba))
            return false;
        textureId = page.TextureId;
        return true;
    }
}
