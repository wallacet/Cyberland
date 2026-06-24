using Cyberland.Engine.UI.Core;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

public sealed class BuiltinTexturesTests
{
    [Fact]
    public void CreateMissingTextureRgba_alternates_magenta_and_green_tiles()
    {
        const int size = 16;
        const int tile = 4;
        var rgba = BuiltinTextures.CreateMissingTextureRgba(size, size, tile);
        Assert.Equal(size * size * 4, rgba.Length);

        static bool IsMagenta(byte[] buf, int x, int y, int width) =>
            buf[(y * width + x) * 4] == 255 && buf[(y * width + x) * 4 + 1] == 0 && buf[(y * width + x) * 4 + 2] == 255;

        Assert.True(IsMagenta(rgba, 0, 0, size));
        Assert.False(IsMagenta(rgba, tile, 0, size));
        Assert.True(IsMagenta(rgba, tile, tile, size));
    }

    [Fact]
    public void CreateMissingTextureRgba_rejects_non_positive_dimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltinTextures.CreateMissingTextureRgba(0, 8, 4));
    }
}

public sealed class SpriteAtlasManifestParserTests
{
    [Fact]
    public void PixelRectToUvRect_normalizes_within_page()
    {
        var uv = SpriteAtlasManifestParser.PixelRectToUvRect(10, 20, 30, 40, 200, 100);
        Assert.Equal(0.05f, uv.X, 3);
        Assert.Equal(0.2f, uv.Y, 3);
        Assert.Equal(0.2f, uv.Z, 3);
        Assert.Equal(0.6f, uv.W, 3);
    }

    [Fact]
    public void PixelRectToUvRect_invalid_dimensions_return_full_uv()
    {
        var uv = SpriteAtlasManifestParser.PixelRectToUvRect(0, 0, 32, 32, 0, 100);
        Assert.Equal(1f, uv.W);
    }

    [Fact]
    public void SheetFrameUvRect_invalid_grid_returns_sheet_uv()
    {
        var sheet = new Vector4D<float>(0.1f, 0.2f, 0.9f, 0.8f);
        var uv = SpriteAtlasManifestParser.SheetFrameUvRect(sheet, columns: 0, frameIndex: 0, frameCount: 4);
        Assert.Equal(sheet, uv);
    }

    [Fact]
    public void SheetFrameUvRect_subdivides_region()
    {
        var sheet = new Vector4D<float>(0f, 0f, 1f, 1f);
        var f0 = SpriteAtlasManifestParser.SheetFrameUvRect(sheet, columns: 4, frameIndex: 0, frameCount: 8);
        var f1 = SpriteAtlasManifestParser.SheetFrameUvRect(sheet, columns: 4, frameIndex: 1, frameCount: 8);
        Assert.Equal(0f, f0.X, 3);
        Assert.Equal(0.25f, f1.X, 3);
        Assert.Equal(0f, f0.Y, 3);
        Assert.Equal(0.5f, f0.W, 3);
    }
}

public sealed class SpriteAtlasCatalogTests
{
    [Fact]
    public void GetOrLoad_resolves_locale_manifest_and_pages()
    {
        var root = CreateAtlasFixture(includeDeOverlay: false);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var renderer = new RecordingRenderer();
            var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => lc);

