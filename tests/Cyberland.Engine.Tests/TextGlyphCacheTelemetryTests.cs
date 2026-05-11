using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class TextGlyphCacheTelemetryTests
{
    [Fact]
    public void TextGlyphCache_miss_summaries_return_none_when_no_misses_tracked()
    {
        _ = TextGlyphCache.SnapshotAndResetMissCodepointSummary();
        _ = TextGlyphCache.SnapshotAndResetMissGlyphKeySummary();
        Assert.Equal("none", TextGlyphCache.SnapshotAndResetMissCodepointSummary());
        Assert.Equal("none", TextGlyphCache.SnapshotAndResetMissGlyphKeySummary());
    }

    [Fact]
    public void TextGlyphCache_SnapshotAndResetTelemetry_clears_accumulators()
    {
        _ = TextGlyphCache.SnapshotAndResetTelemetry();
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var style = new TextStyle(BuiltinFonts.UiSans, 18f, new Vector4D<float>(1f, 1f, 1f, 1f));
        TextRenderer.DrawLiteral(r, lib, cache, style, "Z", new Vector2D<float>(1f, 2f));
        var t = TextGlyphCache.SnapshotAndResetTelemetry();
        Assert.True(t.CacheMisses >= 1 || t.CacheHits >= 1);

        var t2 = TextGlyphCache.SnapshotAndResetTelemetry();
        Assert.Equal(0L, t2.CacheHits);
        Assert.Equal(0L, t2.CacheMisses);
        Assert.Equal(0L, t2.UploadCalls);
    }

    [Fact]
    public void TextGlyphCache_miss_summaries_include_drawn_codepoint()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var style = new TextStyle(BuiltinFonts.UiSans, 18f, new Vector4D<float>(1f, 1f, 1f, 1f));
        TextRenderer.DrawLiteral(r, lib, cache, style, "\u2C6F", new Vector2D<float>(1f, 2f));
        TextRenderer.DrawLiteral(r, lib, cache, style, "\u0243", new Vector2D<float>(5f, 6f));

        var cp = TextGlyphCache.SnapshotAndResetMissCodepointSummary();
        Assert.Contains("2C6F", cp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0243", cp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(',', cp);

        var keys = TextGlyphCache.SnapshotAndResetMissGlyphKeySummary();
        Assert.Contains("2C6F", keys, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0243", keys, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(',', keys);
    }

    [Fact]
    public void TextGlyphCache_SnapshotAndResetBakedImportCount_tracks_RegisterBakedGlyph()
    {
        _ = TextGlyphCache.SnapshotAndResetBakedImportCount();
        var loader = new BakedMsdfAtlasLoader();
        var r = new RecordingRenderer();
        var cache = new TextGlyphCache();
        var manifest = new BakedMsdfAtlasManifest
        {
            FamilyId = "telemetry.family",
            Face = "Regular",
            SizePixels = 14f,
            RasterRevision = GlyphRasterizer.RasterRevision,
            PageSizePixels = 16,
            Pages = [new BakedMsdfAtlasPageRef { Path = "p.png" }],
            Glyphs =
            [
                new BakedMsdfGlyphEntry
                {
                    CodePoint = 120,
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
        using var img = new Image<Rgba32>(16, 16);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        var png = ms.ToArray();
        var res = loader.LoadFromResource("telemetry", manifest, _ => png, r, cache);
        Assert.True(res.Loaded);
        Assert.True(TextGlyphCache.SnapshotAndResetBakedImportCount() >= 1);
    }

#if DEBUG
    [Fact]
    public void TextGlyphCache_runtime_msdf_fallback_warning_path_runs_when_enabled()
    {
        TextGlyphCache.ClearMsdfFallbackWarnOnceKeysForTests();
        var prev = TextGlyphCache.EnableMsdfFallbackConsoleWarnings;
        try
        {
            var r = new RecordingRenderer();
            var lib = new FontLibrary();
            BuiltinFonts.AddTo(lib);
            var style = new TextStyle(BuiltinFonts.UiSans, 99f, new Vector4D<float>(1f, 1f, 1f, 1f));
            TextGlyphCache.EnableMsdfFallbackConsoleWarnings = false;
            var cacheWarmup = new TextGlyphCache();
            Assert.True(cacheWarmup.TryGetGlyph(r, lib, in style, 'V', "V", out _));

            TextGlyphCache.EnableMsdfFallbackConsoleWarnings = true;
            var cache = new TextGlyphCache();
            Assert.True(cache.TryGetGlyph(r, lib, in style, 'W', "W", out _));
            var cache2 = new TextGlyphCache();
            Assert.True(cache2.TryGetGlyph(r, lib, in style, 'W', "W", out _));
        }
        finally
        {
            TextGlyphCache.EnableMsdfFallbackConsoleWarnings = prev;
            TextGlyphCache.ClearMsdfFallbackWarnOnceKeysForTests();
        }
    }
#endif
}
