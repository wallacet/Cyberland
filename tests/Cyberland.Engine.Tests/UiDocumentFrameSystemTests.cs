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
        var rb = new UiRadioButton(group, "a", 120f, 36f);
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
    public void UiDocumentFrameSystem_pointer_routing_skips_presentation_documents_when_presentation_pointer_unavailable()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(400, 300) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = default;
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(50f, 50f),
            LeftSequence = new[] { false },
            Wheel = new Vector2(0f, -1f)
        };

        var doc = new UiDocument();
        doc.Root.AddChild(new UiPanel());

        var world = new World();
        var ent = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.PresentationViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 0f
        };
        host.UiDocuments.Register(ent, doc);

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
    }

    [Fact]
    public void UiDocumentFrameSystem_resolves_presentation_pointer_when_hud_layout_is_valid()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(400, 300) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(new Vector2D<int>(400, 300));
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(50f, 50f),
            LeftSequence = new[] { false },
            Wheel = new Vector2(0f, -1f)
        };

        var doc = new UiDocument();
        doc.Root.AddChild(new UiPanel());

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
    public void UiDocumentFrameSystem_release_outside_viewport_document_cancels_armed_button()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(100, 100) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(10f, 10f),
            LeftSequence = new[] { true, false },
            Wheel = Vector2.Zero
        };

        var clicked = 0;
        var doc = new UiDocument();
        var btn = new UiButton();
        UiLayoutPresets.TopLeftFixed(btn, 40f, 20f);
        btn.Clicked += (_, _) => clicked++;
        doc.Root.AddChild(btn);

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
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f); // arm on button

        // Move pointer outside root; release should not click and should clear armed path.
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(999f, 999f),
            LeftSequence = new[] { false },
            Wheel = Vector2.Zero
        };
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.Equal(0, clicked);
    }

    [Fact]
    public void UiDocumentFrameSystem_wheel_with_no_scroll_target_keeps_layout_stable()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(200, 100) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(20f, 20f),
            LeftSequence = new[] { false },
            Wheel = new Vector2(0f, -1f)
        };

        var measured = new CountingElement();
        UiLayoutPresets.TopLeftFixed(measured, 60f, 20f);
        var doc = new UiDocument();
        doc.Root.AddChild(measured);

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

        var prev = UiLayoutGating.UseIncrementalDocumentFrames;
        UiLayoutGating.UseIncrementalDocumentFrames = true;
        try
        {
            var sys = new UiDocumentFrameSystem(host);
            sys.OnStart(world, world.QueryChunks(DocRootQuery));
            sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
            var measuredAfterFirst = measured.MeasureCount;
            sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
            Assert.Equal(measuredAfterFirst, measured.MeasureCount);
        }
        finally
        {
            UiLayoutGating.UseIncrementalDocumentFrames = prev;
        }
    }

    [Fact]
    public void UiDocumentFrameSystem_pointer_outside_zero_viewport_root_is_ignored()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(0, 0) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(1f, 1f),
            LeftSequence = new[] { true, false },
            Wheel = Vector2.Zero
        };

        var clicked = 0;
        var doc = new UiDocument();
        var btn = new UiButton();
        UiLayoutPresets.TopLeftFixed(btn, 40f, 20f);
        btn.Clicked += (_, _) => clicked++;
        doc.Root.AddChild(btn);

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
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        Assert.Equal(0, clicked);
    }

    [Fact]
    public void UiCommandDrainSystem_invokes_dispatcher_per_command()
    {
        var host = new GameHostServices();
        IUiCommand? last = null;
        host.UiCommandDispatcher = o => last = o;
        host.UiCommands.Enqueue(new TestUiCommand(7));
        host.UiCommands.Enqueue(new TestUiCommand(42));

        var drain = new UiCommandDrainSystem(host);
        var world = new World();
        drain.OnStart(world, world.QueryChunks(SystemQuerySpec.Empty));
        drain.OnLateUpdate(world.QueryChunks(SystemQuerySpec.Empty), 0f);

        Assert.Equal(42, Assert.IsType<TestUiCommand>(last).Value);
        Assert.Equal(0, host.UiCommands.Count);
    }

    [Fact]
    public void UiCommandDrainSystem_keeps_commands_queued_until_dispatcher_is_installed()
    {
        var host = new GameHostServices();
        host.UiCommands.Enqueue(new TestUiCommand(99));

        var drain = new UiCommandDrainSystem(host);
        var world = new World();
        drain.OnStart(world, world.QueryChunks(SystemQuerySpec.Empty));

        drain.OnLateUpdate(world.QueryChunks(SystemQuerySpec.Empty), 0f);
        Assert.Equal(1, host.UiCommands.Count);

        IUiCommand? delivered = null;
        host.UiCommandDispatcher = cmd => delivered = cmd;
        drain.OnLateUpdate(world.QueryChunks(SystemQuerySpec.Empty), 0f);

        Assert.Equal(99, Assert.IsType<TestUiCommand>(delivered).Value);
        Assert.Equal(0, host.UiCommands.Count);
    }

    [Fact]
    public void UiCommandDrainSystem_caps_backlog_when_dispatcher_is_missing()
    {
        var host = new GameHostServices();
        for (var i = 0; i < 5000; i++)
            host.UiCommands.Enqueue(new TestUiCommand(i));

        var drain = new UiCommandDrainSystem(host);
        var world = new World();
        drain.OnStart(world, world.QueryChunks(SystemQuerySpec.Empty));
        drain.OnLateUpdate(world.QueryChunks(SystemQuerySpec.Empty), 0f);

        Assert.Equal(4096, host.UiCommands.Count);
        Assert.True(host.UiCommands.TryPeek(out var firstRemaining));
        Assert.Equal(904, Assert.IsType<TestUiCommand>(firstRemaining).Value);
    }

    [Fact]
    public void UiCommandDrainSystem_enforces_per_frame_dispatch_budget()
    {
        var host = new GameHostServices();
        var dispatched = 0;
        host.UiCommandDispatcher = _ => dispatched++;
        for (var i = 0; i < 600; i++)
            host.UiCommands.Enqueue(new TestUiCommand(i));

        var drain = new UiCommandDrainSystem(host);
        var world = new World();
        drain.OnStart(world, world.QueryChunks(SystemQuerySpec.Empty));
        drain.OnLateUpdate(world.QueryChunks(SystemQuerySpec.Empty), 0f);

        Assert.Equal(512, dispatched);
        Assert.Equal(88, host.UiCommands.Count);
    }

    [Fact]
    public void UiCommandDrainSystem_skips_null_command_without_throwing()
    {
        var queue = new UiCommandQueueWithNullableSeed(null, new TestUiCommand(99));
        var host = new GameHostServices(queue);
        IUiCommand? last = null;
        host.UiCommandDispatcher = c => last = c;

        var drain = new UiCommandDrainSystem(host);
        var world = new World();
        drain.OnStart(world, world.QueryChunks(SystemQuerySpec.Empty));
        drain.OnLateUpdate(world.QueryChunks(SystemQuerySpec.Empty), 0f);

        Assert.Equal(99, Assert.IsType<TestUiCommand>(last).Value);
        Assert.Equal(0, host.UiCommands.Count);
    }

    [Fact]
    public void UiDocumentFrameSystem_wheel_prefers_higher_sort_key_document()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(480, 200) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(40f, 80f),
            LeftSequence = new[] { false },
            Wheel = new Vector2(0f, -2f)
        };

        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(a) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 0f
        };
        world.GetOrAdd<UiDocumentRoot>(b) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 10f
        };

        var scrollA = BuildTallScroll();
        var scrollB = BuildTallScroll();
        host.UiDocuments.Register(a, WrapInDocument(scrollA));
        host.UiDocuments.Register(b, WrapInDocument(scrollB));

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);

        Assert.Equal(0f, scrollA.VerticalOffset);
        Assert.True(scrollB.VerticalOffset > 0f);
    }

    [Fact]
    public void UiDocumentFrameSystem_wheel_routes_only_to_topmost_viewport_document()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(480, 200) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(40f, 80f),
            LeftSequence = new[] { false },
            Wheel = new Vector2(0f, -2f)
        };

        var world = new World();
        var back = world.CreateEntity();
        var front = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(back) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 0f
        };
        world.GetOrAdd<UiDocumentRoot>(front) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 10f
        };

        var scrollBack = BuildTallScroll();
        var scrollFront = BuildTallScroll();
        host.UiDocuments.Register(back, WrapInDocument(scrollBack));
        host.UiDocuments.Register(front, WrapInDocument(scrollFront));

        var sys = new UiDocumentFrameSystem(host);
        sys.OnStart(world, world.QueryChunks(DocRootQuery));
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);

        Assert.Equal(0f, scrollBack.VerticalOffset);
        Assert.True(scrollFront.VerticalOffset > 0f);
    }

    [Fact]
    public void UiDocumentFrameSystem_world_space_documents_ignore_pointer_and_wheel_routing()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(400, 200) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
        host.Input = new StubUiInput
        {
            Viewport = new Vector2(20f, 20f),
            LeftSequence = new[] { true, false },
            Wheel = new Vector2(0f, -2f)
        };

        var clicked = 0;
        var doc = new UiDocument();
        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        var btn = new UiButton();
        UiLayoutPresets.TopLeftFixed(btn, 120f, 36f);
        btn.Clicked += (_, _) => clicked++;
        scroll.Content.AddChild(btn);
        doc.Root.AddChild(scroll);

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
        sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);

        Assert.Equal(0, clicked);
        Assert.Equal(0f, scroll.VerticalOffset);
    }

    [Fact]
    public void UiDocumentFrameSystem_incremental_gating_skips_second_measure_when_root_and_layout_are_stable()
    {
        var prev = UiLayoutGating.UseIncrementalDocumentFrames;
        UiLayoutGating.UseIncrementalDocumentFrames = true;
        try
        {
            var renderer = new RecordingRenderer();
            var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
            var world = new World();
            var ent = world.CreateEntity();
            world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
            {
                Visible = true,
                CoordinateSpace = CoordinateSpace.ViewportSpace,
                RootPreset = UiDocumentRootPreset.FullViewport,
                SortKeyBase = 0f
            };

            var doc = new UiDocument();
            var measured = new CountingElement();
            UiLayoutPresets.TopLeftFixed(measured, 100f, 30f);
            doc.Root.AddChild(measured);
            host.UiDocuments.Register(ent, doc);

            var sys = new UiDocumentFrameSystem(host);
            sys.OnStart(world, world.QueryChunks(DocRootQuery));
            sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
            var countAfterFirstTick = measured.MeasureCount;
            Assert.True(countAfterFirstTick > 0);

            sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
            Assert.Equal(countAfterFirstTick, measured.MeasureCount);
        }
        finally
        {
            UiLayoutGating.UseIncrementalDocumentFrames = prev;
        }
    }

    [Fact]
    public void UiDocumentFrameSystem_warns_once_when_measure_arrange_budget_is_exceeded()
    {
        var prevInc = UiLayoutGating.UseIncrementalDocumentFrames;
        UiLayoutGating.UseIncrementalDocumentFrames = false;
        try
        {
            var renderer = new RecordingRenderer();
            var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };
            var world = new World();
            for (var i = 0; i < 129; i++)
            {
                var ent = world.CreateEntity();
                world.GetOrAdd<UiDocumentRoot>(ent) = new UiDocumentRoot
                {
                    Visible = true,
                    CoordinateSpace = CoordinateSpace.ViewportSpace,
                    RootPreset = UiDocumentRootPreset.FullViewport,
                    SortKeyBase = i
                };
                host.UiDocuments.Register(ent, new UiDocument());
            }

            var sys = new UiDocumentFrameSystem(host);
            sys.OnStart(world, world.QueryChunks(DocRootQuery));
            sys.OnLateUpdate(world.QueryChunks(DocRootQuery), 0.016f);
        }
        finally
        {
            UiLayoutGating.UseIncrementalDocumentFrames = prevInc;
        }
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

    private static UiDocument WrapInDocument(UiScrollView scroll)
    {
        var doc = new UiDocument();
        doc.Root.AddChild(scroll);
        return doc;
    }

    private static UiScrollView BuildTallScroll()
    {
        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        var body = new UiPanel();
        for (var i = 0; i < 10; i++)
            body.AddChild(MakeSpacerRow(40f));
        scroll.Content.AddChild(body);
        return scroll;
    }

    private sealed class StubUiInput : IInputService
    {
        private int _leftIdx;

        public InputBindings Bindings { get; } = new();
        public System.Numerics.Vector2 MousePosition => Viewport;
        public System.Numerics.Vector2 MousePositionScreen => Viewport;
        public System.Numerics.Vector2 MousePositionWorld => Viewport;
        public System.Numerics.Vector2 MouseDelta => System.Numerics.Vector2.Zero;
        public System.Numerics.Vector2 Viewport { get; set; }
        public System.Numerics.Vector2 Wheel { get; set; }
        public bool[] LeftSequence { get; set; } = Array.Empty<bool>();
        public System.Numerics.Vector2 MouseWheelDelta => Wheel;

        public IReadOnlyList<InputGameplayCommand> FrameGameplayCommands => Array.Empty<InputGameplayCommand>();

        public System.Numerics.Vector2 GetMousePosition(CoordinateSpace space = CoordinateSpace.ViewportSpace) =>
            space is CoordinateSpace.ViewportSpace or CoordinateSpace.PresentationViewportSpace
                ? Viewport
                : throw new NotSupportedException();

        public System.Numerics.Vector2 GetMouseDelta(CoordinateSpace space = CoordinateSpace.ViewportSpace) =>
            space is CoordinateSpace.ViewportSpace or CoordinateSpace.PresentationViewportSpace
                ? System.Numerics.Vector2.Zero
                : throw new NotSupportedException();

        public void BeginFrame()
        {
        }

        public bool IsDown(string actionId) => false;
        public bool WasPressed(string actionId) => false;
        public bool WasReleased(string actionId) => false;
        public bool MouseButton(MouseButton button) => button == Silk.NET.Input.MouseButton.Left && IsControlDown(InputControl.MouseButtonControl(Silk.NET.Input.MouseButton.Left));
        public bool MouseButtonDown(MouseButton button) => false;
        public bool MouseButtonUp(MouseButton button) => false;
        public bool ConsumeMouseButtonPressed(MouseButton button) => false;
        public bool ConsumeMouseButtonReleased(MouseButton button) => false;
        public float ReadAxis(string axisId) => 0f;
        public bool ConsumePressed(string actionId) => false;
        public bool ConsumeReleased(string actionId) => false;
        public float ConsumeAxisDelta(string axisId) => 0f;

        public bool IsControlDown(InputControl control)
        {
            if (control.Kind != InputControlKind.MouseButton || control.MouseButton != Silk.NET.Input.MouseButton.Left)
                return false;

            if (_leftIdx >= LeftSequence.Length)
                return LeftSequence.Length > 0 && LeftSequence[^1];

            return LeftSequence[_leftIdx++];
        }

        public float ReadControlValue(InputControl control) => IsControlDown(control) ? 1f : 0f;
    }

    private sealed class CountingElement : UiElement
    {
        public int MeasureCount { get; private set; }

        protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
        {
            MeasureCount++;
            return base.MeasureCore(in constraints);
        }
    }

    /// <summary>Test double that can dequeue null head entries (stock <see cref="UiCommandQueue"/> forbids null enqueue).</summary>
    private sealed class UiCommandQueueWithNullableSeed : IUiCommandQueue
    {
        private readonly Queue<IUiCommand?> _queue = new();

        public UiCommandQueueWithNullableSeed(params IUiCommand?[] seed)
        {
            foreach (var item in seed)
                _queue.Enqueue(item);
        }

        public int Count => _queue.Count;

        public void Enqueue(IUiCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            _queue.Enqueue(command);
        }

        public bool TryPeek(out IUiCommand? command)
        {
            if (_queue.Count == 0)
            {
                command = null;
                return false;
            }

            command = _queue.Peek();
            return true;
        }

        public bool TryDequeue(out IUiCommand? command) => _queue.TryDequeue(out command);

        public int TrimToMaxCount(int maxCount)
        {
            if (maxCount < 0)
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "maxCount must be non-negative.");

            var removed = 0;
            while (_queue.Count > maxCount && TryDequeue(out _))
                removed++;
            return removed;
        }
    }

    private sealed record TestUiCommand(int Value) : IUiCommand;
}