            var atlas = catalog.GetOrLoad("Textures/Atlases/test.atlas.json", renderer);
            Assert.True(atlas.TryGetRegion("icon", out var region));
            Assert.NotEqual(renderer.MissingTextureId, region.PageTextureId);
            Assert.Equal(32, region.PixelWidth);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetOrLoad_prefers_localized_manifest_for_primary_culture()
    {
        var root = CreateAtlasFixture(includeDeOverlay: true);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "de-DE");
            var renderer = new RecordingRenderer();
            var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => lc);

            var atlas = catalog.GetOrLoad("Textures/Atlases/test.atlas.json", renderer);
            Assert.Contains("de", atlas.ResolvedManifestPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(atlas.TryGetAnimation("talk", out var talk));
            Assert.Equal(2, talk.RegionNames.Length);
            Assert.Equal("talk_de_01", talk.RegionNames[0]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetOrLoad_deduplicates_same_resolved_manifest()
    {
        var root = CreateAtlasFixture(includeDeOverlay: false);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var renderer = new RecordingRenderer();
            var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => lc);

            _ = catalog.GetOrLoad("Textures/Atlases/test.atlas.json", renderer);
            var callsBefore = renderer.RegisterTextureRgbaCallCount;
            _ = catalog.GetOrLoad("Textures/Atlases/test.atlas.json", renderer);
            Assert.Equal(callsBefore, renderer.RegisterTextureRgbaCallCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TryGetCached_returns_false_before_load_and_true_after()
    {
        var root = CreateAtlasFixture(includeDeOverlay: false);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var renderer = new RecordingRenderer();
            var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => lc);

            Assert.False(catalog.TryGetCached("Textures/Atlases/test.atlas.json", false, out _));
            var loaded = catalog.GetOrLoad("Textures/Atlases/test.atlas.json", renderer);
            Assert.True(catalog.TryGetCached("Textures/Atlases/test.atlas.json", false, out var cached));
            Assert.Same(loaded, cached);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetOrLoad_missing_manifest_yields_missing_region()
    {
        var vfs = new VirtualFileSystem();
        var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
        var renderer = new RecordingRenderer();
        var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => lc);

        var atlas = catalog.GetOrLoad("Textures/Atlases/missing.atlas.json", renderer);
        Assert.True(atlas.TryGetRegion("__missing__", out _));
    }

    [Fact]
    public void GetOrLoad_corrupt_manifest_and_invalid_entries_use_missing_fallback()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-atlas-bad-" + Guid.NewGuid());
        var atlasDir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(atlasDir);
        File.WriteAllText(Path.Combine(atlasDir, "bad.atlas.json"), "not-json");
        File.WriteAllText(Path.Combine(atlasDir, "schema2.atlas.json"), """{"schemaVersion":2,"pages":[],"regions":[]}""");
        File.WriteAllText(Path.Combine(atlasDir, "skips.atlas.json"), """
            {
              "schemaVersion": 1,
              "pages": [{ "path": "Textures/Atlases/missing-page.png" }],
              "regions": [
                { "name": "", "pageIndex": 0, "pixelRect": [0, 0, 8, 8] },
                { "name": "badpage", "pageIndex": 5, "pixelRect": [0, 0, 8, 8] },
                { "name": "ok", "pageIndex": 0, "pixelRect": [0, 0, 8, 8], "pivot": [0.5, 0.5], "sizeWorld": [16, 16], "nineSlice": [1, 1, 1, 1] }
              ],
              "animations": { "empty": { "regionNames": [], "secondsPerFrame": 0.1, "loop": true } },
              "sheets": { "bad": { "regionName": "", "columns": 0, "frameCount": 0, "secondsPerFrame": 0.1, "loop": true } }
            }
            """);
        WriteSolidPng(Path.Combine(atlasDir, "page0.png"), 16, 16, 255, 255, 255);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var renderer = new RecordingRenderer();
            var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => lc);

            Assert.True(catalog.GetOrLoad("Textures/Atlases/bad.atlas.json", renderer).TryGetRegion("__missing__", out _));
            Assert.True(catalog.GetOrLoad("Textures/Atlases/schema2.atlas.json", renderer).TryGetRegion("__missing__", out _));
            var skips = catalog.GetOrLoad("Textures/Atlases/skips.atlas.json", renderer);
            Assert.True(skips.TryGetRegion("ok", out _));
            Assert.False(skips.TryGetAnimation("empty", out _));
            Assert.False(skips.TryGetSheet("bad", out _));

            var corruptPageDir = Path.Combine(root, "Textures", "Atlases", "badimg");
            Directory.CreateDirectory(corruptPageDir);
            File.WriteAllText(Path.Combine(corruptPageDir, "page.png"), "not-an-image");
            File.WriteAllText(Path.Combine(atlasDir, "badimg.atlas.json"), """
                {"schemaVersion":1,"pages":[{"path":"Textures/Atlases/badimg/page.png"}],"regions":[{"name":"x","pageIndex":0,"pixelRect":[0,0,4,4]}]}
                """);
            _ = catalog.GetOrLoad("Textures/Atlases/badimg.atlas.json", renderer);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetOrLoad_localeInvariant_and_ClearCache()
    {
        var root = CreateAtlasFixture(includeDeOverlay: false);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var renderer = new RecordingRenderer();
            var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => null);

            var atlas = catalog.GetOrLoad("Textures/Atlases/test.atlas.json", renderer, localeInvariant: true);
            Assert.True(atlas.TryGetRegion("icon", out _));

            var callsBefore = renderer.RegisterTextureRgbaCallCount;
            catalog.ClearCache();
            _ = catalog.GetOrLoad("Textures/Atlases/test.atlas.json", renderer, localeInvariant: true);
            Assert.True(renderer.RegisterTextureRgbaCallCount > callsBefore);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateAtlasFixture(bool includeDeOverlay)
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-atlas-" + Guid.NewGuid());
        var atlasDir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(atlasDir);
        WriteSolidPng(Path.Combine(atlasDir, "page0.png"), 64, 64, 255, 0, 0);

        var manifest = """
                       {
                         "schemaVersion": 1,
                         "pages": [{ "path": "Textures/Atlases/page0.png" }],
                         "regions": [
                           { "name": "icon", "pageIndex": 0, "pixelRect": [0, 0, 32, 32], "sizeWorld": [32, 32] }
                         ],
                         "animations": {
                           "talk": { "regionNames": ["icon"], "secondsPerFrame": 0.1, "loop": true }
                         }
                       }
                       """;
        File.WriteAllText(Path.Combine(atlasDir, "test.atlas.json"), manifest);

        if (includeDeOverlay)
        {
            var deDir = Path.Combine(root, "Locale", "de", "Textures", "Atlases");
            Directory.CreateDirectory(deDir);
            WriteSolidPng(Path.Combine(deDir, "page0.png"), 64, 64, 0, 255, 0);
            var deManifest = """
                             {
                               "schemaVersion": 1,
                               "pages": [{ "path": "Textures/Atlases/page0.png" }],
                               "regions": [
                                 { "name": "talk_de_01", "pageIndex": 0, "pixelRect": [0, 0, 32, 32], "sizeWorld": [32, 32] },
                                 { "name": "talk_de_02", "pageIndex": 0, "pixelRect": [32, 0, 32, 32], "sizeWorld": [32, 32] }
                               ],
                               "animations": {
                                 "talk": { "regionNames": ["talk_de_01", "talk_de_02"], "secondsPerFrame": 0.08, "loop": true }
                               }
                             }
                             """;
            File.WriteAllText(Path.Combine(deDir, "test.atlas.json"), deManifest);
        }

        return root;
    }

    private static void WriteSolidPng(string path, int w, int h, byte r, byte g, byte b)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new Image<Rgba32>(w, h);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                    row[x] = new Rgba32(r, g, b, 255);
            }
        });
        image.SaveAsPng(path);
    }

    [Fact]
    public void GetOrLoad_unreadable_page_image_fails_dimension_probe()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-atlas-lock-" + Guid.NewGuid());
        var atlasDir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(atlasDir);
        var pagePath = Path.Combine(atlasDir, "page0.png");
        File.WriteAllBytes(pagePath, Array.Empty<byte>());
        File.WriteAllText(Path.Combine(atlasDir, "lock.atlas.json"), """
            {"schemaVersion":1,"pages":[{"path":"Textures/Atlases/page0.png"}],"regions":[{"name":"a","pageIndex":0,"pixelRect":[0,0,8,8]}]}
            """);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var renderer = new RecordingRenderer();
            var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => null);
            var atlas = catalog.GetOrLoad("Textures/Atlases/lock.atlas.json", renderer);
            Assert.True(atlas.TryGetRegion("a", out var region));
            Assert.Equal(renderer.MissingTextureId, region.PageTextureId);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

