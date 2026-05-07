using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Loads pre-baked MSDF atlas manifests and seeds <see cref="TextGlyphCache"/> before runtime fallback rasterization.
/// </summary>
internal sealed class BakedMsdfAtlasLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed record LoadResult(
        string ManifestPath,
        bool Loaded,
        string Message,
        int GlyphCount,
        int PageCount);

    public LoadResult LoadFromVfs(
        AssetManager assets,
        IRenderer renderer,
        TextGlyphCache cache,
        string manifestPath)
    {
        var manifest = assets.LoadJsonAsync<BakedMsdfAtlasManifest>(manifestPath, JsonOptions).GetAwaiter().GetResult();
        var manifestDir = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
        return LoadInternal(
            manifestPath,
            manifest,
            (relPath) => assets.LoadBytes(JoinManifestPath(manifestDir, relPath)),
            renderer,
            cache);
    }

    public LoadResult LoadFromResource(
        string logicalName,
        BakedMsdfAtlasManifest manifest,
        Func<string, byte[]> readPageBytes,
        IRenderer renderer,
        TextGlyphCache cache) =>
        LoadInternal(logicalName, manifest, readPageBytes, renderer, cache);

    private static LoadResult LoadInternal(
        string sourceLabel,
        BakedMsdfAtlasManifest manifest,
        Func<string, byte[]> readPageBytes,
        IRenderer renderer,
        TextGlyphCache cache)
    {
        if (manifest.RasterRevision != GlyphRasterizer.RasterRevision)
        {
            return new LoadResult(
                sourceLabel,
                false,
                $"raster revision mismatch (manifest={manifest.RasterRevision}, engine={GlyphRasterizer.RasterRevision})",
                0,
                0);
        }

        if (!TryParseFace(manifest.Face, out var face))
            return new LoadResult(sourceLabel, false, $"unknown face '{manifest.Face}'", 0, 0);

        var pageTextures = new TextureId[manifest.Pages.Length];
        for (var i = 0; i < manifest.Pages.Length; i++)
        {
            var raw = readPageBytes(manifest.Pages[i].Path);
            using var image = Image.Load<Rgba32>(raw);
            var rgba = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(rgba);
            var tex = renderer.RegisterTextureRgbaLinear(rgba, image.Width, image.Height);
            if (tex == TextureId.MaxValue)
                return new LoadResult(sourceLabel, false, $"failed to upload page '{manifest.Pages[i].Path}'", 0, i);
            pageTextures[i] = tex;
        }

        var sizeQuant = FontLibrary.QuantizeEmSizePixels(manifest.SizePixels);
        var imported = 0;
        var invW = 1f / manifest.PageSizePixels;
        var invH = 1f / manifest.PageSizePixels;
        foreach (var glyph in manifest.Glyphs)
        {
            if ((uint)glyph.PageIndex >= pageTextures.Length)
                continue;
            glyph.UvRect = new Vector4D<float>(
                glyph.X * invW,
                glyph.Y * invH,
                (glyph.X + glyph.Width) * invW,
                (glyph.Y + glyph.Height) * invH);
            cache.RegisterBakedGlyph(
                manifest.FamilyId,
                face,
                sizeQuant,
                glyph.CodePoint,
                manifest.RasterRevision,
                new TextGlyphCache.CachedGlyph
                {
                    TextureId = pageTextures[glyph.PageIndex],
                    WidthPx = glyph.DrawWidthPx,
                    HeightPx = glyph.DrawHeightPx,
                    OffsetPenToCenterX = glyph.OffsetPenToCenterX,
                    OffsetPenToCenterYWorld = glyph.OffsetPenToCenterYWorld,
                    AdvancePx = glyph.AdvancePx,
                    UvRect = glyph.UvRect,
                    MsdfPixelRange = glyph.MsdfPixelRange
                });
            imported++;
        }

        return new LoadResult(sourceLabel, true, "ok", imported, pageTextures.Length);
    }

    private static string JoinManifestPath(string? manifestDir, string relPath)
    {
        if (string.IsNullOrWhiteSpace(manifestDir))
            return relPath.Replace('\\', '/');
        return $"{manifestDir}/{relPath.Replace('\\', '/')}";
    }

    private static bool TryParseFace(string face, out FontFaceKind kind)
    {
        switch (face)
        {
            case "Regular":
                kind = FontFaceKind.Regular;
                return true;
            case "Bold":
                kind = FontFaceKind.Bold;
                return true;
            case "Italic":
                kind = FontFaceKind.Italic;
                return true;
            case "BoldItalic":
                kind = FontFaceKind.BoldItalic;
                return true;
            default:
                kind = FontFaceKind.Regular;
                return false;
        }
    }
}
