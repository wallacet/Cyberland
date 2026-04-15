using System.Reflection;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class TextRenderSystemTests
{
    [Fact]
    public void TextRenderSystem_skips_when_renderer_null()
    {
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = null, LocalizedContent = null };
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "x";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
    }

    [Fact]
    public void TextRenderSystem_skips_invisible_and_empty_and_missing_localization_for_keys()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"ok":"v"}"""u8.ToArray());
        var lc = new LocalizedContent(loc, new VirtualFileSystem(), "en");
        var host = new GameHostServices(new KeyBindingStore())
        {
            Renderer = r,
            LocalizedContent = null
        };

        var world = new World();
        var e0 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e0);
        ref var b0 = ref world.Components<BitmapText>().GetOrAdd(e0);
        b0.Visible = false;
        b0.Content = "ok";
        b0.IsLocalizationKey = true;
        b0.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        b0.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e1 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e1);
        ref var b1 = ref world.Components<BitmapText>().GetOrAdd(e1);
        b1.Visible = true;
        b1.Content = "";
        b1.IsLocalizationKey = false;
        b1.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        b1.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e2 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e2);
        ref var b2 = ref world.Components<BitmapText>().GetOrAdd(e2);
        b2.Visible = true;
        b2.Content = "ok";
        b2.IsLocalizationKey = true;
        b2.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        b2.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e3 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e3);
        ref var b3 = ref world.Components<BitmapText>().GetOrAdd(e3);
        b3.Visible = true;
        b3.Content = "ok";
        b3.IsLocalizationKey = true;
        b3.CoordinateSpace = TextCoordinateSpace.ScreenPixels;
        b3.Style = b2.Style;

        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
        Assert.Empty(r.Sprites);

        host.LocalizedContent = lc;
        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_world_and_screen_baseline_branches_submit()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"a":"Z"}"""u8.ToArray());
        var lc = new LocalizedContent(loc, new VirtualFileSystem(), "en");
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = r, LocalizedContent = lc };

        var world = new World();
        var ew = world.CreateEntity();
        world.Components<Position>().GetOrAdd(ew);
        ref var btw = ref world.Components<BitmapText>().GetOrAdd(ew);
        btw.Visible = true;
        btw.Content = "a";
        btw.IsLocalizationKey = true;
        btw.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        btw.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var pw = ref world.Components<Position>().Get(ew);
        pw.X = 40f;
        pw.Y = 50f;

        r.Sprites.Clear();
        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
        var nWorld = r.Sprites.Count;
        Assert.True(nWorld > 0);

        r.Sprites.Clear();
        btw.Visible = false;
        var es = world.CreateEntity();
        world.Components<Position>().GetOrAdd(es);
        ref var bts = ref world.Components<BitmapText>().GetOrAdd(es);
        bts.Visible = true;
        bts.Content = "a";
        bts.IsLocalizationKey = true;
        bts.CoordinateSpace = TextCoordinateSpace.ScreenPixels;
        bts.Style = btw.Style;
        ref var ps = ref world.Components<Position>().Get(es);
        ps.X = 40f;
        ps.Y = r.SwapchainPixelSize.Y - 50f;

        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
        Assert.Equal(nWorld, r.Sprites.Count);
    }

    [Fact]
    public void TextRenderSystem_world_literal_and_screen_literal_branches()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = r, LocalizedContent = null };

        var world = new World();
        var ew = world.CreateEntity();
        world.Components<Position>().GetOrAdd(ew);
        ref var bw = ref world.Components<BitmapText>().GetOrAdd(ew);
        bw.Visible = true;
        bw.Content = "Hi";
        bw.IsLocalizationKey = false;
        bw.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        bw.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var pw = ref world.Components<Position>().Get(ew);
        pw.X = 10f;
        pw.Y = 20f;

        r.Sprites.Clear();
        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
        var nLit = r.Sprites.Count;
        Assert.True(nLit > 0);

        r.Sprites.Clear();
        bw.Visible = false;
        var es = world.CreateEntity();
        world.Components<Position>().GetOrAdd(es);
        ref var bs = ref world.Components<BitmapText>().GetOrAdd(es);
        bs.Visible = true;
        bs.Content = "Hi";
        bs.IsLocalizationKey = false;
        bs.CoordinateSpace = TextCoordinateSpace.ScreenPixels;
        bs.Style = bw.Style;
        ref var ps = ref world.Components<Position>().Get(es);
        ps.X = 10f;
        ps.Y = 20f;

        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
        Assert.Equal(nLit, r.Sprites.Count);
    }

    [Fact]
    public void TextRenderSystem_skips_when_localization_key_resolves_to_empty()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"empty":""}"""u8.ToArray());
        var lc = new LocalizedContent(loc, new VirtualFileSystem(), "en");
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = r, LocalizedContent = lc };
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "empty";
        bt.IsLocalizationKey = true;
        bt.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_decoration_branches_world_and_screen_literal_and_localized()
    {
        var r = new RecordingRenderer();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"k":"Z"}"""u8.ToArray());
        var lc = new LocalizedContent(loc, new VirtualFileSystem(), "en");
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = r, LocalizedContent = lc };
        var world = new World();

        var e0 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e0);
        ref var b0 = ref world.Components<BitmapText>().GetOrAdd(e0);
        b0.Visible = true;
        b0.Content = "Hi";
        b0.IsLocalizationKey = false;
        b0.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        b0.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);

        var e1 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e1);
        ref var b1 = ref world.Components<BitmapText>().GetOrAdd(e1);
        b1.Visible = true;
        b1.Content = "k";
        b1.IsLocalizationKey = true;
        b1.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        b1.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Strikethrough: true);

        var e2 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e2);
        ref var b2 = ref world.Components<BitmapText>().GetOrAdd(e2);
        b2.Visible = true;
        b2.Content = "Lo";
        b2.IsLocalizationKey = false;
        b2.CoordinateSpace = TextCoordinateSpace.ScreenPixels;
        b2.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Underline: true);
        ref var p2 = ref world.Components<Position>().Get(e2);
        p2.X = 4f;
        p2.Y = 20f;

        var e3 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e3);
        ref var b3 = ref world.Components<BitmapText>().GetOrAdd(e3);
        b3.Visible = true;
        b3.Content = "k";
        b3.IsLocalizationKey = true;
        b3.CoordinateSpace = TextCoordinateSpace.ScreenPixels;
        b3.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f), Strikethrough: true);
        ref var p3 = ref world.Components<Position>().Get(e3);
        p3.X = 40f;
        p3.Y = 50f;

        r.Sprites.Clear();
        new TextRenderSystem(host).OnLateUpdate(world, world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0.016f);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRenderSystem_second_frame_unchanged_uses_glyph_cache_replay()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "Cache";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var pos = ref world.Components<Position>().Get(e);
        pos.X = 1f;
        pos.Y = 2f;

        var q = world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>());
        sys.OnLateUpdate(world, q, 0.016f);
        var nFirst = r.Sprites.Count;
        Assert.True(nFirst > 0);
        r.Sprites.Clear();
        sys.OnLateUpdate(world, q, 0.016f);
        Assert.Equal(nFirst, r.Sprites.Count);
    }

    [Fact]
    public void TextRenderSystem_prunes_cache_when_entity_destroyed()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "X";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>());
        sys.OnLateUpdate(world, q, 0.016f);
        world.DestroyEntity(e);
        sys.OnLateUpdate(world, q, 0.016f);
    }

    [Fact]
    public void TextRenderSystem_prunes_when_renderer_becomes_null()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "Y";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>());
        sys.OnLateUpdate(world, q, 0.016f);
        host.Renderer = null;
        sys.OnLateUpdate(world, q, 0.016f);
    }

    [Fact]
    public void TextRenderSystem_reuses_cached_row_array_when_content_grows()
    {
        var r = new RecordingRenderer();
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = r, LocalizedContent = null };
        var sys = new TextRenderSystem(host);
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "A";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = TextCoordinateSpace.WorldBaseline;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var q = world.QueryChunks(SystemQuerySpec.All<BitmapText, Position>());
        sys.OnLateUpdate(world, q, 0.016f);
        bt.Content = "AB";
        r.Sprites.Clear();
        sys.OnLateUpdate(world, q, 0.016f);
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRowCacheStamp_object_equals_hashcode_and_inequality()
    {
        var stampType = typeof(TextRenderSystem).GetNestedType("TextRowCacheStamp", BindingFlags.NonPublic);
        Assert.NotNull(stampType);
        var style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        var a = Activator.CreateInstance(stampType!, "hi", style, TextCoordinateSpace.WorldBaseline, 400f, 1f, 2f, 0, 0);
        var b = Activator.CreateInstance(stampType!, "hi", style, TextCoordinateSpace.WorldBaseline, 400f, 1f, 2f, 0, 0);
        var c = Activator.CreateInstance(stampType!, "no", style, TextCoordinateSpace.WorldBaseline, 400f, 1f, 2f, 0, 0);
        var eqObj = stampType!.GetMethod("Equals", new[] { typeof(object) });
        Assert.NotNull(eqObj);
        Assert.True((bool)eqObj.Invoke(a, new object[] { b! })!);
        Assert.False((bool)eqObj.Invoke(a, new object[] { "x" })!);
        var hc = stampType.GetMethod("GetHashCode", Type.EmptyTypes);
        Assert.NotNull(hc);
        Assert.Equal((int)hc.Invoke(a, null)!, (int)hc.Invoke(b, null)!);
        var opNe = stampType.GetMethod("op_Inequality", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(opNe);
        Assert.True((bool)opNe.Invoke(null, new[] { a, c })!);
    }
}