public sealed class SpriteAtlasBindingApplierTests
{
    [Fact]
    public void ApplyAnimatedFrame_advances_sheet_uv()
    {
        var renderer = new RecordingRenderer();
        var atlas = BuildTestAtlas(renderer);
        var binding = new SpriteAtlasBinding { SheetName = "run", ElapsedSeconds = 0f };
        var sprite = new Sprite();
        SpriteAtlasBindingApplier.ApplyInitial(atlas, ref binding, ref sprite, renderer);

        binding.ElapsedSeconds = 0.2f;
        SpriteAtlasBindingApplier.ApplyAnimatedFrame(atlas, ref binding, ref sprite, deltaSeconds: 0f);
        var uvAfter = sprite.UvRect;
        Assert.True(uvAfter.X > 0f);
    }

    private static SpriteAtlas BuildTestAtlas(RecordingRenderer renderer)
    {
        var vfs = new VirtualFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "cyb-bind-" + Guid.NewGuid());
        var dir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(dir);
        using (var img = new Image<Rgba32>(64, 32))
            img.SaveAsPng(Path.Combine(dir, "sheet.png"));
        File.WriteAllText(Path.Combine(dir, "a.atlas.json"), """
            {
              "schemaVersion": 1,
              "pages": [{ "path": "Textures/Atlases/sheet.png" }],
              "regions": [{ "name": "run_sheet", "pageIndex": 0, "pixelRect": [0, 0, 64, 32], "sizeWorld": [64, 32] }],
              "sheets": { "run": { "regionName": "run_sheet", "columns": 4, "frameCount": 8, "secondsPerFrame": 0.1, "loop": true } }
            }
            """);
        vfs.Mount(root);
        var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => null);
        try
        {
            return catalog.GetOrLoad("Textures/Atlases/a.atlas.json", renderer);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

public sealed class NineSliceLayoutTests
{
    [Fact]
    public void BuildQuads_rejects_short_output_invalid_insets_and_zero_dest()
    {
        Span<NineSliceLayout.SliceQuad> quads = stackalloc NineSliceLayout.SliceQuad[8];
        var insets = new NineSliceInsets(10, 8, 10, 8);
        Assert.Equal(0, NineSliceLayout.BuildQuads(new UiRect(0, 0, 100, 80), default, 50, 40, insets, quads));
        Assert.Equal(0, NineSliceLayout.BuildQuads(new UiRect(0, 0, 0, 80), default, 50, 40, insets, stackalloc NineSliceLayout.SliceQuad[9]));
    }

    [Fact]
    public void BuildQuads_skips_degenerate_slice_quads()
    {
        Span<NineSliceLayout.SliceQuad> quads = stackalloc NineSliceLayout.SliceQuad[9];
        var insets = new NineSliceInsets(50, 0, 50, 0);
        var count = NineSliceLayout.BuildQuads(new UiRect(0, 0, 100, 40), default, 100, 40, insets, quads);
        Assert.True(count is > 0 and < 9);
    }

    [Fact]
    public void BuildQuads_emits_nine_slices_for_valid_insets()
    {
        Span<NineSliceLayout.SliceQuad> quads = stackalloc NineSliceLayout.SliceQuad[9];
        var dest = new UiRect(0, 0, 100, 80);
        var uv = new Vector4D<float>(0, 0, 1, 1);
        var insets = new NineSliceInsets(10, 8, 10, 8);
        var count = NineSliceLayout.BuildQuads(dest, uv, 50, 40, insets, quads);
        Assert.Equal(9, count);
        Assert.True(quads[0].HalfExtents.X <= 10.5f);
    }

    [Fact]
    public void NineSliceInsets_FitsSource_rejects_oversized_borders()
    {
        var insets = new NineSliceInsets(30, 0, 30, 0);
        Assert.False(insets.FitsSource(50, 10));
    }
}

public sealed class TextureSourceResolverTests
{
    [Fact]
    public void Resolve_atlas_region_parses_hash_syntax()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-texsrc-" + Guid.NewGuid());
        var dir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(dir);
        using (var img = new Image<Rgba32>(32, 32))
            img.SaveAsPng(Path.Combine(dir, "p.png"));
        File.WriteAllText(Path.Combine(dir, "ui.atlas.json"), """
            {"schemaVersion":1,"pages":[{"path":"Textures/Atlases/p.png"}],"regions":[{"name":"panel","pageIndex":0,"pixelRect":[0,0,32,32],"nineSlice":[4,4,4,4]}]}
            """);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var renderer = new RecordingRenderer();
            var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => lc);
            var resolver = new TextureSourceResolver(new AssetManager(vfs), () => lc, () => catalog);
            var resolved = resolver.Resolve("Textures/Atlases/ui.atlas.json#panel", renderer);
            Assert.True(resolved.IsValid);
            Assert.Equal(4, resolved.NineSlice.Left);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Resolve_handles_builtin_and_png_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-texsrc2-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Textures"));
        using (var img = new Image<Rgba32>(4, 4))
            img.SaveAsPng(Path.Combine(root, "Textures", "icon.png"));
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var renderer = new RecordingRenderer();
            var assets = new AssetManager(vfs);
            var resolver = new TextureSourceResolver(assets, () => lc, () => null);

            Assert.True(resolver.Resolve("white", renderer).IsValid);
            Assert.True(resolver.Resolve("defaultNormal", renderer).IsValid);
            Assert.True(resolver.Resolve("missing", renderer).IsValid);
            Assert.False(resolver.Resolve("", renderer).IsValid);
            Assert.True(resolver.Resolve("Textures/icon.png", renderer).IsValid);
            Assert.True(resolver.Resolve("Textures/Atlases/x.atlas.json#nope", renderer).IsValid);
            Assert.True(resolver.Resolve("Textures/Atlases/x.atlas.json#", renderer).IsValid);
            Assert.True(new TextureSourceResolver(assets, () => lc, () => null)
                .Resolve("Textures/Atlases/x.atlas.json#panel", renderer).IsValid);
            Assert.True(resolver.Resolve("Textures/missing.png", renderer).IsValid);

            var vfs2 = new VirtualFileSystem();
            vfs2.Mount(root);
            var lc2 = new LocalizedContent(new LocalizationManager(), vfs2, "en");
            var resolverWithLc = new TextureSourceResolver(new AssetManager(vfs2), () => lc2, () => null);
            Assert.True(resolverWithLc.Resolve("Textures/icon.png", renderer).IsValid);

            var atlasRoot = Path.Combine(Path.GetTempPath(), "cyb-texsrc3-" + Guid.NewGuid());
            var atlasDir = Path.Combine(atlasRoot, "Textures", "Atlases");
            Directory.CreateDirectory(atlasDir);
            using (var img2 = new Image<Rgba32>(8, 8))
                img2.SaveAsPng(Path.Combine(atlasDir, "p.png"));
            File.WriteAllText(Path.Combine(atlasDir, "a.atlas.json"), """
                {"schemaVersion":1,"pages":[{"path":"Textures/Atlases/p.png"}],"regions":[{"name":"only","pageIndex":0,"pixelRect":[0,0,8,8]}]}
                """);
            try
            {
                vfs2.Mount(atlasRoot);
                var cat = new SpriteAtlasCatalog(new AssetManager(vfs2), () => lc2);
                var atlasResolver = new TextureSourceResolver(new AssetManager(vfs2), () => lc2, () => cat);
                var atlasResolved = atlasResolver.Resolve("Textures/Atlases/a.atlas.json#missing", renderer);
                Assert.True(atlasResolved.IsValid);
                _ = atlasResolved.NineSlice;
                _ = atlasResolved.SourcePixelHeight;
            }
            finally
            {
                Directory.Delete(atlasRoot, true);
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Resolve_without_localized_uses_asset_manager_directly()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-texsrc-noloc-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Textures"));
        using (var img = new Image<Rgba32>(4, 4))
            img.SaveAsPng(Path.Combine(root, "Textures", "direct.png"));
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var resolver = new TextureSourceResolver(assets, () => null, () => null);
            var renderer = new RecordingRenderer();
            var resolved = resolver.Resolve("Textures/direct.png", renderer);
            Assert.True(resolved.IsValid);
            Assert.Equal(4, resolved.SourcePixelWidth);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Resolve_uses_fallback_pixel_size_when_dimensions_unavailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-texsrc-dim-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "bad-dim.png"), "not-a-png");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var renderer = new RecordingRenderer();
            var mockLc = new Moq.Mock<ILocalizedContent>();
            mockLc.Setup(l => l.TryLoadTextureFromCanonical("Textures/x.png", renderer))
                .Returns(new TextureLoadResult(9, TextureLoadStatus.Ok));
            mockLc.Setup(l => l.TryResolveLocalizedPath("Textures/x.png"))
                .Returns("bad-dim.png");
            var resolver = new TextureSourceResolver(assets, () => mockLc.Object, () => null);
            var resolved = resolver.Resolve("Textures/x.png", renderer);
            Assert.True(resolved.IsValid);
            Assert.Equal(1, resolved.SourcePixelWidth);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

