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
        bt.BaselineWorldSpace = true;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        new TextRenderSystem(host).OnLateUpdate(world, 0.016f);
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
        b0.BaselineWorldSpace = true;
        b0.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e1 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e1);
        ref var b1 = ref world.Components<BitmapText>().GetOrAdd(e1);
        b1.Visible = true;
        b1.Content = "";
        b1.IsLocalizationKey = false;
        b1.BaselineWorldSpace = true;
        b1.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e2 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e2);
        ref var b2 = ref world.Components<BitmapText>().GetOrAdd(e2);
        b2.Visible = true;
        b2.Content = "ok";
        b2.IsLocalizationKey = true;
        b2.BaselineWorldSpace = true;
        b2.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));

        var e3 = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e3);
        ref var b3 = ref world.Components<BitmapText>().GetOrAdd(e3);
        b3.Visible = true;
        b3.Content = "ok";
        b3.IsLocalizationKey = true;
        b3.BaselineWorldSpace = false;
        b3.Style = b2.Style;

        new TextRenderSystem(host).OnLateUpdate(world, 0.016f);
        Assert.Empty(r.Sprites);

        host.LocalizedContent = lc;
        new TextRenderSystem(host).OnLateUpdate(world, 0.016f);
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
        btw.BaselineWorldSpace = true;
        btw.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var pw = ref world.Components<Position>().Get(ew);
        pw.X = 40f;
        pw.Y = 50f;

        r.Sprites.Clear();
        new TextRenderSystem(host).OnLateUpdate(world, 0.016f);
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
        bts.BaselineWorldSpace = false;
        bts.Style = btw.Style;
        ref var ps = ref world.Components<Position>().Get(es);
        ps.X = 40f;
        ps.Y = r.SwapchainPixelSize.Y - 50f;

        new TextRenderSystem(host).OnLateUpdate(world, 0.016f);
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
        bw.BaselineWorldSpace = true;
        bw.Style = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        ref var pw = ref world.Components<Position>().Get(ew);
        pw.X = 10f;
        pw.Y = 20f;

        r.Sprites.Clear();
        new TextRenderSystem(host).OnLateUpdate(world, 0.016f);
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
        bs.BaselineWorldSpace = false;
        bs.Style = bw.Style;
        ref var ps = ref world.Components<Position>().Get(es);
        ps.X = 10f;
        ps.Y = 20f;

        new TextRenderSystem(host).OnLateUpdate(world, 0.016f);
        Assert.Equal(nLit, r.Sprites.Count);
    }
}
