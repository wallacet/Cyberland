using System.Threading.Tasks;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class TextRenderSystemTests
{
    private static readonly SystemQuerySpec TextRowQuery =
        SystemQuerySpec.All<BitmapText, Transform, TextBuildFingerprint, TextSpriteCache>();

    [Fact]
    public void TextRenderSystem_runs_with_assigned_renderer()
    {
        var host = new GameHostServices() { Renderer = new RecordingRenderer(), LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "x";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var tr = new TextRenderSystem(host);
        var trQ = TextRowQuery;
        tr.OnStart(world, world.QueryChunks(trQ));
        tr.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
    }

    [Fact]
    public void TextRenderSystem_skips_invisible_and_empty_and_missing_localization_for_keys()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"ok":"v"}"""u8.ToArray());
        var lc = new LocalizedContent(loc, new VirtualFileSystem(), "en");
        var host = new GameHostServices()
        {
            Renderer = r,
            LocalizedContent = null
        };

        var world = new World();
        var e0 = world.CreateEntity();
        world.GetOrAdd<Transform>(e0) = Transform.Identity;
        ref var b0 = ref world.GetOrAdd<BitmapText>(e0);
        b0.Visible = false;
        b0.Content = "ok";
        b0.IsLocalizationKey = true;
        b0.CoordinateSpace = CoordinateSpace.WorldSpace;
        b0.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e1 = world.CreateEntity();
        world.GetOrAdd<Transform>(e1) = Transform.Identity;
        ref var b1 = ref world.GetOrAdd<BitmapText>(e1);
        b1.Visible = true;
        b1.Content = "";
        b1.IsLocalizationKey = false;
        b1.CoordinateSpace = CoordinateSpace.WorldSpace;
        b1.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e2 = world.CreateEntity();
        world.GetOrAdd<Transform>(e2) = Transform.Identity;
        ref var b2 = ref world.GetOrAdd<BitmapText>(e2);
        b2.Visible = true;
        b2.Content = "ok";
        b2.IsLocalizationKey = true;
        b2.CoordinateSpace = CoordinateSpace.WorldSpace;
        b2.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e3 = world.CreateEntity();
        world.GetOrAdd<Transform>(e3) = Transform.Identity;
        ref var b3 = ref world.GetOrAdd<BitmapText>(e3);
        b3.Visible = true;
        b3.Content = "ok";
        b3.IsLocalizationKey = true;
        b3.CoordinateSpace = CoordinateSpace.ViewportSpace;
        b3.Style = b2.Style;

        var tr = new TextRenderSystem(host);
        var trQ = TextRowQuery;
        tr.OnStart(world, world.QueryChunks(trQ));
        tr.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.Empty(r.Sprites);

        host.LocalizedContent = lc;
        var tr2 = new TextRenderSystem(host);
        tr2.OnStart(world, world.QueryChunks(trQ));
        tr2.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_world_and_screen_baseline_branches_submit()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"a":"Z"}"""u8.ToArray());
        var lc = new LocalizedContent(loc, new VirtualFileSystem(), "en");
        var host = new GameHostServices() { Renderer = r, LocalizedContent = lc };

        var world = new World();
        var ew = world.CreateEntity();
        world.GetOrAdd<Transform>(ew) = Transform.Identity;
        ref var btw = ref world.GetOrAdd<BitmapText>(ew);
        btw.Visible = true;
        btw.Content = "a";
        btw.IsLocalizationKey = true;
        btw.CoordinateSpace = CoordinateSpace.WorldSpace;
        btw.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var tw = ref world.Get<Transform>(ew);
        tw.WorldPosition = new Vector2D<float>(40f, 50f);

        r.Sprites.Clear();
        var tr = new TextRenderSystem(host);
        var trQ = TextRowQuery;
        tr.OnStart(world, world.QueryChunks(trQ));
        tr.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        var nWorld = r.Sprites.Count;
        Assert.True(nWorld > 0);

        r.Sprites.Clear();
        btw.Visible = false;
        var es = world.CreateEntity();
        world.GetOrAdd<Transform>(es) = Transform.Identity;
        ref var bts = ref world.GetOrAdd<BitmapText>(es);
        bts.Visible = true;
        bts.Content = "a";
        bts.IsLocalizationKey = true;
        bts.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bts.Style = btw.Style;
        ref var ts = ref world.Get<Transform>(es);
        ts.WorldPosition = new Vector2D<float>(40f, r.SwapchainPixelSize.Y - 50f);

        var trScreen = new TextRenderSystem(host);
        trScreen.OnStart(world, world.QueryChunks(trQ));
        trScreen.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.True(r.Sprites.Count >= nWorld);
    }

    [Fact]
    public void TextRenderSystem_world_literal_and_screen_literal_branches()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };

        var world = new World();
        var ew = world.CreateEntity();
        world.GetOrAdd<Transform>(ew) = Transform.Identity;
        ref var bw = ref world.GetOrAdd<BitmapText>(ew);
        bw.Visible = true;
        bw.Content = "Hi";
        bw.IsLocalizationKey = false;
        bw.CoordinateSpace = CoordinateSpace.WorldSpace;
        bw.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var tw2 = ref world.Get<Transform>(ew);
        tw2.WorldPosition = new Vector2D<float>(10f, 20f);

        r.Sprites.Clear();
        var tr = new TextRenderSystem(host);
        var trQ = TextRowQuery;
        tr.OnStart(world, world.QueryChunks(trQ));
        tr.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        var nLit = r.Sprites.Count;
        Assert.True(nLit > 0);

        r.Sprites.Clear();
        bw.Visible = false;
        var es = world.CreateEntity();
        world.GetOrAdd<Transform>(es) = Transform.Identity;
        ref var bs = ref world.GetOrAdd<BitmapText>(es);
        bs.Visible = true;
        bs.Content = "Hi";
        bs.IsLocalizationKey = false;
        bs.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bs.Style = bw.Style;
        ref var ts2 = ref world.Get<Transform>(es);
        ts2.WorldPosition = new Vector2D<float>(10f, 20f);

        var trSc = new TextRenderSystem(host);
        trSc.OnStart(world, world.QueryChunks(trQ));
        trSc.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.True(r.Sprites.Count >= nLit);
    }

    [Fact]
    public void TextRenderSystem_submits_text_glyph_queue_when_sprite_mirroring_disabled()
    {
        var r = new RecordingRenderer { MirrorTextGlyphsIntoSprites = false };
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "GlyphPath";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        var sys = new TextRenderSystem(host);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);

        Assert.NotEmpty(r.TextGlyphs);
        Assert.Empty(r.MirroredTextSprites);
    }

    [Fact]
    public void TextRenderSystem_skips_when_localization_key_resolves_to_empty()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"empty":""}"""u8.ToArray());
        var lc = new LocalizedContent(loc, new VirtualFileSystem(), "en");
        var host = new GameHostServices() { Renderer = r, LocalizedContent = lc };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "empty";
        bt.IsLocalizationKey = true;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var tr = new TextRenderSystem(host);
        var trQ = TextRowQuery;
        tr.OnStart(world, world.QueryChunks(trQ));
        tr.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_decoration_branches_world_and_screen_literal_and_localized()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"k":"Z"}"""u8.ToArray());
        var lc = new LocalizedContent(loc, new VirtualFileSystem(), "en");
        var host = new GameHostServices() { Renderer = r, LocalizedContent = lc };
        var world = new World();

        var e0 = world.CreateEntity();
        world.GetOrAdd<Transform>(e0) = Transform.Identity;
        ref var b0 = ref world.GetOrAdd<BitmapText>(e0);
        b0.Visible = true;
        b0.Content = "Hi";
        b0.IsLocalizationKey = false;
        b0.CoordinateSpace = CoordinateSpace.WorldSpace;
        b0.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);

        var e1 = world.CreateEntity();
        world.GetOrAdd<Transform>(e1) = Transform.Identity;
        ref var b1 = ref world.GetOrAdd<BitmapText>(e1);
        b1.Visible = true;
        b1.Content = "k";
        b1.IsLocalizationKey = true;
        b1.CoordinateSpace = CoordinateSpace.WorldSpace;
        b1.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Strikethrough: true);

        var e2 = world.CreateEntity();
        world.GetOrAdd<Transform>(e2) = Transform.Identity;
        ref var b2 = ref world.GetOrAdd<BitmapText>(e2);
        b2.Visible = true;
        b2.Content = "Lo";
        b2.IsLocalizationKey = false;
        b2.CoordinateSpace = CoordinateSpace.ViewportSpace;
        b2.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        ref var t2 = ref world.Get<Transform>(e2);
        t2.WorldPosition = new Vector2D<float>(4f, 20f);

        var e3 = world.CreateEntity();
        world.GetOrAdd<Transform>(e3) = Transform.Identity;
        ref var b3 = ref world.GetOrAdd<BitmapText>(e3);
        b3.Visible = true;
        b3.Content = "k";
        b3.IsLocalizationKey = true;
        b3.CoordinateSpace = CoordinateSpace.ViewportSpace;
        b3.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Strikethrough: true);
        ref var t3 = ref world.Get<Transform>(e3);
        t3.WorldPosition = new Vector2D<float>(40f, 50f);

        r.Sprites.Clear();
        var tr = new TextRenderSystem(host);
        var trQ = TextRowQuery;
        tr.OnStart(world, world.QueryChunks(trQ));
        tr.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_viewport_decorations_apply_viewport_clip_rect()
    {
        var r = new RecordingRenderer { MirrorTextGlyphsIntoSprites = false };
        r.ActiveCameraViewportSize = new Vector2D<int>(320, 180);
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var transform = ref world.Get<Transform>(e);
        transform.WorldPosition = new Vector2D<float>(12f, 16f);
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "Clip me";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);

        var sys = new TextRenderSystem(host);
        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);

        Assert.NotEmpty(r.Sprites);
        Assert.All(r.Sprites, s => Assert.True(s.ViewportClipEnabled));
        Assert.All(r.Sprites, s =>
        {
            Assert.Equal(0f, s.ViewportClipRect.X);
            Assert.Equal(0f, s.ViewportClipRect.Y);
            Assert.Equal(320f, s.ViewportClipRect.Width);
            Assert.Equal(180f, s.ViewportClipRect.Height);
        });
    }

    [Fact]
    public void TextRenderSystem_swapchain_decorations_use_swapchain_clip_rect()
    {
        var r = new RecordingRenderer { MirrorTextGlyphsIntoSprites = false };
        r.SwapchainPixelSize = new Vector2D<int>(1024, 576);
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "Swap clip";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.SwapchainSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Strikethrough: true);

        var sys = new TextRenderSystem(host);
        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);

        Assert.NotEmpty(r.Sprites);
        Assert.All(r.Sprites, s => Assert.True(s.ViewportClipEnabled));
        Assert.All(r.Sprites, s =>
        {
            Assert.Equal(1024f, s.ViewportClipRect.Width);
            Assert.Equal(576f, s.ViewportClipRect.Height);
        });
    }

    [Fact]
    public void TextRenderSystem_viewport_decorations_skip_clip_when_viewport_size_is_invalid()
    {
        var r = new RecordingRenderer { MirrorTextGlyphsIntoSprites = false };
        r.ActiveCameraViewportSize = new Vector2D<int>(0, 0);
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "No clip";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);

        var sys = new TextRenderSystem(host);
        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);

        Assert.NotEmpty(r.Sprites);
        Assert.All(r.Sprites, s => Assert.False(s.ViewportClipEnabled));
    }

    [Fact]
    public void TextRenderSystem_second_frame_unchanged_uses_glyph_cache_replay()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "Cache";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var transform = ref world.Get<Transform>(e);
        transform.WorldPosition = new Vector2D<float>(1f, 2f);

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        var nFirst = r.Sprites.Count;
        Assert.True(nFirst > 0);
        r.Sprites.Clear();
        sys.OnLateUpdate(q, 0.016f);
        Assert.Equal(nFirst, r.Sprites.Count);
    }

    [Fact]
    public void TextRenderSystem_does_not_submit_cached_glyphs_when_visible_becomes_false()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "Hello";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var transform = ref world.Get<Transform>(e);
        transform.WorldPosition = new Vector2D<float>(1f, 2f);

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        Assert.NotEmpty(r.Sprites);

        bt.Visible = false;
        r.Sprites.Clear();
        sys.OnLateUpdate(q, 0.016f);
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_prunes_cache_when_entity_destroyed()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "X";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        world.DestroyEntity(e);
        sys.OnLateUpdate(q, 0.016f);
    }

    [Fact]
    public void TextRenderSystem_continues_when_renderer_instance_changes()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "Y";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        host.Renderer = new RecordingRenderer();
        sys.OnLateUpdate(q, 0.016f);
    }

    [Fact]
    public void TextRenderSystem_reuses_cached_row_array_when_content_grows()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "A";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        bt.Content = "AB";
        r.Sprites.Clear();
        sys.OnLateUpdate(q, 0.016f);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_OnLateUpdate_renders_without_column_map_cache()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "Z";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_OnParallelLateUpdate_partitions_large_chunk_when_parallelism_gt_one()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        for (var i = 0; i < 65; i++)
        {
            var e = world.CreateEntity();
            world.GetOrAdd<Transform>(e) = Transform.Identity;
            ref var bt = ref world.GetOrAdd<BitmapText>(e);
            bt.Visible = true;
            bt.Content = "x";
            bt.IsLocalizationKey = false;
            bt.CoordinateSpace = CoordinateSpace.WorldSpace;
            bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        }

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnParallelLateUpdate(q, 0.016f, new ParallelOptions { MaxDegreeOfParallelism = 2 });
        Assert.NotEmpty(r.TextGlyphs);
    }

    [Fact]
    public void TextRenderSystem_uses_bitmaptext_runtime_cache_fields()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "hi";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);

        ref var cache = ref world.Get<TextSpriteCache>(e);
        Assert.True(cache.GlyphCount > 0);
        Assert.NotNull(cache.CachedGlyphs);
    }

    [Fact]
    public void TextRenderer_DrawLocalizedScreen_is_covered()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"hud":"HUD"}"""u8.ToArray());
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        TextRenderer.DrawLocalizedScreen(
            r,
            new FontLibrary(),
            new TextGlyphCache(),
            loc,
            in style,
            "hud",
            new Vector2D<float>(12f, 24f),
            300f);
    }

    [Fact]
    public void TextRuntimeBuilder_TryPrepare_fills_glyph_cache_without_TextRenderSystem()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "direct";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var fp = ref world.Get<TextBuildFingerprint>(e);
        ref var cache = ref world.Get<TextSpriteCache>(e);
        ref readonly var tf = ref world.Get<Transform>(e);
        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fp, ref cache, in tf, host, r, out _, out _));
        Assert.True(cache.GlyphCount > 0);
    }

    [Fact]
    public void TextRuntimeBuilder_TryPrepare_returns_false_without_throw_when_renderer_null()
    {
        var host = new GameHostServices() { Renderer = new RecordingRenderer(), LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "x";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var fp = ref world.Get<TextBuildFingerprint>(e);
        ref var cache = ref world.Get<TextSpriteCache>(e);
        ref readonly var tf = ref world.Get<Transform>(e);
        Assert.False(TextRuntimeBuilder.TryPrepare(ref bt, ref fp, ref cache, in tf, host, null, out _, out _));
        Assert.Equal(0, fp.ResolvedCharCount);
    }

    [Fact]
    public void TextRuntimeBuilder_second_prepare_keeps_array_when_capacity_still_fits()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "stable";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        ref var fp = ref world.Get<TextBuildFingerprint>(e);
        ref var cache = ref world.Get<TextSpriteCache>(e);
        ref readonly var tf = ref world.Get<Transform>(e);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fp, ref cache, in tf, host, r, out _, out _));
        var firstHash = fp.ResolvedContentHash64;
        var firstArray = cache.CachedGlyphs;

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fp, ref cache, in tf, host, r, out _, out _));
        Assert.Equal(firstHash, fp.ResolvedContentHash64);
        Assert.Same(firstArray, cache.CachedGlyphs);
    }

    [Fact]
    public void TextRuntimeBuilder_fingerprint_tracks_baseline_when_transform_moves_with_same_content()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var tf0 = ref world.Get<Transform>(e);
        tf0.LocalPosition = new Vector2D<float>(10f, 20f);

        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "same";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        ref var fp = ref world.Get<TextBuildFingerprint>(e);
        ref var cache = ref world.Get<TextSpriteCache>(e);
        ref readonly var tf = ref world.Get<Transform>(e);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fp, ref cache, in tf, host, r, out _, out _));
        Assert.Equal(10f, fp.BaselineWorldX, 3);
        Assert.Equal(20f, fp.BaselineWorldY, 3);

        ref var tf1 = ref world.Get<Transform>(e);
        tf1.LocalPosition = new Vector2D<float>(90f, 120f);

        Assert.True(TextRuntimeBuilder.TryPrepare(ref bt, ref fp, ref cache, in tf, host, r, out _, out _));
        Assert.Equal(90f, fp.BaselineWorldX, 3);
        Assert.Equal(120f, fp.BaselineWorldY, 3);
    }

    [Fact]
    public void TextRenderSystem_zeros_trailing_cached_glyph_slots_when_content_shortens()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "ABCDEFGHIJ";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var transform = ref world.Get<Transform>(e);
        transform.WorldPosition = new Vector2D<float>(0f, 0f);

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        var cacheAfterLong = world.Get<TextSpriteCache>(e);
        Assert.NotNull(cacheAfterLong.CachedGlyphs);
        Assert.True(cacheAfterLong.CachedGlyphs!.Length >= cacheAfterLong.GlyphCount);

        bt.Content = "A";
        r.Sprites.Clear();
        sys.OnLateUpdate(q, 0.016f);
        var cacheAfterShort = world.Get<TextSpriteCache>(e);
        Assert.Equal(1, cacheAfterShort.GlyphCount);
        Assert.NotNull(cacheAfterShort.CachedGlyphs);
        Assert.Single(cacheAfterShort.CachedGlyphs);
        Assert.Single(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_viewport_space_zeros_trailing_cached_glyph_slots_when_content_shortens()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(800, 600));

        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var transform = ref world.Get<Transform>(e);
        transform.LocalPosition = new Vector2D<float>(4f, 8f);
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "ABCDEFGHIJ";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        var sys = new TextRenderSystem(host);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        var cacheLong = world.Get<TextSpriteCache>(e);
        Assert.True(cacheLong.GlyphCount > 1);

        bt.Content = "A";
        r.Sprites.Clear();
        sys.OnLateUpdate(q, 0.016f);
        var cacheShort = world.Get<TextSpriteCache>(e);
        Assert.True(cacheShort.GlyphCount < cacheLong.GlyphCount);
        Assert.Equal(cacheShort.GlyphCount, cacheShort.CachedGlyphs!.Length);
        Assert.Single(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_viewport_shrink_replaces_oversized_glyph_buffer_after_long_tutorial_style_run()
    {
        // Mirrors HUD swapping a long localized line for a short one (MouseChase tutorial.complete → Step 1…).
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(1280, 720));

        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var transform = ref world.Get<Transform>(e);
        transform.LocalPosition = new Vector2D<float>(40f, 74f);
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content =
            "Tutorial complete: hit target score, then enter the bright yellow gate zone at top-right.";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.SortKey = 801f;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 18f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        var sys = new TextRenderSystem(host);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        var nLong = r.Sprites.Count;
        var lenLongBuf = world.Get<TextSpriteCache>(e).CachedGlyphs!.Length;

        bt.Content = "Step 1: Enter the green zone.";
        r.Sprites.Clear();
        sys.OnLateUpdate(q, 0.016f);

        var cache = world.Get<TextSpriteCache>(e);
        Assert.True(r.Sprites.Count < nLong);
        Assert.Equal(cache.GlyphCount, r.Sprites.Count);
        Assert.True(cache.CachedGlyphs!.Length < lenLongBuf);
        Assert.Equal(bt.Content.Length, cache.CachedGlyphs.Length);
    }

    [Fact]
    public void TextRenderSystem_clears_tail_of_nonshrunk_glyph_buffer_when_run_shortens_moderately()
    {
        // shorter resolved copy invalidates the prepared pipeline → discard resizes the backing array to the new run.
        // FillGlyphRunSprites still clears unused indices within the new capacity.
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };

        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var tf = ref world.Get<Transform>(e);
        tf.WorldPosition = new Vector2D<float>(0f, 0f);
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "ABCDEFGHIJKLMNOPQRSTUVWXY";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        var sys = new TextRenderSystem(host);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        Assert.Equal(25, world.Get<TextSpriteCache>(e).CachedGlyphs!.Length);

        bt.Content = "ABCDEFGHIJKLMNOPQRST";
        r.Sprites.Clear();
        sys.OnLateUpdate(q, 0.016f);
        var cache = world.Get<TextSpriteCache>(e);
        Assert.Equal(20, cache.CachedGlyphs!.Length);
        Assert.Equal(20, cache.GlyphCount);
        Assert.Equal(20, r.Sprites.Count);
    }

    [Fact]
    public void TextRenderSystem_discards_glyph_cache_when_row_turns_invisible_after_being_visible()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = "HUD";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        var sys = new TextRenderSystem(host);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        Assert.True(world.Get<TextSpriteCache>(e).GlyphCount > 0);

        bt.Visible = false;
        sys.OnLateUpdate(q, 0.016f);
        Assert.Equal(0, world.Get<TextSpriteCache>(e).GlyphCount);
        Assert.Null(world.Get<TextSpriteCache>(e).CachedGlyphs);
        Assert.Equal(0, world.Get<TextBuildFingerprint>(e).ResolvedCharCount);
    }
}
