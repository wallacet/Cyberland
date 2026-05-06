using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class UiScrollViewRoutingTests
{
    private static readonly SystemQuerySpec DocRootQuery = SystemQuerySpec.All<UiDocumentRoot>();

    [Fact]
    public void UiDocument_HitTest_reaches_UiButton_inside_UiScrollView_content()
    {
        var doc = new UiDocument();
        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        var btn = new UiButton();
        UiLayoutPresets.TopLeftFixed(btn, 160f, 44f);
        scroll.Content.AddChild(btn);
        doc.Root.AddChild(scroll);

        doc.MeasureArrange(new Vector2D<float>(400f, 600f));
        var c = btn.ComputedBounds.Center;
        var hit = doc.HitTest(c, new UiRect(0f, 0f, 400f, 600f));
        Assert.NotNull(hit);
        Assert.Same(btn, hit);
    }

    [Fact]
    public void ApplyWheel_increments_ContentOffset()
    {
        var sv = new UiScrollView();
        sv.ApplyWheel(-2f);
        Assert.True(sv.ContentOffset.Y > 0f);
    }

    [Fact]
    public void Wheel_measure_apply_measure_keeps_ContentOffset()
    {
        var scroll = BuildTallScroll();
        var doc = new UiDocument();
        doc.Root.AddChild(scroll);

        doc.MeasureArrange(new Vector2D<float>(480f, 160f));
        var clip = new UiRect(0f, 0f, 480f, 160f);
        var sv = UiDocumentFrameSystem.FindDeepestScrollView(doc.Root, new Vector2D<float>(40f, 80f), clip);
        Assert.NotNull(sv);
        sv!.ApplyWheel(-2f);
        doc.MeasureArrange(new Vector2D<float>(480f, 160f));
        Assert.True(scroll.ContentOffset.Y > 0f);
    }

    [Fact]
    public void UiDocumentFrameSystem_applies_wheel_via_mock_input()
    {
        var renderer = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(480, 160) };
        var host = new GameHostServices { Renderer = renderer, LocalizedContent = null };

        var input = new StubUiInput { Wheel = new System.Numerics.Vector2(0f, -2f), Viewport = new System.Numerics.Vector2(40f, 80f) };
        host.Input = input;

        var scroll = BuildTallScroll();
        var doc = new UiDocument();
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

        Assert.True(scroll.ContentOffset.Y > 0f);
    }

    [Fact]
    public void FindDeepestScrollView_locates_viewport_scroll_under_pointer()
    {
        var scroll = BuildTallScroll();

        var doc = new UiDocument();
        doc.Root.AddChild(scroll);

        doc.MeasureArrange(new Vector2D<float>(480f, 160f));

        var clip = new UiRect(0f, 0f, 480f, 160f);
        var found = UiDocumentFrameSystem.FindDeepestScrollView(doc.Root, new Vector2D<float>(40f, 80f), clip);
        Assert.Same(scroll, found);
    }

    [Fact]
    public void FindDeepestScrollView_returns_null_for_invisible_outside_or_clipped()
    {
        var scroll = BuildTallScroll();
        var doc = new UiDocument();
        doc.Root.AddChild(scroll);
        doc.MeasureArrange(new Vector2D<float>(480f, 160f));

        var clip = new UiRect(0f, 0f, 480f, 160f);

        scroll.Visible = false;
        Assert.Null(UiDocumentFrameSystem.FindDeepestScrollView(doc.Root, new Vector2D<float>(40f, 80f), clip));
        scroll.Visible = true;

        Assert.Null(UiDocumentFrameSystem.FindDeepestScrollView(doc.Root, new Vector2D<float>(999f, 999f), clip));
        Assert.Null(UiDocumentFrameSystem.FindDeepestScrollView(doc.Root, new Vector2D<float>(40f, 80f), new UiRect(0f, 0f, 0f, 0f)));
    }

    [Fact]
    public void UiScrollView_Arrange_when_invisible_early_returns()
    {
        var scroll = BuildTallScroll();
        scroll.Visible = false;
        scroll.Measure(UiSizeConstraints.Loose(480f, 160f));
        scroll.Arrange(new UiRect(0f, 0f, 480f, 160f));
    }

    private static UiScrollView BuildTallScroll()
    {
        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        var inner = new UiPanel();
        for (var i = 0; i < 12; i++)
            inner.AddChild(MakeRow(40f));
        scroll.Content.AddChild(inner);
        return scroll;
    }

    private static UiPanel MakeRow(float h)
    {
        var p = new UiPanel();
        UiLayoutPresets.TopLeftFixed(p, 400f, h);
        return p;
    }

    private sealed class StubUiInput : IInputService
    {
        public InputBindings Bindings { get; } = new();
        public System.Numerics.Vector2 MousePosition => Viewport;
        public System.Numerics.Vector2 MouseDelta => System.Numerics.Vector2.Zero;
        public System.Numerics.Vector2 Viewport { get; set; }
        public System.Numerics.Vector2 Wheel { get; set; }
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
        public bool IsControlDown(InputControl control) => false;
        public float ReadControlValue(InputControl control) => 0f;
    }
}
