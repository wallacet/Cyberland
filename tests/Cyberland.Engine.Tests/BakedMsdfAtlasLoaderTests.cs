using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

public sealed class BakedMsdfAtlasLoaderTests
{
    private static byte[] MinimalPng(int width, int height)
    {
        using var img = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0, 255));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static BakedMsdfAtlasManifest BaseManifest(string face, int revision, params BakedMsdfGlyphEntry[] glyphs) =>
        new()
        {
            FamilyId = "test.family",
            Face = face,
            SizePixels = 14f,
            RasterRevision = revision,
            PageSizePixels = 16,
            Pages = [new BakedMsdfAtlasPageRef { Path = "page0.png" }],
            Glyphs = glyphs
        };

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromResource_raster_revision_mismatch()
    {
        var loader = new BakedMsdfAtlasLoader();
        var r = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var manifest = BaseManifest("Regular", GlyphRasterizer.RasterRevision - 1,
            new BakedMsdfGlyphEntry
            {
                CodePoint = 65,
                PageIndex = 0,
                X = 0,
                Y = 0,
                Width = 4,
                Height = 4,
                DrawWidthPx = 4f,
                DrawHeightPx = 4f,
                AdvancePx = 4f,
                MsdfPixelRange = 4f
            });
        var png = MinimalPng(16, 16);
        var res = loader.LoadFromResource("logical", manifest, _ => png, r, cache);
        Assert.False(res.Loaded);
        Assert.Equal("logical", res.ManifestPath);
        Assert.Contains("raster revision mismatch", res.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromResource_unknown_face()
    {
        var loader = new BakedMsdfAtlasLoader();
        var r = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var manifest = BaseManifest("NotARealFace", GlyphRasterizer.RasterRevision);
        var png = MinimalPng(16, 16);
        var res = loader.LoadFromResource("logical", manifest, _ => png, r, cache);
        Assert.False(res.Loaded);
        Assert.Equal("logical", res.ManifestPath);
        Assert.Contains("unknown face", res.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Regular")]
    [InlineData("Bold")]
    [InlineData("Italic")]
    [InlineData("BoldItalic")]
    public void BakedMsdfAtlasLoader_LoadFromResource_accepts_face(string face)
    {
        var loader = new BakedMsdfAtlasLoader();
        var r = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var manifest = BaseManifest(face, GlyphRasterizer.RasterRevision,
            new BakedMsdfGlyphEntry
            {
                CodePoint = 65,
                PageIndex = 0,
                X = 0,
                Y = 0,
                Width = 4,
                Height = 4,
                DrawWidthPx = 4f,
                DrawHeightPx = 4f,
                AdvancePx = 4f,
                MsdfPixelRange = 4f
            });
        var png = MinimalPng(16, 16);
        var res = loader.LoadFromResource(face, manifest, _ => png, r, cache);
        Assert.True(res.Loaded);
        Assert.Equal(face, res.ManifestPath);
        Assert.Equal(1, res.GlyphCount);
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromResource_skips_glyph_when_page_index_out_of_range()
    {
        var loader = new BakedMsdfAtlasLoader();
        var r = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var manifest = BaseManifest("Regular", GlyphRasterizer.RasterRevision,
            new BakedMsdfGlyphEntry
            {
                CodePoint = 65,
                PageIndex = 9,
                X = 0,
                Y = 0,
                Width = 4,
                Height = 4,
                DrawWidthPx = 4f,
                DrawHeightPx = 4f,
                AdvancePx = 4f,
                MsdfPixelRange = 4f
            });
        var png = MinimalPng(16, 16);
        var res = loader.LoadFromResource("logical", manifest, _ => png, r, cache);
        Assert.True(res.Loaded);
        Assert.Equal("logical", res.ManifestPath);
        Assert.Equal(0, res.GlyphCount);
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromResource_fails_when_texture_upload_returns_invalid()
    {
        var loader = new BakedMsdfAtlasLoader();
        var r = new RecordingRenderer { RegisterTextureRgbaOverride = TextureId.MaxValue };
        var cache = new TextGlyphCache();
        var manifest = BaseManifest("Regular", GlyphRasterizer.RasterRevision,
            new BakedMsdfGlyphEntry
            {
                CodePoint = 65,
                PageIndex = 0,
                X = 0,
                Y = 0,
                Width = 4,
                Height = 4,
                DrawWidthPx = 4f,
                DrawHeightPx = 4f,
                AdvancePx = 4f,
                MsdfPixelRange = 4f
            });
        var png = MinimalPng(16, 16);
        var res = loader.LoadFromResource("logical", manifest, _ => png, r, cache);
        Assert.False(res.Loaded);
        Assert.Equal("logical", res.ManifestPath);
        Assert.Contains("failed to upload", res.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromVfs_resolves_page_paths_relative_to_manifest_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb bake vfs " + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "atlas"));
        var manifestPathOnDisk = Path.Combine(root, "atlas", "m.json");
        var pngPath = Path.Combine(root, "atlas", "pages", "p0.png");
        Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
        File.WriteAllBytes(pngPath, MinimalPng(16, 16));

        var manifest = new BakedMsdfAtlasManifest
        {
            FamilyId = "vfs.family",
            Face = "Regular",
            SizePixels = 14f,
            RasterRevision = GlyphRasterizer.RasterRevision,
            PageSizePixels = 16,
            Pages = [new BakedMsdfAtlasPageRef { Path = "pages/p0.png" }],
            Glyphs =
            [
                new BakedMsdfGlyphEntry
                {
                    CodePoint = 66,
                    PageIndex = 0,
                    X = 0,
                    Y = 0,
                    Width = 4,
                    Height = 4,
                    DrawWidthPx = 4f,
                    DrawHeightPx = 4f,
                    AdvancePx = 4f,
                    MsdfPixelRange = 4f
                }
            ]
        };
        var json = JsonSerializer.Serialize(manifest,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(manifestPathOnDisk, json);

        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var loader = new BakedMsdfAtlasLoader();
            var r = new RecordingRenderer();
            var cache = new TextGlyphCache();
            var relManifest = "atlas/m.json".Replace('\\', '/');
            var res = loader.LoadFromVfs(assets, r, cache, relManifest);
            Assert.True(res.Loaded);
            Assert.Equal(relManifest, res.ManifestPath);
            Assert.Equal(1, res.GlyphCount);
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromVfs_joins_pages_when_manifest_has_no_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb bake root " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var manifestOnDisk = Path.Combine(root, "manifest.json");
        File.WriteAllBytes(Path.Combine(root, "page0.png"), MinimalPng(8, 8));

        var manifest = BaseManifest("Regular", GlyphRasterizer.RasterRevision,
            new BakedMsdfGlyphEntry
            {
                CodePoint = 67,
                PageIndex = 0,
                X = 0,
                Y = 0,
                Width = 2,
                Height = 2,
                DrawWidthPx = 2f,
                DrawHeightPx = 2f,
                AdvancePx = 2f,
                MsdfPixelRange = 2f
            });
        File.WriteAllText(manifestOnDisk,
            JsonSerializer.Serialize(manifest,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var loader = new BakedMsdfAtlasLoader();
            var r = new RecordingRenderer();
            var cache = new TextGlyphCache();
            var res = loader.LoadFromVfs(assets, r, cache, "manifest.json");
            Assert.True(res.Loaded);
            Assert.Equal("manifest.json", res.ManifestPath);
            Assert.Equal(1, res.GlyphCount);
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