public sealed class SpriteAtlasBindingApplierCoverageTests
{
    [Fact]
    public void ApplyInitial_and_animation_cover_region_and_frame_list_paths()
    {
        var renderer = new RecordingRenderer();
        var atlas = BuildRichAtlas(renderer);
        var sprite = new Sprite();

        var regionBinding = new SpriteAtlasBinding { RegionName = "icon" };
        SpriteAtlasBindingApplier.ApplyInitial(atlas, ref regionBinding, ref sprite, renderer);
        Assert.Equal(16f, sprite.HalfExtents.X);

        var animBinding = new SpriteAtlasBinding { AnimationName = "talk" };
        SpriteAtlasBindingApplier.ApplyInitial(atlas, ref animBinding, ref sprite, renderer);
        animBinding.ElapsedSeconds = 0.15f;
        SpriteAtlasBindingApplier.ApplyAnimatedFrame(atlas, ref animBinding, ref sprite, 0f);
        Assert.True(SpriteAtlasBindingApplier.IsAnimated(in animBinding));

        var missingBinding = new SpriteAtlasBinding();
        SpriteAtlasBindingApplier.ApplyInitial(atlas, ref missingBinding, ref sprite, renderer);
        Assert.Equal(renderer.MissingTextureId, sprite.AlbedoTextureId);
    }

