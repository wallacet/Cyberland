using System.Numerics;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Ecs;
using Cyberland.Engine.UI.Text;
using Moq;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class UiDocumentFrameSystemTests
{
    private static readonly SystemQuerySpec DocRootQuery = SystemQuerySpec.All<UiDocumentRoot>();

    [Fact]
    public void UiDocumentFrameSystem_exposes_QuerySpec()
    {
        var sys = new UiDocumentFrameSystem(new GameHostServices());
        Assert.Equal(SystemQuerySpec.All<UiDocumentRoot>(), sys.QuerySpec);
    }

    [Fact]
    public void UiDocumentFrameSystem_second_tick_redraws_after_clear_simulates_renderer_tick_reset()
    {
        var prev = UiLayoutGating.UseIncrementalDocumentFrames;
        UiLayoutGating.UseIncrementalDocumentFrames = true;
        try
        {
            var renderer = new RecordingRenderer();
            var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
            var doc = BuildHelloDocument();
            var world = new World();
            var ent = world.CreateEntity();
            world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
            {
                Visible = true,
                CoordinateSpace = CoordinateSpace.ViewportSpace,
                RootPreset = UiDocumentRootPreset.FullViewport,
                SortKeyBase = 1f
            };
            host.UiDocuments.Register(ent, doc);

            var sys = new UiDocumentFrameSystem(host);
            sys.OnStart(world, world.QueryChunks(DocRootQuery));
            sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
            var combined = renderer.TextGlyphs.Count + renderer.Sprites.Count;
            Assert.True(combined > 0);

            // Vulkan resets pending submits each tick; mimic so the second frame must resubmit the same HUD draws.
            renderer.Sprites.Clear();
            renderer.TextGlyphs.Clear();

            sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
            Assert.Equal(combined, renderer.TextGlyphs.Count + renderer.Sprites.Count);
        }
        finally
        {
            UiLayoutGating.UseIncrementalDocumentFrames = prev;
        }
    }

    [Fact]
    public void UiDocumentFrameSystem_layout_and_draw_submits_viewport_text_sprites()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices
        {
            Renderer = renderer,
            LocalizedContent = null
        };

        var doc = BuildHelloDocument();

        var world = new World();
        var ent = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 480f
        };
        host.UiDocuments.Register(ent, doc);

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);

        Assert.NotEmpty(renderer.Sprites);
    }

    [Fact]
    public void UiDocumentFrameSystem_skips_unregistered_invisible_and_unknown_preset()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        var world = new World();

        var emptyDoc = new UiDocument();

        var e0 = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(e0) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 1f
        };

        var e1 = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(e1) = new UiDocumentRoot
        {
            Visible = false,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 2f
        };
        host.UiDocuments.Register(e1, BuildHelloDocument());

        var e2 = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(e2) = new UiDocumentRoot
        {
            Visible = false,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 3f
        };
        host.UiDocuments.Register(e2, BuildHelloDocument());

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.Empty(renderer.Sprites);

        var eBad = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(eBad) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = (UiDocumentRootPreset)999,
            SortKeyBase = 0f
        };
        host.UiDocuments.Register(eBad, emptyDoc);

        Assert.Throws<ArgumentOutOfRangeException>(() => sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f));
    }

    [Fact]
    public void UiDocumentFrameSystem_Unregister_stops_submits_next_tick()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        var doc = BuildHelloDocument();
        var world = new World();
        var ent = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 0f
        };
        host.UiDocuments.Register(ent, doc);

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.NotEmpty(renderer.Sprites);

        renderer.Sprites.Clear();
        Assert.True(host.UiDocuments.Unregister(ent));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.Empty(renderer.Sprites);
    }

    [Fact]
    public void UiDocumentFrameSystem_primary_click_fires_top_stacked_button()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };

        var input = new Mock<IInputService>();
        input.Setup(i => i.GetMousePosition(CoordinateSpace.ViewportSpace)).Returns(new System.Numerics.Vector2(16f, 16f));
        input.SetupSequence(i => i.IsControlDown(It.IsAny<InputControl>()))
            .Returns(true)
            .Returns(false);
        input.Setup(i => i.MouseWheelDelta).Returns(System.Numerics.Vector2.Zero);
        host.Input = input.Object;

        var clicked = 0;
        var doc = new UiDocument();
        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        scroll.Content.AddChild(MakeSpacerRow(40f));

        var btn = new UiButton();
        UiLayoutPresets.TopLeftFixed(btn, 120f, 36f);
        btn.Clicked += (_, _) => clicked++;

        doc.Root.AddChild(btn);
        doc.Root.AddChild(scroll);

        var world = new World();
        var ent = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 0f
        };
        host.UiDocuments.Register(ent, doc);

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));

        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.Equal(0, clicked);

        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.Equal(1, clicked);
    }

    [Fact]
    public void UiDocumentFrameSystem_primary_click_hits_button_inside_scroll_content()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(400, 600) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };

        var clicked = 0;
        var doc = new UiDocument();
        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        var btn = new UiButton();
        UiLayoutPresets.TopLeftFixed(btn, 160f, 44f);
        btn.Clicked += (_, _) => clicked++;
        scroll.Content.AddChild(btn);
        doc.Root.AddChild(scroll);

        doc.MeasureArrange(new Vector2D<float>(400f, 600f));
        var center = btn.ComputedBounds.Center;

        var input = new Mock<IInputService>();
        input.Setup(i => i.GetMousePosition(CoordinateSpace.ViewportSpace))
            .Returns(new Vector2(center.X, center.Y));
        input.SetupSequence(i => i.IsControlDown(It.IsAny<InputControl>()))
            .Returns(true)
            .Returns(false);
        input.Setup(i => i.MouseWheelDelta).Returns(Vector2.Zero);
        host.Input = input.Object;

        var world = new World();
        var ent = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 0f
        };
        host.UiDocuments.Register(ent, doc);

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.Equal(0, clicked);
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.Equal(1, clicked);
    }

    [Fact]
    public void UiDocumentFrameSystem_primary_release_selects_radio_button()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(480, 160) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };

        var input = new StubUiInput
        {
            Viewport = new System.Numerics.Vector2(16f, 16f),
            LeftSequence = new[] { true, false },
            Wheel = System.Numerics.Vector2.Zero
        };
        host.Input = input;

        var group = new UiRadioGroup();
        var rb = new UiRadioButton(group, "a");
        UiLayoutPresets.TopLeftFixed(rb, 120f, 36f);

        var doc = new UiDocument();
        doc.Root.AddChild(rb);

        var world = new World();
        var ent = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 0f
        };
        host.UiDocuments.Register(ent, doc);

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f); // press
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f); // release

        Assert.Equal("a", group.SelectedOptionId);
    }

    [Fact]
    public void UiDocumentFrameSystem_pointer_press_on_empty_tree_covers_null_button_path()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(100, 100) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };

        var input = new StubUiInput
        {
            Viewport = new System.Numerics.Vector2(10f, 10f),
            LeftSequence = new[] { true },
            Wheel = System.Numerics.Vector2.Zero
        };
        host.Input = input;

        var doc = new UiDocument(); // no interactable children → HitTest returns null

        var world = new World();
        var ent = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 0f
        };
        host.UiDocuments.Register(ent, doc);

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
    }

    [Fact]
    public void UiCommandDrainSystem_invokes_dispatcher_per_command()
    {
        var host = new GameHostServices();
        object? last = null;
        host.UiCommandDispatcher = o => last = o;
        host.UiCommands.Enqueue("a");
        host.UiCommands.Enqueue(42);

        var drain = new UiCommandDrainSystem(host);
        var world = new World();
        drain.OnStart(world, world.QueryChunks(SystemQuerySpec.Empty));
        drain.OnLateUpdate(world.QueryChunks(SystemQuerySpec.Empty), 0f);

        Assert.Equal(42, last);
        Assert.Equal(0, host.UiCommands.Count);
    }

    private static UiDocument BuildHelloDocument()
    {
        var doc = new UiDocument();
        var tb = new UiTextBlock
        {
            Text = "Hello",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.TopLeftFixed(tb, 320f, 80f);
        doc.Root.AddChild(tb);
        return doc;
    }

    private static UiPanel MakeSpacerRow(float h)
    {
        var p = new UiPanel { BackgroundColor = new Vector4D<float>(1f, 0f, 0f, 0.2f) };
        UiLayoutPresets.TopLeftFixed(p, 400f, h);
        return p;
    }

    private sealed class StubUiInput : IInputService
    {
        private int _leftIdx;

        public InputBindings Bindings { get; } = new();
        public System.Numerics.Vector2 MousePosition => Viewport;
        public System.Numerics.Vector2 MouseDelta => System.Numerics.Vector2.Zero;
        public System.Numerics.Vector2 Viewport { get; set; }
        public System.Numerics.Vector2 Wheel { get; set; }
        public bool[] LeftSequence { get; set; } = Array.Empty<bool>();
        public System.Numerics.Vector2 MouseWheelDelta => Wheel;

        public System.Numerics.Vector2 GetMousePosition(CoordinateSpace space = CoordinateSpace.ViewportSpace) =>
            space == CoordinateSpace.ViewportSpace ? Viewport : throw new NotSupportedException();

        public System.Numerics.Vector2 GetMouseDelta(CoordinateSpace space = CoordinateSpace.ViewportSpace) =>
            space == CoordinateSpace.ViewportSpace ? System.Numerics.Vector2.Zero : throw new NotSupportedException();

        public void BeginFrame()
        {
        }

        public bool IsDown(string actionId) => false;
        public bool WasPressed(string actionId) => false;
        public bool WasReleased(string actionId) => false;
        public float ReadAxis(string axisId) => 0f;
        public bool ConsumePressed(string actionId) => false;
        public bool ConsumeReleased(string actionId) => false;
        public float ConsumeAxisDelta(string axisId) => 0f;

        public bool IsControlDown(InputControl control)
        {
            if (control.Kind != InputControlKind.MouseButton || control.MouseButton != MouseButton.Left)
                return false;

            if (_leftIdx >= LeftSequence.Length)
                return LeftSequence.Length > 0 && LeftSequence[^1];

            return LeftSequence[_leftIdx++];
        }

        public float ReadControlValue(InputControl control) => IsControlDown(control) ? 1f : 0f;
    }
}
