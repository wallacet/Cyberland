using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

/// <summary>Coverage for <see cref="DeferredSubmissionQueries"/> (via public systems), viewport anchors, and text staging.</summary>
[Collection("EngineDiagnostics")]
public sealed class DeferredSubmissionAndSceneSystemsTests
{
    private static ParallelOptions ParOpts() => new ParallelismSettings().CreateParallelOptions();

    private static GameHostServices Host(IRenderer r) => new GameHostServices(new KeyBindingStore()) { Renderer = r };

    [Fact]
    public void Engine_submit_systems_expose_expected_query_specs()
    {
        var r = new RecordingRenderer();
        Assert.Equal(SystemQuerySpec.Empty, new LightingSystem(Host(r)).QuerySpec);
        Assert.Equal(SystemQuerySpec.Empty, new PostProcessVolumeSystem(Host(r)).QuerySpec);
        Assert.Equal(SystemQuerySpec.All<ViewportAnchor2D, Position>(), new ViewportAnchorSystem(Host(r)).QuerySpec);
        Assert.Equal(SystemQuerySpec.All<BitmapText, Position>(), new TextStagingSystem(Host(r)).QuerySpec);
    }

    [Fact]
    public void LightingSystem_OnStart_requires_renderer()
    {
        var h = new GameHostServices(new KeyBindingStore()) { Renderer = null };
        var sys = new LightingSystem(h);
        var w = new World();
        var prev = EngineDiagnostics.SinkOverride;
        EngineDiagnostics.SinkOverride = new RecordingDiagSink();
        try
        {
            Assert.Throws<InvalidOperationException>(() => sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty)));
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void PostProcessVolumeSystem_OnStart_requires_renderer()
    {
        var h = new GameHostServices(new KeyBindingStore()) { Renderer = null };
        var sys = new PostProcessVolumeSystem(h);
        var w = new World();
        var prev = EngineDiagnostics.SinkOverride;
        EngineDiagnostics.SinkOverride = new RecordingDiagSink();
        try
        {
            Assert.Throws<InvalidOperationException>(() => sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty)));
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void ViewportAnchorSystem_OnStart_requires_renderer()
    {
        var h = new GameHostServices(new KeyBindingStore()) { Renderer = null };
        var sys = new ViewportAnchorSystem(h);
        var w = new World();
        var prev = EngineDiagnostics.SinkOverride;
        EngineDiagnostics.SinkOverride = new RecordingDiagSink();
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Position>())));
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void LightingSystem_skips_point_pass_when_no_point_sources()
    {
        var r = new RecordingRenderer();
        var sys = new LightingSystem(Host(r));
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty));

        var amb = w.CreateEntity();
        w.Components<AmbientLightSource>().GetOrAdd(amb) = new AmbientLightSource
        {
            Active = true,
            Light = new AmbientLight { Color = new Vector3D<float>(1f, 1f, 1f), Intensity = 0.2f }
        };

        sys.OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.Empty), 0f, ParOpts());
        Assert.Single(r.AmbientLights);
        Assert.Empty(r.PointLights);
    }

    [Fact]
    public void LightingSystem_skips_inactive_singleton_rows_before_best()
    {
        var r = new RecordingRenderer();
        var sys = new LightingSystem(Host(r));
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty));

        var ambOff = w.CreateEntity();
        w.Components<AmbientLightSource>().GetOrAdd(ambOff) = new AmbientLightSource
        {
            Active = false,
            Light = new AmbientLight { Color = new Vector3D<float>(0.2f, 0f, 0f), Intensity = 99f }
        };
        var ambOn = w.CreateEntity();
        w.Components<AmbientLightSource>().GetOrAdd(ambOn) = new AmbientLightSource
        {
            Active = true,
            Light = new AmbientLight { Color = new Vector3D<float>(0f, 0.8f, 0f), Intensity = 1f }
        };

        var dirOff = w.CreateEntity();
        w.Components<DirectionalLightSource>().GetOrAdd(dirOff) = new DirectionalLightSource { Active = false, Light = default };
        var dirOn = w.CreateEntity();
        w.Components<DirectionalLightSource>().GetOrAdd(dirOn) = new DirectionalLightSource
        {
            Active = true,
            Light = new DirectionalLight
            {
                DirectionWorld = new Vector2D<float>(0f, 1f),
                Color = new Vector3D<float>(0.5f, 0.5f, 0.5f),
                Intensity = 1f
            }
        };

        var spotOff = w.CreateEntity();
        w.Components<SpotLightSource>().GetOrAdd(spotOff) = new SpotLightSource { Active = false, Light = default };
        var spotOn = w.CreateEntity();
        w.Components<SpotLightSource>().GetOrAdd(spotOn) = new SpotLightSource
        {
            Active = true,
            Light = new SpotLight
            {
                PositionWorld = new Vector2D<float>(1f, 2f),
                DirectionWorld = new Vector2D<float>(0f, -1f),
                Radius = 50f,
                InnerConeRadians = 0.4f,
                OuterConeRadians = 0.9f,
                Color = new Vector3D<float>(1f, 1f, 0f),
                Intensity = 1f
            }
        };

        sys.OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.Empty), 0f, ParOpts());
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
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty));
        sys.OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.Empty), 0f, ParOpts());
        Assert.Empty(r.Volumes);
    }

    [Fact]
    public void TextStagingSystem_maps_columns_when_OnLateUpdate_runs_before_OnStart()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var sys = new TextStagingSystem(h);
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Position>().GetOrAdd(e) = default;
        w.Components<BitmapText>().GetOrAdd(e) = new BitmapText { Visible = false, Content = "ok" };

        sys.OnLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0f);

        var e2 = w.CreateEntity();
        w.Components<Position>().GetOrAdd(e2) = default;
        w.Components<BitmapText>().GetOrAdd(e2) = new BitmapText { Visible = true, Content = "visible with content skips warning branch" };

        sys.OnLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0f);
    }

    [Fact]
    public void LightingSystem_submits_best_singleton_lights_and_parallel_points()
    {
        var r = new RecordingRenderer();
        var sys = new LightingSystem(Host(r));
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty));

        var ambLow = w.CreateEntity();
        w.Components<AmbientLightSource>().GetOrAdd(ambLow) = new AmbientLightSource
        {
            Active = true,
            Light = new AmbientLight { Color = new Vector3D<float>(0.1f, 0.1f, 0.1f), Intensity = 1f }
        };
        var ambHigh = w.CreateEntity();
        w.Components<AmbientLightSource>().GetOrAdd(ambHigh) = new AmbientLightSource
        {
            Active = true,
            Light = new AmbientLight { Color = new Vector3D<float>(0.9f, 0.9f, 0.9f), Intensity = 2f }
        };

        var dir = w.CreateEntity();
        w.Components<DirectionalLightSource>().GetOrAdd(dir) = new DirectionalLightSource
        {
            Active = true,
            Light = new DirectionalLight
            {
                DirectionWorld = new Vector2D<float>(0f, -1f),
                Color = new Vector3D<float>(1f, 1f, 1f),
                Intensity = 1f
            }
        };

        var spot = w.CreateEntity();
        w.Components<SpotLightSource>().GetOrAdd(spot) = new SpotLightSource
        {
            Active = true,
            Light = new SpotLight
            {
                PositionWorld = default,
                DirectionWorld = new Vector2D<float>(0f, -1f),
                Radius = 100f,
                InnerConeRadians = 0.5f,
                OuterConeRadians = 1f,
                Color = new Vector3D<float>(1f, 0f, 0f),
                Intensity = 1f
            }
        };

        var p1 = w.CreateEntity();
        w.Components<PointLightSource>().GetOrAdd(p1) = new PointLightSource
        {
            Active = false,
            Light = new PointLight { PositionWorld = default, Radius = 1f, Color = default, Intensity = 1f }
        };
        var p2 = w.CreateEntity();
        w.Components<PointLightSource>().GetOrAdd(p2) = new PointLightSource
        {
            Active = true,
            Light = new PointLight
            {
                PositionWorld = new Vector2D<float>(3f, 4f),
                Radius = 10f,
                Color = new Vector3D<float>(1f, 1f, 1f),
                Intensity = 2f,
                FalloffExponent = 2f
            }
        };

        sys.OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.Empty), 0.016f, ParOpts());

        Assert.Single(r.AmbientLights);
        Assert.Equal(2f, r.AmbientLights[0].Intensity);
        Assert.Single(r.DirectionalLights);
        Assert.Single(r.SpotLights);
        Assert.Single(r.PointLights);
        Assert.Equal(3f, r.PointLights[0].PositionWorld.X);
    }

    [Fact]
    public void LightingSystem_skips_when_renderer_null_on_update()
    {
        var h = Host(new RecordingRenderer());
        var sys = new LightingSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty));
        h.Renderer = null;
        sys.OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.Empty), 0f, ParOpts());
    }

    [Fact]
    public void PostProcessVolumeSystem_submits_active_rows_in_parallel()
    {
        var r = new RecordingRenderer();
        var sys = new PostProcessVolumeSystem(Host(r));
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty));

        var e = w.CreateEntity();
        w.Components<PostProcessVolumeSource>().GetOrAdd(e) = new PostProcessVolumeSource
        {
            Active = true,
            Volume = new PostProcessVolume
            {
                MinWorld = new Vector2D<float>(0f, 0f),
                MaxWorld = new Vector2D<float>(10f, 10f),
                Priority = 1,
                Overrides = default
            }
        };
        var eInactive = w.CreateEntity();
        w.Components<PostProcessVolumeSource>().GetOrAdd(eInactive) = new PostProcessVolumeSource { Active = false, Volume = default };

        sys.OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.Empty), 0f, ParOpts());
        Assert.Single(r.Volumes);
    }

    [Fact]
    public void PostProcessVolumeSystem_skips_when_renderer_null()
    {
        var h = Host(new RecordingRenderer());
        var sys = new PostProcessVolumeSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.Empty));
        h.Renderer = null;
        sys.OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.Empty), 0f, ParOpts());
    }

    [Fact]
    public void ViewportAnchorSystem_positions_screen_and_world_and_syncs_sprite()
    {
        var r = new RecordingRenderer { SwapchainPixelSize = new Vector2D<int>(800, 600) };
        var h = Host(r);
        var sys = new ViewportAnchorSystem(h);
        var w = new World();
        sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Position>()));

        var screen = w.CreateEntity();
        w.Components<ViewportAnchor2D>().GetOrAdd(screen) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ScreenSpace,
            Anchor = ViewportAnchorPreset.TopRight,
            OffsetX = 10f,
            OffsetY = 20f
        };
        w.Components<Position>().GetOrAdd(screen) = new Position { X = 0f, Y = 0f };

        var world = w.CreateEntity();
        w.Components<ViewportAnchor2D>().GetOrAdd(world) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.WorldSpace,
            Anchor = ViewportAnchorPreset.Center,
            OffsetX = 0f,
            OffsetY = 0f
        };
        w.Components<Position>().GetOrAdd(world) = default;

        var inactive = w.CreateEntity();
        w.Components<ViewportAnchor2D>().GetOrAdd(inactive) = new ViewportAnchor2D { Active = false };
        w.Components<Position>().GetOrAdd(inactive) = new Position { X = 99f, Y = 99f };

        var full = w.CreateEntity();
        w.Components<ViewportAnchor2D>().GetOrAdd(full) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ScreenSpace,
            Anchor = ViewportAnchorPreset.LeftCenter,
            OffsetX = 0f,
            OffsetY = 0f,
            SyncSpriteHalfExtentsToViewport = true
        };
        w.Components<Position>().GetOrAdd(full) = default;
        w.Components<Sprite>().GetOrAdd(full) = Sprite.DefaultWhiteUnlit(1, 1, new Vector2D<float>(1f, 1f));

        var presets = w.CreateEntity();
        w.Components<ViewportAnchor2D>().GetOrAdd(presets) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ScreenSpace,
            Anchor = (ViewportAnchorPreset)42,
            OffsetX = 1f,
            OffsetY = 2f
        };
        w.Components<Position>().GetOrAdd(presets) = default;

        void AddCorner(ViewportAnchorPreset anchor, float ox, float oy)
        {
            var ent = w.CreateEntity();
            w.Components<ViewportAnchor2D>().GetOrAdd(ent) = new ViewportAnchor2D
            {
                Active = true,
                ContentSpace = CoordinateSpace.ScreenSpace,
                Anchor = anchor,
                OffsetX = ox,
                OffsetY = oy
            };
            w.Components<Position>().GetOrAdd(ent) = default;
        }

        AddCorner(ViewportAnchorPreset.TopLeft, 3f, 4f);
        AddCorner(ViewportAnchorPreset.BottomLeft, 5f, 6f);
        AddCorner(ViewportAnchorPreset.BottomRight, 7f, 8f);

        sys.OnLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Position>()), 0f);

        ref var ps = ref w.Components<Position>().Get(screen);
        Assert.Equal(800f - 10f, ps.X);
        Assert.Equal(20f, ps.Y);

        h.Renderer = null;
        sys.OnLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Position>()), 0f);

        r.SwapchainPixelSize = new Vector2D<int>(0, 600);
        h.Renderer = r;
        sys.OnLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<ViewportAnchor2D, Position>()), 0f);
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
            sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()));

            var e = w.CreateEntity();
            w.Components<Position>().GetOrAdd(e) = default;
            w.Components<BitmapText>().GetOrAdd(e) = new BitmapText { Visible = true, Content = "" };

            sys.OnLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0f);
            sys.OnLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<BitmapText, Position>()), 0f);

            Assert.Single(sink.Calls, c => c.Severity == EngineErrorSeverity.Warning && c.Message.Contains("empty Content", StringComparison.Ordinal));
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void TextStagingSystem_OnStart_requires_renderer()
    {
        var h = new GameHostServices(new KeyBindingStore()) { Renderer = null };
        var sys = new TextStagingSystem(h);
        var w = new World();
        Assert.Throws<InvalidOperationException>(() => sys.OnStart(w, w.QueryChunks(SystemQuerySpec.All<BitmapText, Position>())));
    }
}