    private static SpriteAtlas BuildRichAtlas(RecordingRenderer renderer)
    {
        var vfs = new VirtualFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "cyb-rich-" + Guid.NewGuid());
        var dir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(dir);
        using (var img = new Image<Rgba32>(32, 32))
            img.SaveAsPng(Path.Combine(dir, "p.png"));
        File.WriteAllText(Path.Combine(dir, "a.atlas.json"), """
            {
              "schemaVersion": 1,
              "pages": [{ "path": "Textures/Atlases/p.png" }],
              "regions": [{ "name": "icon", "pageIndex": 0, "pixelRect": [0, 0, 32, 32], "sizeWorld": [32, 32] }],
              "animations": { "talk": { "regionNames": ["icon"], "secondsPerFrame": 0.1, "loop": false } },
              "sheets": { "run": { "regionName": "icon", "columns": 2, "frameCount": 4, "secondsPerFrame": 0.05, "loop": true } }
            }
            """);
        vfs.Mount(root);
        var catalog = new SpriteAtlasCatalog(new AssetManager(vfs), () => null);
        try
        {
            return catalog.GetOrLoad("Textures/Atlases/a.atlas.json", renderer);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

public sealed class ModLoadContextSpriteAtlasTests
{
    [Fact]
    public void LoadSpriteAtlas_delegates_to_host_catalog()
    {
        var vfs = new VirtualFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "cyb-modctx-" + Guid.NewGuid());
        var dir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(dir);
        using (var img = new Image<Rgba32>(8, 8))
            img.SaveAsPng(Path.Combine(dir, "p.png"));
        File.WriteAllText(Path.Combine(dir, "t.atlas.json"), """
            {"schemaVersion":1,"pages":[{"path":"Textures/Atlases/p.png"}],"regions":[{"name":"a","pageIndex":0,"pixelRect":[0,0,8,8]}]}
            """);
        try
        {
            vfs.Mount(root);
            var host = new GameHostServices();
            host.Renderer = new RecordingRenderer();
            host.InitializeSpriteAssets(vfs);
            var ctx = new ModLoadContext(
                new ModManifest { Id = "test", ContentRoot = "Content" },
                root,
                vfs,
                new LocalizedContent(new LocalizationManager(), vfs, "en"),
                new World(),
                new SystemScheduler(new ParallelismSettings()),
                host);
            var atlas = ctx.LoadSpriteAtlas("Textures/Atlases/t.atlas.json");
            Assert.True(atlas.TryGetRegion("a", out _));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

public sealed class SpriteAtlasAsyncLoaderTests
{
    [Fact]
    public async Task LoadAsync_drains_on_render_thread_and_completes()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-async-" + Guid.NewGuid());
        var dir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(dir);
        using (var img = new Image<Rgba32>(16, 16))
            img.SaveAsPng(Path.Combine(dir, "p.png"));
        File.WriteAllText(Path.Combine(dir, "async.atlas.json"), """
            {"schemaVersion":1,"pages":[{"path":"Textures/Atlases/p.png"}],"regions":[{"name":"icon","pageIndex":0,"pixelRect":[0,0,16,16]}]}
            """);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var assets = new AssetManager(vfs);
            var catalog = new SpriteAtlasCatalog(assets, () => lc);
            var loader = new SpriteAtlasAsyncLoader(assets, () => lc);
            var renderer = new RecordingRenderer();

            var task = loader.LoadAsync("Textures/Atlases/async.atlas.json", renderer, catalog);
            await Task.Delay(200);
            loader.DrainPendingUploads(renderer, catalog);
            var atlas = await task;
            Assert.True(atlas.TryGetRegion("icon", out _));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_missing_manifest_still_completes_via_drain()
    {
        var vfs = new VirtualFileSystem();
        var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
        var assets = new AssetManager(vfs);
        var catalog = new SpriteAtlasCatalog(assets, () => lc);
        var loader = new SpriteAtlasAsyncLoader(assets, () => lc);
        var renderer = new RecordingRenderer();

        var task = loader.LoadAsync("Textures/Atlases/missing.atlas.json", renderer, catalog);
        await Task.Delay(100);
        loader.DrainPendingUploads(renderer, catalog);
        var atlas = await task;
        Assert.True(atlas.TryGetRegion("__missing__", out _));
    }

    [Fact]
    public async Task LoadAsync_missing_page_still_enqueues_for_drain()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-async2-" + Guid.NewGuid());
        var dir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "partial.atlas.json"), """
            {"schemaVersion":1,"pages":[{"path":"Textures/Atlases/missing-page.png"}],"regions":[{"name":"x","pageIndex":0,"pixelRect":[0,0,8,8]}]}
            """);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var assets = new AssetManager(vfs);
            var catalog = new SpriteAtlasCatalog(assets, () => lc);
            var loader = new SpriteAtlasAsyncLoader(assets, () => lc);
            var renderer = new RecordingRenderer();
            var task = loader.LoadAsync("Textures/Atlases/partial.atlas.json", renderer, catalog);
            await Task.Delay(150);
            loader.DrainPendingUploads(renderer, catalog);
            var atlas = await task;
            Assert.True(atlas.TryGetRegion("x", out _));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_tracks_pending_until_drained()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-async3-" + Guid.NewGuid());
        var dir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(dir);
        using (var img = new Image<Rgba32>(8, 8))
            img.SaveAsPng(Path.Combine(dir, "p.png"));
        File.WriteAllText(Path.Combine(dir, "q.atlas.json"), """
            {"schemaVersion":1,"pages":[{"path":"Textures/Atlases/p.png"}],"regions":[{"name":"a","pageIndex":0,"pixelRect":[0,0,8,8]}]}
            """);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var assets = new AssetManager(vfs);
            var catalog = new SpriteAtlasCatalog(assets, () => lc);
            var loader = new SpriteAtlasAsyncLoader(assets, () => lc);
            var renderer = new RecordingRenderer();
            var task = loader.LoadAsync("Textures/Atlases/q.atlas.json", renderer, catalog);
            for (var i = 0; i < 50 && loader.PendingCount == 0; i++)
                await Task.Delay(20);
            loader.DrainPendingUploads(renderer, catalog);
            var loaded = await task;
            Assert.True(loaded.TryGetRegion("a", out _));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_invalid_manifest_faults_background_task()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-async-bad-" + Guid.NewGuid());
        var dir = Path.Combine(root, "Textures", "Atlases");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "broken.atlas.json"), "not-json");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var catalog = new SpriteAtlasCatalog(assets, () => null);
            var loader = new SpriteAtlasAsyncLoader(assets, () => null);
            var renderer = new RecordingRenderer();
            var task = loader.LoadAsync("Textures/Atlases/broken.atlas.json", renderer, catalog);
            await Assert.ThrowsAnyAsync<Exception>(() => task);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task DrainPendingUploads_faults_when_catalog_missing()
    {
        var vfs = new VirtualFileSystem();
        var assets = new AssetManager(vfs);
        var loader = new SpriteAtlasAsyncLoader(assets, () => null);
        var renderer = new RecordingRenderer();
        var task = loader.LoadAsync("Textures/Atlases/missing.atlas.json", renderer, new SpriteAtlasCatalog(assets, () => null));
        await Task.Delay(100);
        loader.DrainPendingUploads(renderer, null!);
        await Assert.ThrowsAnyAsync<Exception>(() => task);
    }
}

