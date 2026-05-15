using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public void BakedMsdfAtlasLoader_LoadFromVfs_prefixes_bare_page_png_with_manifest_stem_when_using_dot_manifest_json()
    {
        // Matches MsdfAtlasBaker output: "{stem}.manifest.json" + "{stem}.page0.png" while JSON still lists "page0.png".
        var root = Path.Combine(Path.GetTempPath(), "cyb bake stem " + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "baked"));
        var manifestOnDisk = Path.Combine(root, "baked", "DemoAtlas.manifest.json");
        File.WriteAllBytes(Path.Combine(root, "baked", "DemoAtlas.page0.png"), MinimalPng(8, 8));

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
            var relManifest = "baked/DemoAtlas.manifest.json".Replace('\\', '/');
            var res = loader.LoadFromVfs(assets, r, cache, relManifest);
            Assert.True(res.Loaded);
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

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromPath_resolves_builtin_virtual_manifest()
    {
        var vfs = new VirtualFileSystem();
        var assets = new AssetManager(vfs);
        var loader = new BakedMsdfAtlasLoader();
        var renderer = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var result = loader.LoadFromPath(
            assets,
            renderer,
            cache,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            pageBudget: 1);
        Assert.True(result.Loaded);
        Assert.Equal(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14, result.ManifestPath);
        Assert.True(result.PageCount >= 1);
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromPath_invokes_onProgress_for_builtin_manifest()
    {
        var vfs = new VirtualFileSystem();
        var assets = new AssetManager(vfs);
        var loader = new BakedMsdfAtlasLoader();
        var renderer = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var progress = new List<float>();
        var result = loader.LoadFromPath(
            assets,
            renderer,
            cache,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            pageBudget: 1,
            onProgress: progress.Add);
        Assert.True(result.Loaded);
        Assert.NotEmpty(progress);
        Assert.Contains(1f, progress);
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromPath_missing_manifest_returns_failure()
    {
        var vfs = new VirtualFileSystem();
        var assets = new AssetManager(vfs);
        var loader = new BakedMsdfAtlasLoader();
        var renderer = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var result = loader.LoadFromPath(assets, renderer, cache, "missing/atlas.json");
        Assert.False(result.Loaded);
        Assert.Contains("manifest not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromPath_returns_failure_when_page_decode_fails()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb bake bad page " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "broken.bin"), "not-a-png");
        var manifest = new BakedMsdfAtlasManifest
        {
            FamilyId = "decode.fail",
            Face = "Regular",
            SizePixels = 14f,
            RasterRevision = GlyphRasterizer.RasterRevision,
            PageSizePixels = 16,
            Pages = [new BakedMsdfAtlasPageRef { Path = "broken.bin" }],
            Glyphs = Array.Empty<BakedMsdfGlyphEntry>()
        };
        File.WriteAllText(
            Path.Combine(root, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var loader = new BakedMsdfAtlasLoader();
            var renderer = new RecordingRenderer();
            var cache = new TextGlyphCache();
            var result = loader.LoadFromPath(assets, renderer, cache, "manifest.json");
            Assert.False(result.Loaded);
            Assert.Contains("decode", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task BakedMsdfAtlasLoader_LoadFromPathAsync_enqueue_then_drain_uploads()
    {
        var vfs = new VirtualFileSystem();
        var assets = new AssetManager(vfs);
        var loader = new BakedMsdfAtlasLoader();
        var renderer = new RecordingRenderer();
        var cache = new TextGlyphCache();

        var pending = loader.LoadFromPathAsync(
            assets,
            cache,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            pageBudget: 1);

        Assert.False(pending.IsCompleted);

        // Poll once to allow decode task to enqueue the upload work.
        for (var i = 0; i < 40 && !pending.IsCompleted; i++)
        {
            loader.DrainPendingUploads(renderer);
            await Task.Delay(5);
        }

        _ = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(pending.IsCompleted, "Loader async task did not complete before timeout.");
        var result = await pending;
        Assert.True(result.Loaded);
        Assert.True(result.GlyphCount > 0);
    }

    [Fact]
    public async Task BakedMsdfAtlasLoader_LoadFromPathAsync_canceled_token_cancels_task()
    {
        var vfs = new VirtualFileSystem();
        var assets = new AssetManager(vfs);
        var loader = new BakedMsdfAtlasLoader();
        var cache = new TextGlyphCache();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var task = loader.LoadFromPathAsync(
            assets,
            cache,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
    }

    [Fact]
    public void BakedMsdfAtlasLoader_LoadFromResource_returns_failure_when_page_decode_fails()
    {
        var loader = new BakedMsdfAtlasLoader();
        var renderer = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var manifest = BaseManifest("Regular", GlyphRasterizer.RasterRevision);
        var result = loader.LoadFromResource("bad-resource", manifest, _ => "not-a-png"u8.ToArray(), renderer, cache);
        Assert.False(result.Loaded);
        Assert.Contains("decode", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuiltinFonts_paths_and_virtual_resolver_cover_new_helpers()
    {
        var all = BuiltinFonts.EnumerateBakedAtlasManifestPaths().ToArray();
        Assert.Contains(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14, all);
        Assert.Contains(BuiltinFonts.BakedAtlasManifestPath.MonoRegular18, all);

        Assert.True(BuiltinFonts.TryResolveBakedAtlasFromVirtualPath(
            "/" + BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            out var manifest,
            out var readPageBytes));
        Assert.NotNull(manifest);
        var page = readPageBytes("nested/path/page0.png");
        Assert.NotEmpty(page);

        Assert.False(BuiltinFonts.TryResolveBakedAtlasFromVirtualPath("_cyberland/engine/msdf/nope.json", out _, out _));
    }
}
