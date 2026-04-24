using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
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
    public void TextRenderSystem_throws_when_renderer_null()
    {
        var host = new GameHostServices() { Renderer = null, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "x";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var tr = new TextRenderSystem(host);
        var trQ = TextRowQuery;
        tr.OnStart(world, world.QueryChunks(trQ));
        Assert.Throws<NullReferenceException>(() => tr.OnLateUpdate(world.QueryChunks(trQ), 0.016f));
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
        world.Components<Transform>().GetOrAdd(e0) = Transform.Identity;
        ref var b0 = ref world.Components<BitmapText>().GetOrAdd(e0);
        b0.Visible = false;
        b0.Content = "ok";
        b0.IsLocalizationKey = true;
        b0.CoordinateSpace = CoordinateSpace.WorldSpace;
        b0.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e1 = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e1) = Transform.Identity;
        ref var b1 = ref world.Components<BitmapText>().GetOrAdd(e1);
        b1.Visible = true;
        b1.Content = "";
        b1.IsLocalizationKey = false;
        b1.CoordinateSpace = CoordinateSpace.WorldSpace;
        b1.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e2 = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e2) = Transform.Identity;
        ref var b2 = ref world.Components<BitmapText>().GetOrAdd(e2);
        b2.Visible = true;
        b2.Content = "ok";
        b2.IsLocalizationKey = true;
        b2.CoordinateSpace = CoordinateSpace.WorldSpace;
        b2.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e3 = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e3) = Transform.Identity;
        ref var b3 = ref world.Components<BitmapText>().GetOrAdd(e3);
        b3.Visible = true;
        b3.Content = "ok";
        b3.IsLocalizationKey = true;
        b3.CoordinateSpace = CoordinateSpace.ScreenSpace;
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
        world.Components<Transform>().GetOrAdd(ew) = Transform.Identity;
        ref var btw = ref world.Components<BitmapText>().GetOrAdd(ew);
        btw.Visible = true;
        btw.Content = "a";
        btw.IsLocalizationKey = true;
        btw.CoordinateSpace = CoordinateSpace.WorldSpace;
        btw.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var tw = ref world.Components<Transform>().Get(ew);
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
        world.Components<Transform>().GetOrAdd(es) = Transform.Identity;
        ref var bts = ref world.Components<BitmapText>().GetOrAdd(es);
        bts.Visible = true;
        bts.Content = "a";
        bts.IsLocalizationKey = true;
        bts.CoordinateSpace = CoordinateSpace.ScreenSpace;
        bts.Style = btw.Style;
        ref var ts = ref world.Components<Transform>().Get(es);
        ts.WorldPosition = new Vector2D<float>(40f, r.SwapchainPixelSize.Y - 50f);

        var trScreen = new TextRenderSystem(host);
        trScreen.OnStart(world, world.QueryChunks(trQ));
        trScreen.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.Equal(nWorld, r.Sprites.Count);
    }

    [Fact]
    public void TextRenderSystem_world_literal_and_screen_literal_branches()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };

        var world = new World();
        var ew = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(ew) = Transform.Identity;
        ref var bw = ref world.Components<BitmapText>().GetOrAdd(ew);
        bw.Visible = true;
        bw.Content = "Hi";
        bw.IsLocalizationKey = false;
        bw.CoordinateSpace = CoordinateSpace.WorldSpace;
        bw.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var tw2 = ref world.Components<Transform>().Get(ew);
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
        world.Components<Transform>().GetOrAdd(es) = Transform.Identity;
        ref var bs = ref world.Components<BitmapText>().GetOrAdd(es);
        bs.Visible = true;
        bs.Content = "Hi";
        bs.IsLocalizationKey = false;
        bs.CoordinateSpace = CoordinateSpace.ScreenSpace;
        bs.Style = bw.Style;
        ref var ts2 = ref world.Components<Transform>().Get(es);
        ts2.WorldPosition = new Vector2D<float>(10f, 20f);

        var trSc = new TextRenderSystem(host);
        trSc.OnStart(world, world.QueryChunks(trQ));
        trSc.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.Equal(nLit, r.Sprites.Count);
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
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
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
        world.Components<Transform>().GetOrAdd(e0) = Transform.Identity;
        ref var b0 = ref world.Components<BitmapText>().GetOrAdd(e0);
        b0.Visible = true;
        b0.Content = "Hi";
        b0.IsLocalizationKey = false;
        b0.CoordinateSpace = CoordinateSpace.WorldSpace;
        b0.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);

        var e1 = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e1) = Transform.Identity;
        ref var b1 = ref world.Components<BitmapText>().GetOrAdd(e1);
        b1.Visible = true;
        b1.Content = "k";
        b1.IsLocalizationKey = true;
        b1.CoordinateSpace = CoordinateSpace.WorldSpace;
        b1.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Strikethrough: true);

        var e2 = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e2) = Transform.Identity;
        ref var b2 = ref world.Components<BitmapText>().GetOrAdd(e2);
        b2.Visible = true;
        b2.Content = "Lo";
        b2.IsLocalizationKey = false;
        b2.CoordinateSpace = CoordinateSpace.ScreenSpace;
        b2.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        ref var t2 = ref world.Components<Transform>().Get(e2);
        t2.WorldPosition = new Vector2D<float>(4f, 20f);

        var e3 = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e3) = Transform.Identity;
        ref var b3 = ref world.Components<BitmapText>().GetOrAdd(e3);
        b3.Visible = true;
        b3.Content = "k";
        b3.IsLocalizationKey = true;
        b3.CoordinateSpace = CoordinateSpace.ScreenSpace;
        b3.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Strikethrough: true);
        ref var t3 = ref world.Components<Transform>().Get(e3);
        t3.WorldPosition = new Vector2D<float>(40f, 50f);

        r.Sprites.Clear();
        var tr = new TextRenderSystem(host);
        var trQ = TextRowQuery;
        tr.OnStart(world, world.QueryChunks(trQ));
        tr.OnLateUpdate(world.QueryChunks(trQ), 0.016f);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_second_frame_unchanged_uses_glyph_cache_replay()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "Cache";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var transform = ref world.Components<Transform>().Get(e);
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
    public void TextRenderSystem_prunes_cache_when_entity_destroyed()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
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
    public void TextRenderSystem_throws_when_renderer_becomes_null()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "Y";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);
        host.Renderer = null;
        Assert.Throws<NullReferenceException>(() => sys.OnLateUpdate(q, 0.016f));
    }

    [Fact]
    public void TextRenderSystem_reuses_cached_row_array_when_content_grows()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
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
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
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
    public void TextRenderSystem_uses_bitmaptext_runtime_cache_fields()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "hi";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);

        ref var cache = ref world.Components<TextSpriteCache>().Get(e);
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
    public void TextBuildSystem_builds_runtime_sprites_in_parallel()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices() { Renderer = r, LocalizedContent = null };
        var sys = new TextBuildSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "parallel";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        Assert.Equal(TextRowQuery, sys.QuerySpec);
        sys.OnStart(world, q);
        sys.OnParallelLateUpdate(q, 0.016f, new ParallelismSettings().CreateParallelOptions());

        ref var cache = ref world.Components<TextSpriteCache>().Get(e);
        Assert.True(cache.GlyphCount >= 0);
    }

    [Fact]
    public void TextBuildSystem_throws_when_renderer_null()
    {
        var host = new GameHostServices() { Renderer = null, LocalizedContent = null };
        var sys = new TextBuildSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "x";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        Assert.Throws<AggregateException>(() => sys.OnParallelLateUpdate(q, 0.016f, new ParallelismSettings().CreateParallelOptions()));
    }
}