public sealed class TextureImageProbeTests
{
    [Fact]
    public void TryGetImageDimensions_handles_missing_valid_and_invalid_paths()
    {
        var vfs = new VirtualFileSystem();
        Assert.False(TextureImageProbe.TryGetImageDimensions(vfs, "missing.png", out _, out _));

        var root = Path.Combine(Path.GetTempPath(), "cyb-probe-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "bad.png"), "not-image");
        File.WriteAllBytes(Path.Combine(root, "empty.png"), Array.Empty<byte>());
        using (var img = new Image<Rgba32>(6, 4))
            img.SaveAsPng(Path.Combine(root, "good.png"));
        try
        {
            vfs.Mount(root);
            Assert.False(TextureImageProbe.TryGetImageDimensions(vfs, "bad.png", out _, out _));
            Assert.False(TextureImageProbe.TryGetImageDimensions(vfs, "empty.png", out _, out _));
            Assert.True(TextureImageProbe.TryGetImageDimensions(vfs, "good.png", out var w, out var h));
            Assert.Equal(6, w);
            Assert.Equal(4, h);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

public sealed class TextureLoadDiagnosticsTests
{
    [Fact]
    public void LogFailureOnce_logs_first_call_only()
    {
        var assets = new AssetManager(new VirtualFileSystem());
        var renderer = new RecordingRenderer();
        _ = assets.TryLoadTexture("Textures/diag-" + Guid.NewGuid() + ".png", renderer);
        _ = assets.TryLoadTexture("Textures/diag-repeat.png", renderer);
        _ = assets.TryLoadTexture("Textures/diag-repeat.png", renderer);
    }
}
