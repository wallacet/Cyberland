using System.Reflection;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

/// <summary>Coverage for light submit/merge paths (via <see cref="Scene.Systems"/>), viewport anchors, and text staging.</summary>
[Collection("EngineDiagnostics")]
public sealed class DeferredSubmissionAndSceneSystemsTests
{
    private static ParallelOptions ParOpts() => new ParallelismSettings().CreateParallelOptions();

    private static GameHostServices Host(IRenderer r) => new GameHostServices() { Renderer = r };

    // Seeded-from-Identity helper: property setters on a default Transform collapse scale to (0,0) because the zero
    // matrix decomposes to zero PRS. Tests must seed from Identity before assigning PRS properties.
    private static Transform MakeTransform(
        Vector2D<float>? localPos = null,
        Vector2D<float>? worldPos = null,
        float localRotation = 0f,
        float worldRotation = 0f,
        Vector2D<float>? localScale = null,
        Vector2D<float>? worldScale = null)
    {
        var t = Transform.Identity;
        if (localPos is { } lp) t.LocalPosition = lp;
        if (worldPos is { } wp) t.WorldPosition = wp;
        if (localRotation != 0f) t.LocalRotationRadians = localRotation;
        if (worldRotation != 0f) t.WorldRotationRadians = worldRotation;
        if (localScale is { } ls) t.LocalScale = ls;
        if (worldScale is { } ws) t.WorldScale = ws;
        return t;
    }

