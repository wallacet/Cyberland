using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;
using System.Threading.Tasks;
using TextRenderer = Cyberland.Engine.Rendering.Text.TextRenderer;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Glyph cache invariants after TryPrepare: submitted quad count must match <see cref="TextRenderer.FillGlyphRunGlyphs"/>
/// for the same resolved string (warm glyph atlas). Multi-frame tests mirror HUD timelines (long copy → idle frames → short).
/// </summary>
public sealed class TextRuntimeGlyphCacheTests
{
    private static readonly SystemQuerySpec TextRowQuery =
        SystemQuerySpec.All<BitmapText, Transform, TextBuildFingerprint, TextSpriteCache>();

    [Fact]
    public void TextRuntimeBuilder_SubmitSpriteCount_never_exceeds_resolved_utf16_length()
    {
        var inflated = new TextSpriteCache { GlyphCount = 999 };
        Assert.Equal(12, TextRuntimeBuilder.SubmitSpriteCount(in inflated, 12));
        Assert.Equal(0, TextRuntimeBuilder.SubmitSpriteCount(in inflated, 0));
        var emptyCache = default(TextSpriteCache);
        Assert.Equal(0, TextRuntimeBuilder.SubmitSpriteCount(in emptyCache, 5));
    }

    [Fact]
    public void TextRuntimeBuilder_TryPrepare_rebuilds_when_glyph_cache_content_version_advances()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(800, 600));

        var style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Ab",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            SortKey = 10f,
            Style = style
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        var fpAfterFirst = fingerprint;
        var glyphCountFirst = cache.GlyphCount;
        Assert.True(glyphCountFirst > 0);
        Assert.Equal(TextGlyphCache.ContentVersion, fpAfterFirst.GlyphContentVersion);

        // Any new renderable glyph data bumps the global version; BitmapText must not reuse CPU quads as if the prior
        // prepare still matches atlas contents (e.g. async MSDF page landed after a no-ink row).
        var dummyGlyph = new TextGlyphCache.CachedGlyph
        {
            TextureId = 99u,
            WidthPx = 1f,
            HeightPx = 1f,
            OffsetPenToCenterX = 0f,
            OffsetPenToCenterYWorld = 0f,
            AdvancePx = 1f,
            UvRect = default,
            MsdfPixelRange = 4f
        };
        host.TextGlyphCache.RegisterBakedGlyph(
            "__coverage_dummy_family__",
            FontFaceKind.Regular,
            1,
            0x7FFFFFFF,
            GlyphRasterizer.RasterRevision,
            dummyGlyph);
        Assert.NotEqual(fpAfterFirst.GlyphContentVersion, TextGlyphCache.ContentVersion);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(glyphCountFirst, cache.GlyphCount);
        Assert.Equal(TextGlyphCache.ContentVersion, fingerprint.GlyphContentVersion);
    }

    [Fact]
    public void BitmapTextPreparedRow_DiscardPrepared_clears_cache_and_fingerprint()
    {
        var cache = new TextSpriteCache
        {
            GlyphCount = 3,
            PenAfter = 12f,
            CachedGlyphs = new TextGlyphDrawRequest[4],
            BaselineAuthored = new Vector2D<float>(1f, 2f),
            Space = CoordinateSpace.ViewportSpace
        };
        var fp = new TextBuildFingerprint
        {
            ResolvedCharCount = 5,
            ResolvedContentHash64 = 123,
            BaselineWorldX = 7f
        };
        BitmapTextPreparedRow.DiscardPrepared(ref cache, ref fp);
        Assert.Equal(0, cache.GlyphCount);
        Assert.Null(cache.CachedGlyphs);
        Assert.Equal(0f, cache.PenAfter);
        Assert.Equal(0, fp.ResolvedCharCount);
        Assert.Equal(0u, fp.ResolvedContentHash64);
    }

    [Fact]
    public void TextRuntimeBuilder_TryPrepare_rebuilds_when_fingerprint_matches_but_glyph_count_exceeds_resolved_length()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(800, 600));

        var style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Hi",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            SortKey = 10f,
            Style = style
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        var fingerprintAfterHi = fingerprint;
        var glyphCountHi = cache.GlyphCount;
        Assert.InRange(glyphCountHi, 1, bt.Content.Length);

        bt.Content = "HelloWorld";
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.True(cache.GlyphCount > glyphCountHi);
        var inflatedGlyphCount = cache.GlyphCount;

        // Simulate skew (e.g. fingerprint reset without rebuilding): short string but GlyphCount still from long run.
        bt.Content = "Hi";
        fingerprint = fingerprintAfterHi;
        cache.GlyphCount = inflatedGlyphCount;

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.True(cache.GlyphCount <= bt.Content.Length);
        Assert.Equal(glyphCountHi, cache.GlyphCount);

        Span<TextGlyphDrawRequest> verify = stackalloc TextGlyphDrawRequest[Math.Max(1, bt.Content.Length)];
        var expectedN = TextRenderer.FillGlyphRunGlyphs(renderer, host.Fonts, host.TextGlyphCache, bt.Content, in style,
            cache.BaselineAuthored, 0f, bt.SortKey, verify, out _, CoordinateSpace.WorldSpace);
        Assert.Equal(expectedN, cache.GlyphCount);
        Assert.True(inflatedGlyphCount > glyphCountHi);
    }

    [Fact]
    public void TextRenderSystem_viewport_multi_frame_long_then_short_submits_expected_glyph_count_only()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(1280, 720));

        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var tf = ref world.Get<Transform>(e);
        tf.LocalPosition = new Vector2D<float>(40f, 74f);
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.SortKey = 801f;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 18f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        var sys = new TextRenderSystem(host);
        sys.OnStart(world, q);

        bt.Content =
            "Tutorial complete: hit target score, then enter the bright yellow gate zone at top-right.";
        for (var frame = 0; frame < 4; frame++)
        {
            r.Sprites.Clear();
            sys.OnLateUpdate(q, 1f / 60f);
            var cache = world.Get<TextSpriteCache>(e);
            Assert.True(cache.GlyphCount <= bt.Content.Length);
            Assert.Equal(cache.GlyphCount, r.Sprites.Count);
        }

        bt.Content = "Press R or Enter to restart.";
        r.Sprites.Clear();
        sys.OnLateUpdate(q, 1f / 60f);

        var cacheFinal = world.Get<TextSpriteCache>(e);
        Span<TextGlyphDrawRequest> verify = stackalloc TextGlyphDrawRequest[Math.Max(1, bt.Content.Length)];
        var expectedGlyphs = TextRenderer.FillGlyphRunGlyphs(r, host.Fonts, host.TextGlyphCache, bt.Content, in bt.Style,
            cacheFinal.BaselineAuthored, 0f, bt.SortKey, verify, out _, CoordinateSpace.ViewportSpace);

        Assert.Equal(expectedGlyphs, r.Sprites.Count);
        Assert.Equal(expectedGlyphs, cacheFinal.GlyphCount);
        Assert.True(cacheFinal.GlyphCount <= bt.Content.Length);
    }

    [Fact]
    public void TextRenderSystem_world_space_short_copy_submits_fewer_glyphs_than_prior_long_run()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };

        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bitmapText = ref world.GetOrAdd<BitmapText>(e);
        bitmapText.Visible = true;
        bitmapText.Content = new string('W', 48);
        bitmapText.IsLocalizationKey = false;
        bitmapText.CoordinateSpace = CoordinateSpace.WorldSpace;
        bitmapText.SortKey = 500f;
        bitmapText.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var tr = new TextRenderSystem(host);
        var q = world.QueryChunks(TextRowQuery);
        tr.OnStart(world, q);
        tr.OnLateUpdate(q, 1f / 60f);
        var longSubmitCount = r.Sprites.Count;
        Assert.True(longSubmitCount > 4);

        r.Sprites.Clear();
        bitmapText.Content = "OK";
        tr.OnLateUpdate(q, 1f / 60f);
        var shortSubmitCount = r.Sprites.Count;

        Assert.True(shortSubmitCount < longSubmitCount);
        Assert.InRange(shortSubmitCount, 1, bitmapText.Content.Length);

        Span<TextGlyphDrawRequest> verify = stackalloc TextGlyphDrawRequest[Math.Max(1, bitmapText.Content.Length)];
        var expected = TextRenderer.FillGlyphRunGlyphs(r, host.Fonts, host.TextGlyphCache, bitmapText.Content,
            in bitmapText.Style,
            world.Get<TextSpriteCache>(e).BaselineAuthored,
            0f,
            bitmapText.SortKey,
            verify,
            out _,
            CoordinateSpace.WorldSpace);
        Assert.Equal(expected, shortSubmitCount);
    }

    [Fact]
    public void TextRuntimeBuilder_swapchain_space_vertical_layout_matches_viewport_for_same_baseline()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(800, 600));
        var style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var transform = Transform.Identity;

        var cacheV = new TextSpriteCache();
        var fpV = default(TextBuildFingerprint);
        var btV = new BitmapText
        {
            Visible = true,
            Content = "A",
            IsLocalizationKey = false,
            Style = style,
            SortKey = 1f,
            CoordinateSpace = CoordinateSpace.ViewportSpace
        };
        var cacheS = new TextSpriteCache();
        var fpS = default(TextBuildFingerprint);
        var btS = new BitmapText
        {
            Visible = true,
            Content = "A",
            IsLocalizationKey = false,
            Style = style,
            SortKey = 1f,
            CoordinateSpace = CoordinateSpace.SwapchainSpace
        };

        Assert.True(TextRuntimeBuilder.TryPrepare(ref btV, ref fpV, ref cacheV, in transform, host, renderer, out _, out _));
        Assert.True(TextRuntimeBuilder.TryPrepare(ref btS, ref fpS, ref cacheS, in transform, host, renderer, out _, out _));
        Assert.True(cacheV.GlyphCount > 0 && cacheS.GlyphCount > 0);
        Assert.Equal(cacheV.CachedGlyphs![0].Center.Y, cacheS.CachedGlyphs![0].Center.Y, 4);
        Assert.Equal(cacheV.CachedGlyphs[0].Center.X, cacheS.CachedGlyphs[0].Center.X, 4);
    }

    [Fact]
    public void TextRuntimeBuilder_TryPrepare_viewport_rebuilds_each_call_so_glyph_count_tracks_content()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(800, 600));

        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = new string('X', 30),
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            SortKey = 800f,
            Style = style
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out var space));
        Assert.Equal(CoordinateSpace.ViewportSpace, space);
        var nLong = cache.GlyphCount;

        bt.Content = "--";
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.True(cache.GlyphCount < nLong);
        Assert.True(cache.GlyphCount <= bt.Content.Length);
        Assert.NotEqual(default, fingerprint.ResolvedContentHash64);
    }

    [Fact]
    public void TextRuntimeBuilder_discards_prepared_row_when_viewport_framebuffer_size_changes()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(800, 600));

        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Hud",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            SortKey = 12f,
            Style = style
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(800, fingerprint.FramebufferW);
        Assert.Equal(600, fingerprint.FramebufferH);

        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(1280, 720));
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(1280, fingerprint.FramebufferW);
        Assert.Equal(720, fingerprint.FramebufferH);
    }

    [Fact]
    public void TextRuntimeBuilder_reuses_cached_glyphs_when_only_baseline_changes()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        var style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Move",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            SortKey = 4f,
            Style = style
        };

        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);
        var t0 = Transform.Identity;

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in t0, host, renderer, out var baseline, out _));
        Assert.NotNull(cache.CachedGlyphs);
        Assert.True(cache.GlyphCount > 0);
        var firstArray = cache.CachedGlyphs!;
        var firstCenter = firstArray[0].Center;

        // Simulate a previously prepared row stored at a different baseline while content/style/viewport inputs still match.
        var offset = new Vector2D<float>(25f, 8f);
        cache.BaselineAuthored = baseline + offset;
        fingerprint.BaselineWorldX = cache.BaselineAuthored.X;
        fingerprint.BaselineWorldY = cache.BaselineAuthored.Y;
        firstArray[0].Center += offset;

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in t0, host, renderer, out _, out _));

        Assert.Same(firstArray, cache.CachedGlyphs);
        var shiftedBackCenter = cache.CachedGlyphs![0].Center;
        Assert.Equal(firstCenter.X, shiftedBackCenter.X, 4);
        Assert.Equal(firstCenter.Y, shiftedBackCenter.Y, 4);
    }

    [Fact]
    public void TextRuntimeBuilder_viewport_reuse_applies_rounded_baseline_delta()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(640, 360));
        var style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Move",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            SortKey = 4f,
            Style = style
        };

        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);
        var transform = Transform.Identity;
        transform.LocalPosition = new Vector2D<float>(10.2f, 20.2f);
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer, out _, out _));
        var firstCenter = cache.CachedGlyphs![0].Center;

        transform.LocalPosition = new Vector2D<float>(11.7f, 21.7f);
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer, out _, out _));
        var secondCenter = cache.CachedGlyphs![0].Center;

        // Rounded authored baseline moves from (10,20) to (12,22), so glyph centers shift by +2,+2.
        Assert.Equal(firstCenter.X + 2f, secondCenter.X, 3);
        Assert.Equal(firstCenter.Y + 2f, secondCenter.Y, 3);
    }

    [Fact]
    public void TextRuntimeBuilder_discards_prepared_row_when_sort_key_changes()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(800, 600));
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Sort",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            SortKey = 1f,
            Style = style
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(1f, fingerprint.SortKey);

        bt.SortKey = 2f;
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(2f, fingerprint.SortKey);
    }

    [Fact]
    public void TextRuntimeBuilder_discards_prepared_row_when_text_style_changes()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Styled",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            SortKey = 3f,
            Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        var firstStyleHash = fingerprint.StyleHash;

        bt.Style = bt.Style with { SizePixels = 22f };
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.NotEqual(firstStyleHash, fingerprint.StyleHash);
    }

    [Fact]
    public void TextRuntimeBuilder_discards_prepared_row_when_coordinate_space_changes()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(800, 600));
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Space",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            SortKey = 5f,
            Style = style
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(CoordinateSpace.ViewportSpace, fingerprint.CoordinateSpace);

        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(CoordinateSpace.WorldSpace, fingerprint.CoordinateSpace);
    }

    [Fact]
    public void TextRuntimeBuilder_discards_when_stored_hash_does_not_match_resolved_string_at_same_length()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        var bt = new BitmapText
        {
            Visible = true,
            Content = "abc",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            SortKey = 1f,
            Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        var goodHash = fingerprint.ResolvedContentHash64;

        fingerprint.ResolvedContentHash64 = 1UL;
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(goodHash, fingerprint.ResolvedContentHash64);
    }

    [Fact]
    public void TextRuntimeBuilder_clears_unused_glyph_slots_when_quad_count_less_than_utf16_capacity()
    {
        // One BMP emoji surrogate pair => 2 UTF-16 code units; shaping typically emits one quad so BuildGlyphSprites must
        // clear destination slots after the active prefix (see TextRuntimeBuilder tail Array.Clear).
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        var bt = new BitmapText
        {
            Visible = true,
            Content = "\uD83D\uDE00",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            SortKey = 2f,
            Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.Equal(2, bt.Content.Length);
        Assert.NotNull(cache.CachedGlyphs);
        Assert.True(cache.GlyphCount < cache.CachedGlyphs!.Length);
    }

    [Fact]
    public void TextGlyphCache_parallel_same_glyph_requests_return_consistent_cached_result()
    {
        var renderer = new RecordingRenderer();
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();
        var style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));

        TextGlyphCache.CachedGlyph[] results = new TextGlyphCache.CachedGlyph[2];
        Parallel.For(0, 2, i =>
        {
            Assert.True(cache.TryGetGlyph(renderer, fonts, in style, 'A', "A", out var glyph));
            results[i] = glyph;
        });

        Assert.Equal(results[0].TextureId, results[1].TextureId);
        Assert.Equal(results[0].UvRect, results[1].UvRect);
    }

    [Fact]
    public void TextRuntimeBuilder_matching_fingerprint_with_null_cache_falls_back_to_rebuild()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "Cache",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            SortKey = 4f,
            Style = style
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        cache.CachedGlyphs = null;

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer,
            out _, out _));
        Assert.NotNull(cache.CachedGlyphs);
        Assert.True(cache.GlyphCount > 0);
    }

    [Fact]
    public void TextRuntimeBuilder_rebuild_discards_oversized_cached_glyph_array_when_content_is_much_shorter()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var bt = new BitmapText
        {
            Visible = true,
            Content = "A",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            SortKey = 1f,
            Style = style
        };
        var transform = Transform.Identity;
        var cache = new TextSpriteCache();
        var fingerprint = default(TextBuildFingerprint);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer, out _, out _));
        cache.CachedGlyphs = new TextGlyphDrawRequest[32];
        cache.GlyphCount = 99; // Invalid for resolved length=1: forces TryReusePreparedGlyphs line-161 guard path.
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, host, renderer, out _, out _));
        Assert.NotNull(cache.CachedGlyphs);
        Assert.True(cache.CachedGlyphs!.Length <= 2, "Oversized glyph cache should shrink to current content capacity.");
        Assert.True(cache.GlyphCount <= bt.Content.Length);
    }
}
