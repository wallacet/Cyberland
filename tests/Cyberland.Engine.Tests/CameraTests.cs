using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class CameraTests
{
    private static ParallelOptions ParOpts() => new ParallelismSettings().CreateParallelOptions();

    private static GameHostServices Host(IRenderer r) =>
        new GameHostServices() { Renderer = r };

    private static Transform MakeTransform(Vector2D<float>? worldPos = null, float worldRotation = 0f)
    {
        var t = Transform.Identity;
        if (worldPos is { } p) t.WorldPosition = p;
        if (worldRotation != 0f) t.WorldRotationRadians = worldRotation;
        return t;
    }

    [Fact]
    public void Camera2D_Create_uses_standard_defaults()
    {
        var c = Camera2D.Create(new Vector2D<int>(1280, 720));
        Assert.True(c.Enabled);
        Assert.Equal(0, c.Priority);
        Assert.Equal(new Vector2D<int>(1280, 720), c.ViewportSizeWorld);
        Assert.Equal(0.02f, c.BackgroundColor.X);
        Assert.Equal(0.06f, c.BackgroundColor.Z);
    }

    [Fact]
    public void CameraProjection_WorldToViewport_centers_on_camera_and_flips_Y()
    {
        // Camera at (100, 50), rotation 0, viewport 800x600 → world (100, 50) maps to viewport center (400, 300).
        var p = CameraProjection.WorldToViewportPixel(
            new Vector2D<float>(100f, 50f),
            new Vector2D<float>(100f, 50f),
            0f,
            new Vector2D<float>(800f, 600f));
        Assert.Equal(400f, p.X, 4);
        Assert.Equal(300f, p.Y, 4);

        // World point +Y up above camera → viewport Y SMALLER (toward top-left origin).
        var above = CameraProjection.WorldToViewportPixel(
            new Vector2D<float>(100f, 150f),
            new Vector2D<float>(100f, 50f),
            0f,
            new Vector2D<float>(800f, 600f));
        Assert.Equal(400f, above.X, 4);
        Assert.Equal(200f, above.Y, 4);
    }

    [Fact]
    public void CameraProjection_WorldToViewport_rotates_opposite_camera()
    {
        // Camera rotated +π/2 (CCW) means the world "spins" -π/2 in the camera frame: a world point at +X
        // appears at -Y (down) in +Y up viewport-relative coords, which maps to +Y down viewport pixel below
        // center. Viewport 200x100, viewport Y = H/2 - ry = 50 - (-10) = 60.
        var viewport = new Vector2D<float>(200f, 100f);
        var p = CameraProjection.WorldToViewportPixel(
            new Vector2D<float>(10f, 0f),
            new Vector2D<float>(0f, 0f),
            MathF.PI * 0.5f,
            viewport);
        Assert.Equal(100f, p.X, 3);
        Assert.Equal(60f, p.Y, 3);
    }

    [Fact]
    public void CameraProjection_ViewportToWorld_inverts_WorldToViewportPixel()
    {
        var vpSize = new Vector2D<float>(800f, 600f);
        var cam = new Vector2D<float>(150f, 220f);
        var rot = 0.37f;

        var world = new Vector2D<float>(12.5f, -7.25f);
        var vp = CameraProjection.WorldToViewportPixel(world, cam, rot, vpSize);
        var back = CameraProjection.ViewportPixelToWorld(vp, cam, rot, vpSize);
        Assert.Equal(world.X, back.X, 3);
        Assert.Equal(world.Y, back.Y, 3);
    }

    [Fact]
    public void CameraProjection_ComputePhysicalViewport_pillarboxes_wider_window()
    {
        // Virtual viewport 16:9 at 1280x720; window 1600x720 (wider) → pillarbox, not letterbox.
        var p = CameraProjection.ComputePhysicalViewport(
            new Vector2D<int>(1280, 720),
            new Vector2D<int>(1600, 720));
        Assert.Equal(1f, p.Scale);
        Assert.Equal(new Vector2D<int>(1280, 720), p.SizePixels);
        Assert.Equal(160, p.OffsetPixels.X); // (1600-1280)/2
        Assert.Equal(0, p.OffsetPixels.Y);
    }

    [Fact]
    public void CameraProjection_ComputePhysicalViewport_letterboxes_taller_window()
    {
        // Virtual 16:9; window 16:10 (taller) → letterbox on top and bottom.
        var p = CameraProjection.ComputePhysicalViewport(
            new Vector2D<int>(1280, 720),
            new Vector2D<int>(1280, 800));
        Assert.Equal(1f, p.Scale);
        Assert.Equal(new Vector2D<int>(1280, 720), p.SizePixels);
        Assert.Equal(0, p.OffsetPixels.X);
        Assert.Equal(40, p.OffsetPixels.Y);
    }

    [Fact]
    public void CameraProjection_ComputePhysicalViewport_scales_down_uniformly_when_needed()
    {
        // Virtual 1280x720 into 640x400 (smaller): scale = min(0.5, 0.555) = 0.5 → 640x360 centered vertically.
        var p = CameraProjection.ComputePhysicalViewport(
            new Vector2D<int>(1280, 720),
            new Vector2D<int>(640, 400));
        Assert.Equal(0.5f, p.Scale, 3);
        Assert.Equal(640, p.SizePixels.X);
        Assert.Equal(360, p.SizePixels.Y);
        Assert.Equal(0, p.OffsetPixels.X);
        Assert.Equal(20, p.OffsetPixels.Y);
    }

    [Fact]
    public void CameraProjection_ComputePhysicalViewport_zero_sizes_return_empty()
    {
        var zeroVp = CameraProjection.ComputePhysicalViewport(
            new Vector2D<int>(0, 100),
            new Vector2D<int>(1280, 720));
        Assert.Equal(0, zeroVp.SizePixels.X);
        Assert.Equal(0, zeroVp.SizePixels.Y);

        var zeroSc = CameraProjection.ComputePhysicalViewport(
            new Vector2D<int>(100, 100),
            new Vector2D<int>(0, 100));
        Assert.Equal(0, zeroSc.SizePixels.X);
        Assert.Equal(0, zeroSc.SizePixels.Y);
    }

    [Fact]
    public void CameraProjection_viewport_to_swapchain_roundtrip_honors_letterbox()
    {
        var physical = new PhysicalViewport(new Vector2D<int>(80, 0), new Vector2D<int>(640, 360), 0.5f);
        var sw = CameraProjection.ViewportPixelToSwapchainPixel(new Vector2D<float>(100f, 50f), in physical);
        Assert.Equal(80f + 100f * 0.5f, sw.X, 3);
        Assert.Equal(0f + 50f * 0.5f, sw.Y, 3);

        var back = CameraProjection.SwapchainPixelToViewportPixel(sw, in physical);
        Assert.Equal(100f, back.X, 3);
        Assert.Equal(50f, back.Y, 3);
    }

    [Fact]
    public void CameraProjection_swapchain_to_viewport_zero_scale_returns_input()
    {
        var empty = new PhysicalViewport(new Vector2D<int>(0, 0), new Vector2D<int>(0, 0), 0f);
        var back = CameraProjection.SwapchainPixelToViewportPixel(new Vector2D<float>(7f, 9f), in empty);
        Assert.Equal(7f, back.X);
        Assert.Equal(9f, back.Y);
    }

    [Fact]
    public void CameraSelection_Default_centers_on_swapchain()
    {
        var d = CameraSelection.Default(new Vector2D<int>(1280, 720));
        Assert.Equal(640f, d.PositionWorld.X);
        Assert.Equal(360f, d.PositionWorld.Y);
        Assert.Equal(new Vector2D<int>(1280, 720), d.ViewportSizeWorld);
        Assert.True(d.Enabled);

        // Zero swapchain clamps to 1x1 so math stays well-defined.
        var z = CameraSelection.Default(new Vector2D<int>(0, 0));
        Assert.Equal(new Vector2D<int>(1, 1), z.ViewportSizeWorld);
    }

    [Fact]
    public void CameraSelection_PickActive_no_cameras_returns_default()
    {
        var picked = CameraSelection.PickActive(
            ReadOnlySpan<CameraViewRequest>.Empty,
            new Vector2D<int>(800, 600));
        Assert.Equal(int.MinValue, picked.Priority);
        Assert.Equal(new Vector2D<int>(800, 600), picked.ViewportSizeWorld);
    }

    [Fact]
    public void CameraSelection_PickActive_ignores_disabled_and_zero_viewport()
    {
        Span<CameraViewRequest> cams = stackalloc CameraViewRequest[3];
        cams[0] = new CameraViewRequest { Enabled = false, Priority = 100, ViewportSizeWorld = new Vector2D<int>(1280, 720) };
        cams[1] = new CameraViewRequest { Enabled = true, Priority = 10, ViewportSizeWorld = new Vector2D<int>(0, 720) };
        cams[2] = new CameraViewRequest { Enabled = true, Priority = 5, ViewportSizeWorld = new Vector2D<int>(800, 0) };

        // All cameras invalid → fallback to Default.
        var picked = CameraSelection.PickActive(cams, new Vector2D<int>(1, 1));
        Assert.Equal(int.MinValue, picked.Priority);
    }

    [Fact]
    public void CameraSelection_PickActive_picks_highest_priority_and_ties_go_first()
    {
        Span<CameraViewRequest> cams = stackalloc CameraViewRequest[4];
        cams[0] = new CameraViewRequest { Enabled = true, Priority = 1, ViewportSizeWorld = new Vector2D<int>(10, 10), PositionWorld = new Vector2D<float>(1f, 0f) };
        cams[1] = new CameraViewRequest { Enabled = true, Priority = 7, ViewportSizeWorld = new Vector2D<int>(10, 10), PositionWorld = new Vector2D<float>(2f, 0f) };
        cams[2] = new CameraViewRequest { Enabled = true, Priority = 7, ViewportSizeWorld = new Vector2D<int>(10, 10), PositionWorld = new Vector2D<float>(3f, 0f) };
        cams[3] = new CameraViewRequest { Enabled = true, Priority = 3, ViewportSizeWorld = new Vector2D<int>(10, 10), PositionWorld = new Vector2D<float>(4f, 0f) };

        // Priority 7 wins; first-wins on ties, so (2,0) is selected over (3,0).
        var picked = CameraSelection.PickActive(cams, new Vector2D<int>(100, 100));
        Assert.Equal(7, picked.Priority);
        Assert.Equal(2f, picked.PositionWorld.X);
    }

    [Fact]
    public void CameraSubmitSystem_exposes_expected_query_spec()
    {
        var host = Host(new RecordingRenderer());
        Assert.Equal(SystemQuerySpec.All<Camera2D, Transform>(), new CameraSubmitSystem(host).QuerySpec);
    }

    [Fact]
    public void CameraSubmitSystem_OnStart_allows_null_renderer()
    {
        var h = new GameHostServices() { Renderer = null };
        var sys = new CameraSubmitSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<Camera2D, Transform>()));
    }

    [Fact]
    public void CameraSubmitSystem_skips_when_renderer_null_on_update()
    {
        var h = Host(new RecordingRenderer());
        var sys = new CameraSubmitSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<Camera2D, Transform>()));
        h.Renderer = null;
        sys.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Camera2D, Transform>()), 0f, ParOpts());
    }

    [Fact]
    public void CameraSubmitSystem_submits_enabled_and_skips_disabled()
    {
        var r = new RecordingRenderer();
        var sys = new CameraSubmitSystem(Host(r));
        var w = new World();
        var spec = SystemQuerySpec.All<Camera2D, Transform>();
        sys.OnStart(w, w.QueryChunks(spec));

        var enabled = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(enabled) = MakeTransform(
            worldPos: new Vector2D<float>(25f, 35f), worldRotation: 0.3f);
        w.Components<Camera2D>().GetOrAdd(enabled) = new Camera2D
        {
            Enabled = true,
            Priority = 5,
            ViewportSizeWorld = new Vector2D<int>(320, 180),
            BackgroundColor = new Vector4D<float>(0.1f, 0.2f, 0.3f, 1f)
        };

        var disabled = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(disabled) = Transform.Identity;
        w.Components<Camera2D>().GetOrAdd(disabled) = new Camera2D
        {
            Enabled = false,
            Priority = 99,
            ViewportSizeWorld = new Vector2D<int>(10, 10)
        };

        sys.OnParallelLateUpdate(w.QueryChunks(spec), 0f, ParOpts());

        Assert.Single(r.Cameras);
        Assert.Equal(25f, r.Cameras[0].PositionWorld.X, 3);
        Assert.Equal(35f, r.Cameras[0].PositionWorld.Y, 3);
        Assert.Equal(5, r.Cameras[0].Priority);
        Assert.Equal(new Vector2D<int>(320, 180), r.Cameras[0].ViewportSizeWorld);
        Assert.Equal(0.3f, r.Cameras[0].RotationRadians, 3);
        Assert.True(r.Cameras[0].Enabled);
    }

    [Fact]
    public void CameraSubmitSystem_reports_pose_after_hierarchy_solves_WorldMatrix()
    {
        // Regression guard for the "Pong camera showed only the bottom-left corner" bug: mods seed the
        // camera via Transform.WorldPosition; TransformHierarchySystem rebuilds WorldMatrix from LocalMatrix
        // each early update, so the Transform setter must back-propagate to LocalMatrix for the written
        // world pose to survive the hierarchy pass.
        var r = new RecordingRenderer();
        var w = new World();
        var hierarchy = new TransformHierarchySystem();
        var submit = new CameraSubmitSystem(Host(r));
        var hierarchySpec = SystemQuerySpec.All<Transform>();
        var cameraSpec = SystemQuerySpec.All<Camera2D, Transform>();
        hierarchy.OnStart(w, w.QueryChunks(hierarchySpec));
        submit.OnStart(w, w.QueryChunks(cameraSpec));

        var camera = w.CreateEntity();
        var tf = Transform.Identity;
        tf.WorldPosition = new Vector2D<float>(640f, 360f);
        w.Components<Transform>().GetOrAdd(camera) = tf;
        w.Components<Camera2D>().GetOrAdd(camera) = Camera2D.Create(new Vector2D<int>(1280, 720));

        // Early update resolves the camera's world matrix from its back-propagated local matrix; the camera
        // submit then decomposes the refreshed world matrix to get (640, 360) back.
        hierarchy.OnParallelEarlyUpdate(w.QueryChunks(hierarchySpec), 0f, ParOpts());
        submit.OnParallelLateUpdate(w.QueryChunks(cameraSpec), 0f, ParOpts());

        Assert.Single(r.Cameras);
        Assert.Equal(640f, r.Cameras[0].PositionWorld.X, 3);
        Assert.Equal(360f, r.Cameras[0].PositionWorld.Y, 3);
        Assert.Equal(new Vector2D<int>(1280, 720), r.Cameras[0].ViewportSizeWorld);
    }

    [Fact]
    public void ViewportAnchorSystem_returns_early_when_viewport_extent_is_zero()
    {
        var r = new RecordingRenderer { ActiveCameraViewportSize = new Vector2D<int>(0, 0) };
        var h = Host(r);
        var sys = new ViewportAnchorSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Transform>()));

        var e = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        w.Components<ViewportAnchor2D>().GetOrAdd(e) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ScreenSpace,
            Anchor = ViewportAnchorPreset.Center
        };

        sys.OnLateUpdate(w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Transform>()), 0f);
        // Transform stays at Identity because the early return fires before writing.
        Assert.Equal(0f, w.Components<Transform>().Get(e).WorldPosition.X);
        Assert.Equal(0f, w.Components<Transform>().Get(e).WorldPosition.Y);
    }

    [Fact]
    public void ViewportAnchorSystem_tracks_camera_viewport_independent_of_swapchain()
    {
        // The same mod authoring should produce the same anchor math whether the window is 800x600 or 1920x1080
        // as long as the active camera viewport stays (800, 600).
        var r = new RecordingRenderer
        {
            SwapchainPixelSize = new Vector2D<int>(1920, 1080),
            ActiveCameraViewportSize = new Vector2D<int>(800, 600)
        };
        var h = Host(r);
        var sys = new ViewportAnchorSystem(h);
        var w = new World();
        var spec = SystemQuerySpec.All<ViewportAnchor2D, Transform>();
        sys.OnStart(w, w.QueryChunks(spec));

        var e = w.CreateEntity();
        w.Components<ViewportAnchor2D>().GetOrAdd(e) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ScreenSpace,
            Anchor = ViewportAnchorPreset.TopRight,
            OffsetX = 10f,
            OffsetY = 20f
        };
        w.Components<Transform>().GetOrAdd(e) = Transform.Identity;

        sys.OnLateUpdate(w.QueryChunks(spec), 0f);
        ref var t = ref w.Components<Transform>().Get(e);
        Assert.Equal(800f - 10f, t.WorldPosition.X);
        Assert.Equal(20f, t.WorldPosition.Y);
    }
}