    [Fact]
    public void Engine_submit_systems_expose_expected_query_specs()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        Assert.Equal(SystemQuerySpec.All<AmbientLightSource>(), new AmbientLightSystem(h).QuerySpec);
        Assert.Equal(SystemQuerySpec.All<DirectionalLightSource, Transform>(), new DirectionalLightSystem(h).QuerySpec);
        Assert.Equal(SystemQuerySpec.All<SpotLightSource, Transform>(), new SpotLightSystem(h).QuerySpec);
        Assert.Equal(SystemQuerySpec.All<PointLightSource, Transform>(), new PointLightSystem(h).QuerySpec);
        Assert.Equal(SystemQuerySpec.All<PostProcessVolumeSource, Transform>(), new PostProcessVolumeSystem(h).QuerySpec);
        Assert.Equal(SystemQuerySpec.All<ViewportAnchor2D, Transform>(), new ViewportAnchorSystem(h).QuerySpec);
        Assert.Equal(SystemQuerySpec.All<BitmapText, Transform>(), new TextStagingSystem(h).QuerySpec);
        var textRow = SystemQuerySpec.All<BitmapText, Transform, TextBuildFingerprint, TextSpriteCache>();
        Assert.Equal(textRow, new TextBuildSystem(h).QuerySpec);
        Assert.Equal(textRow, new TextRenderSystem(h).QuerySpec);
    }

    [Fact]
    public void PointLightSystem_OnStart_allows_null_renderer()
    {
        var h = new GameHostServices() { Renderer = null };
        var sys = new PointLightSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<PointLightSource, Transform>()));
    }

    [Fact]
    public void AmbientLightSystem_OnStart_allows_null_renderer()
    {
        var h = new GameHostServices() { Renderer = null };
        var sys = new AmbientLightSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()));
    }

    [Fact]
    public void DirectionalLightSystem_OnStart_allows_null_renderer()
    {
        var h = new GameHostServices() { Renderer = null };
        var sys = new DirectionalLightSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<DirectionalLightSource, Transform>()));
    }

    [Fact]
    public void SpotLightSystem_OnStart_allows_null_renderer()
    {
        var h = new GameHostServices() { Renderer = null };
        var sys = new SpotLightSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<SpotLightSource, Transform>()));
    }

    [Fact]
    public void PostProcessVolumeSystem_OnStart_allows_null_renderer()
    {
        var h = new GameHostServices() { Renderer = null };
        var sys = new PostProcessVolumeSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<PostProcessVolumeSource, Transform>()));
    }

    [Fact]
    public void ViewportAnchorSystem_OnStart_allows_null_renderer()
    {
        var h = new GameHostServices() { Renderer = null };
        var sys = new ViewportAnchorSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Transform>()));
    }

    [Fact]
    public void PointLightSystem_skips_points_when_no_sources_in_query()
    {
        var r = new RecordingRenderer();
        var pl = new PointLightSystem(Host(r));
        var w = new World();
        pl.OnStart(w, w.QueryChunks(SystemQuerySpec.All<PointLightSource, Transform>()));
        pl.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<PointLightSource, Transform>()), 0f, ParOpts());
        Assert.Empty(r.PointLights);
    }

    [Fact]
    public void AmbientLightSystem_submits_active_and_skips_inactive()
    {
        var r = new RecordingRenderer();
        var ambSys = new AmbientLightSystem(Host(r));
        var w = new World();
        ambSys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()));

        var ambOff = w.CreateEntity();
        w.GetOrAdd<AmbientLightSource>(ambOff) = new AmbientLightSource
        {
            Active = false,
            Color = new Vector3D<float>(0.2f, 0f, 0f),
            Intensity = 99f
        };
        var ambOn = w.CreateEntity();
        w.GetOrAdd<AmbientLightSource>(ambOn) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0f, 0.8f, 0f),
            Intensity = 1f
        };

        var dirOff = w.CreateEntity();
        w.GetOrAdd<Transform>(dirOff) = Transform.Identity;
        w.GetOrAdd<DirectionalLightSource>(dirOff) = new DirectionalLightSource { Active = false };
        var dirOn = w.CreateEntity();
        w.GetOrAdd<Transform>(dirOn) = MakeTransform(localRotation: MathF.PI * 0.5f, worldRotation: MathF.PI * 0.5f);
        w.GetOrAdd<DirectionalLightSource>(dirOn) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.5f, 0.5f, 0.5f),
            Intensity = 1f
        };

        var spotOff = w.CreateEntity();
        w.GetOrAdd<Transform>(spotOff) = Transform.Identity;
        w.GetOrAdd<SpotLightSource>(spotOff) = new SpotLightSource { Active = false };
        var spotOn = w.CreateEntity();
        w.GetOrAdd<Transform>(spotOn) = MakeTransform(
            localPos: new Vector2D<float>(1f, 2f),
            worldPos: new Vector2D<float>(1f, 2f),
            localRotation: -MathF.PI * 0.5f,
            worldRotation: -MathF.PI * 0.5f);
        w.GetOrAdd<SpotLightSource>(spotOn) = new SpotLightSource
        {
            Active = true,
            Radius = 50f,
            InnerConeRadians = 0.4f,
            OuterConeRadians = 0.9f,
            Color = new Vector3D<float>(1f, 1f, 0f),
            Intensity = 1f
        };

        var dirSys = new DirectionalLightSystem(Host(r));
        dirSys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<DirectionalLightSource, Transform>()));
        var spotSys = new SpotLightSystem(Host(r));
        spotSys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<SpotLightSource, Transform>()));
        ambSys.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()), 0f, ParOpts());
        dirSys.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<DirectionalLightSource, Transform>()), 0f, ParOpts());
        spotSys.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<SpotLightSource, Transform>()), 0f, ParOpts());
        Assert.Equal(0.8f, r.AmbientLights[0].Color.Y);
        Assert.Single(r.DirectionalLights);
        Assert.Single(r.SpotLights);
    }

    [Fact]
    public void PostProcessVolumeSystem_no_chunks_no_submit()
    {
        var r = new RecordingRenderer();
        var sys = new PostProcessVolumeSystem(Host(r));
        var w = new World();
        var vSpec = SystemQuerySpec.All<PostProcessVolumeSource, Transform>();
        sys.OnStart(w, w.QueryChunks(vSpec));
        sys.OnParallelLateUpdate(w.QueryChunks(vSpec), 0f, ParOpts());
        Assert.Empty(r.Volumes);
    }

    [Fact]
    public void TextStagingSystem_maps_columns_on_late_update()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var sys = new TextStagingSystem(h);
        var w = new World();
        var spec = SystemQuerySpec.All<BitmapText, Transform>();
        var q0 = w.QueryChunks(spec);
        sys.OnStart(w, q0);
        var e = w.CreateEntity();
        w.GetOrAdd<Transform>(e) = Transform.Identity;
        w.GetOrAdd<BitmapText>(e) = new BitmapText { Visible = false, Content = "ok" };

        sys.OnLateUpdate(w.QueryChunks(spec), 0f);

        var e2 = w.CreateEntity();
        w.GetOrAdd<Transform>(e2) = Transform.Identity;
        w.GetOrAdd<BitmapText>(e2) = new BitmapText { Visible = true, Content = "visible with content skips warning branch" };

        sys.OnLateUpdate(w.QueryChunks(spec), 0f);
    }

    [Fact]
    public void Light_submitters_submit_independent_bands_and_parallel_points()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var ambS = new AmbientLightSystem(h);
        var dirS = new DirectionalLightSystem(h);
        var spotS = new SpotLightSystem(h);
        var pointS = new PointLightSystem(h);
        var w = new World();
        ambS.OnStart(w, w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()));
        dirS.OnStart(w, w.QueryChunks(SystemQuerySpec.All<DirectionalLightSource, Transform>()));
        spotS.OnStart(w, w.QueryChunks(SystemQuerySpec.All<SpotLightSource, Transform>()));
        pointS.OnStart(w, w.QueryChunks(SystemQuerySpec.All<PointLightSource, Transform>()));

        var ambLow = w.CreateEntity();
        w.GetOrAdd<AmbientLightSource>(ambLow) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.1f, 0.1f, 0.1f),
            Intensity = 1f
        };
        var ambHigh = w.CreateEntity();
        w.GetOrAdd<AmbientLightSource>(ambHigh) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.9f, 0.9f, 0.9f),
            Intensity = 2f
        };

        var dir = w.CreateEntity();
        w.GetOrAdd<Transform>(dir) = MakeTransform(localRotation: -MathF.PI * 0.5f, worldRotation: -MathF.PI * 0.5f);
        w.GetOrAdd<DirectionalLightSource>(dir) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(1f, 1f, 1f),
            Intensity = 1f
        };

        var spot = w.CreateEntity();
        w.GetOrAdd<Transform>(spot) = MakeTransform(localRotation: -MathF.PI * 0.5f, worldRotation: -MathF.PI * 0.5f);
        w.GetOrAdd<SpotLightSource>(spot) = new SpotLightSource
        {
            Active = true,
            Radius = 100f,
            InnerConeRadians = 0.5f,
            OuterConeRadians = 1f,
            Color = new Vector3D<float>(1f, 0f, 0f),
            Intensity = 1f
        };

        var p1 = w.CreateEntity();
        w.GetOrAdd<Transform>(p1) = Transform.Identity;
        w.GetOrAdd<PointLightSource>(p1) = new PointLightSource
        {
            Active = false,
            Radius = 1f,
            Color = default,
            Intensity = 1f
        };
        var p2 = w.CreateEntity();
        w.GetOrAdd<Transform>(p2) = MakeTransform(
            localPos: new Vector2D<float>(3f, 4f),
            worldPos: new Vector2D<float>(3f, 4f));
        w.GetOrAdd<PointLightSource>(p2) = new PointLightSource
        {
            Active = true,
            Radius = 10f,
            Color = new Vector3D<float>(1f, 1f, 1f),
            Intensity = 2f,
            FalloffExponent = 2f
        };

        var ptSpec = SystemQuerySpec.All<PointLightSource, Transform>();
        ambS.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()), 0.016f, ParOpts());
        dirS.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<DirectionalLightSource, Transform>()), 0.016f, ParOpts());
        spotS.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<SpotLightSource, Transform>()), 0.016f, ParOpts());
        pointS.OnParallelLateUpdate(w.QueryChunks(ptSpec), 0.016f, ParOpts());

        Assert.Equal(2, r.AmbientLights.Count);
        Assert.Single(r.DirectionalLights);
        Assert.Single(r.SpotLights);
        Assert.Single(r.PointLights);
        Assert.Equal(3f, r.PointLights[0].PositionWorld.X);
    }

    [Fact]
    public void PointLightSystem_skips_when_renderer_null_on_update()
    {
        var h = Host(new RecordingRenderer());
        var sys = new PointLightSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<PointLightSource, Transform>()));
        h.Renderer = null;
        sys.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<PointLightSource, Transform>()), 0f, ParOpts());
    }

    [Fact]
    public void AmbientLightSystem_skips_when_renderer_null_on_update()
    {
        var h = Host(new RecordingRenderer());
        var sys = new AmbientLightSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()));
        h.Renderer = null;
        sys.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()), 0f, ParOpts());
    }

    [Fact]
    public void AmbientLightSystem_with_empty_query_returns_before_parallel()
    {
        var r = new RecordingRenderer();
        var sys = new AmbientLightSystem(Host(r));
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()));
        sys.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()), 0f, ParOpts());
        Assert.Empty(r.AmbientLights);
    }

    [Fact]
    public void DirectionalLightSystem_skips_when_renderer_null_on_update()
    {
        var h = Host(new RecordingRenderer());
        var sys = new DirectionalLightSystem(h);
        var w = new World();
        var dSpec = SystemQuerySpec.All<DirectionalLightSource, Transform>();
        sys.OnStart(w, w.QueryChunks(dSpec));
        h.Renderer = null;
        sys.OnParallelLateUpdate(w.QueryChunks(dSpec), 0f, ParOpts());
    }

    [Fact]
    public void DirectionalLightSystem_with_empty_query_returns_before_parallel()
    {
        var r = new RecordingRenderer();
        var sys = new DirectionalLightSystem(Host(r));
        var w = new World();
        var dSpec = SystemQuerySpec.All<DirectionalLightSource, Transform>();
        sys.OnStart(w, w.QueryChunks(dSpec));
        sys.OnParallelLateUpdate(w.QueryChunks(dSpec), 0f, ParOpts());
        Assert.Empty(r.DirectionalLights);
    }

    [Fact]
    public void SpotLightSystem_skips_when_renderer_null_on_update()
    {
        var h = Host(new RecordingRenderer());
        var sys = new SpotLightSystem(h);
        var w = new World();
        var sSpec = SystemQuerySpec.All<SpotLightSource, Transform>();
        sys.OnStart(w, w.QueryChunks(sSpec));
        h.Renderer = null;
        sys.OnParallelLateUpdate(w.QueryChunks(sSpec), 0f, ParOpts());
    }

    [Fact]
    public void SpotLightSystem_with_empty_query_returns_before_parallel()
    {
        var r = new RecordingRenderer();
        var sys = new SpotLightSystem(Host(r));
        var w = new World();
        var sSpec = SystemQuerySpec.All<SpotLightSource, Transform>();
        sys.OnStart(w, w.QueryChunks(sSpec));
        sys.OnParallelLateUpdate(w.QueryChunks(sSpec), 0f, ParOpts());
        Assert.Empty(r.SpotLights);
    }

    [Fact]
    public void PostProcessVolumeSystem_submits_active_rows_in_parallel()
    {
        var r = new RecordingRenderer();
        var sys = new PostProcessVolumeSystem(Host(r));
        var w = new World();
        var vSpec = SystemQuerySpec.All<PostProcessVolumeSource, Transform>();
        sys.OnStart(w, w.QueryChunks(vSpec));

        var e = w.CreateEntity();
        w.GetOrAdd<Transform>(e) = MakeTransform(
            localPos: new Vector2D<float>(5f, 5f),
            worldPos: new Vector2D<float>(5f, 5f));
        w.GetOrAdd<PostProcessVolumeSource>(e) = new PostProcessVolumeSource
        {
            Active = true,
            Volume = new PostProcessVolume
            {
                HalfExtentsLocal = new Vector2D<float>(5f, 5f),
                Priority = 1,
                Overrides = default
            }
        };
        var eInactive = w.CreateEntity();
        w.GetOrAdd<Transform>(eInactive) = Transform.Identity;
        w.GetOrAdd<PostProcessVolumeSource>(eInactive) = new PostProcessVolumeSource
        {
            Active = false,
            Volume = default
        };

        sys.OnParallelLateUpdate(w.QueryChunks(vSpec), 0f, ParOpts());
        Assert.Single(r.Volumes);
    }

    [Fact]
    public void PostProcessVolumeSystem_skips_when_renderer_null()
    {
        var h = Host(new RecordingRenderer());
        var sys = new PostProcessVolumeSystem(h);
        var w = new World();
        var vSpec = SystemQuerySpec.All<PostProcessVolumeSource, Transform>();
        sys.OnStart(w, w.QueryChunks(vSpec));
        h.Renderer = null;
        sys.OnParallelLateUpdate(w.QueryChunks(vSpec), 0f, ParOpts());
    }

    [Fact]
    public void ViewportAnchorSystem_positions_screen_and_world_and_syncs_sprite()
    {
        var r = new RecordingRenderer { SwapchainPixelSize = new Vector2D<int>(800, 600) };
        var h = Host(r);
        var sys = new ViewportAnchorSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Transform>()));

        var screen = w.CreateEntity();
        w.GetOrAdd<ViewportAnchor2D>(screen) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ViewportSpace,
            Anchor = ViewportAnchorPreset.TopRight,
            OffsetX = 10f,
            OffsetY = 20f
        };
        w.GetOrAdd<Transform>(screen) = Transform.Identity;

        var world = w.CreateEntity();
        w.GetOrAdd<ViewportAnchor2D>(world) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.WorldSpace,
            Anchor = ViewportAnchorPreset.Center,
            OffsetX = 0f,
            OffsetY = 0f
        };
        w.GetOrAdd<Transform>(world) = Transform.Identity;

        var inactive = w.CreateEntity();
        w.GetOrAdd<ViewportAnchor2D>(inactive) = new ViewportAnchor2D { Active = false };
        w.GetOrAdd<Transform>(inactive) = MakeTransform(
            localPos: new Vector2D<float>(99f, 99f),
            worldPos: new Vector2D<float>(99f, 99f));

        var full = w.CreateEntity();
        w.GetOrAdd<ViewportAnchor2D>(full) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ViewportSpace,
            Anchor = ViewportAnchorPreset.LeftCenter,
            OffsetX = 0f,
            OffsetY = 0f,
            SyncSpriteHalfExtentsToViewport = true
        };
        w.GetOrAdd<Transform>(full) = Transform.Identity;
        w.GetOrAdd<Sprite>(full) = Sprite.DefaultWhiteUnlit(1, 1, new Vector2D<float>(1f, 1f));

        var presets = w.CreateEntity();
        w.GetOrAdd<ViewportAnchor2D>(presets) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ViewportSpace,
            Anchor = (ViewportAnchorPreset)42,
            OffsetX = 1f,
            OffsetY = 2f
        };
        w.GetOrAdd<Transform>(presets) = Transform.Identity;

        void AddCorner(ViewportAnchorPreset anchor, float ox, float oy)
        {
            var ent = w.CreateEntity();
            w.GetOrAdd<ViewportAnchor2D>(ent) = new ViewportAnchor2D
            {
                Active = true,
                ContentSpace = CoordinateSpace.ViewportSpace,
                Anchor = anchor,
                OffsetX = ox,
                OffsetY = oy
            };
            w.GetOrAdd<Transform>(ent) = Transform.Identity;
        }

        AddCorner(ViewportAnchorPreset.TopLeft, 3f, 4f);
        AddCorner(ViewportAnchorPreset.BottomLeft, 5f, 6f);
        AddCorner(ViewportAnchorPreset.BottomRight, 7f, 8f);

        sys.OnLateUpdate(w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Transform>()), 0f);

        ref var ts = ref w.Get<Transform>(screen);
        Assert.Equal(800f - 10f, ts.WorldPosition.X);
        Assert.Equal(20f, ts.WorldPosition.Y);

        r.SwapchainPixelSize = new Vector2D<int>(0, 600);
        sys.OnLateUpdate(w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Transform>()), 0f);
    }

    private sealed class RecordingDiagSink : IEngineDiagnosticSink
    {
        public readonly List<(EngineErrorSeverity Severity, string Title, string Message)> Calls = new();

        public void Deliver(EngineErrorSeverity severity, string title, string message) =>
            Calls.Add((severity, title, message));
    }

    [Fact]
    public void TextStagingSystem_warns_once_for_visible_empty_content()
    {
        var sink = new RecordingDiagSink();
        var prev = EngineDiagnostics.SinkOverride;
        EngineDiagnostics.SinkOverride = sink;
        try
        {
            var r = new RecordingRenderer();
            var h = Host(r);
            var sys = new TextStagingSystem(h);
            var w = new World();
            sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<BitmapText, Transform>()));

            var e = w.CreateEntity();
            w.GetOrAdd<Transform>(e) = Transform.Identity;
            w.GetOrAdd<BitmapText>(e) = new BitmapText { Visible = true, Content = "" };

            sys.OnLateUpdate(w.QueryChunks(SystemQuerySpec.All<BitmapText, Transform>()), 0f);
            sys.OnLateUpdate(w.QueryChunks(SystemQuerySpec.All<BitmapText, Transform>()), 0f);

            Assert.Single(sink.Calls, c => c.Severity == EngineErrorSeverity.Warning && c.Message.Contains("empty Content", StringComparison.Ordinal));
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void TextStagingSystem_OnStart_allows_null_renderer()
    {
        var h = new GameHostServices() { Renderer = null };
        var sys = new TextStagingSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<BitmapText, Transform>()));
    }

    [Fact]
    public void TextStagingSystem_OnLateUpdate_handles_hidden_rows()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var sys = new TextStagingSystem(h);
        var w = new World();
        var spec = SystemQuerySpec.All<BitmapText, Transform>();
        sys.OnStart(w, w.QueryChunks(spec));
        var e = w.CreateEntity();
        w.GetOrAdd<Transform>(e) = Transform.Identity;
        w.GetOrAdd<BitmapText>(e) = new BitmapText { Visible = false, Content = "x" };
        sys.OnLateUpdate(w.QueryChunks(spec), 0f);
    }
}
