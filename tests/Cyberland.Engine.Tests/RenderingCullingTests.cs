using System.Collections.Generic;
using System.Linq;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

[Collection("EngineDiagnostics")]
public sealed class RenderingCullingTests
{
    [Fact]
    public void SpriteRenderSystem_submits_world_sprites_without_cpu_frustum_cull_but_keeps_viewport_space_tags()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer };
        host.CameraRuntimeState = new CameraRuntimeState(
            ViewportSizeWorld: new Vector2D<int>(100, 100),
            PositionWorld: new Vector2D<float>(50f, 50f),
            RotationRadians: 0f,
            BackgroundColor: default,
            Priority: 0,
            Valid: true);

        var world = new World();
        var inside = world.CreateEntity();
        world.GetOrAdd<Transform>(inside) = Transform.Identity;
        world.GetOrAdd<Sprite>(inside) = new Sprite
        {
            Visible = true,
            HalfExtents = new Vector2D<float>(8f, 8f),
            Layer = (int)SpriteLayer.World,
            Space = CoordinateSpace.WorldSpace
        };
        ref var insideTf = ref world.Get<Transform>(inside);
        insideTf.WorldPosition = new Vector2D<float>(50f, 50f);

        var outside = world.CreateEntity();
        world.GetOrAdd<Transform>(outside) = Transform.Identity;
        world.GetOrAdd<Sprite>(outside) = new Sprite
        {
            Visible = true,
            HalfExtents = new Vector2D<float>(8f, 8f),
            Layer = (int)SpriteLayer.World,
            Space = CoordinateSpace.WorldSpace
        };
        ref var outsideTf = ref world.Get<Transform>(outside);
        outsideTf.WorldPosition = new Vector2D<float>(500f, 500f);

        var viewport = world.CreateEntity();
        world.GetOrAdd<Transform>(viewport) = Transform.Identity;
        world.GetOrAdd<Sprite>(viewport) = new Sprite
        {
            Visible = true,
            HalfExtents = new Vector2D<float>(8f, 8f),
            Layer = (int)SpriteLayer.Ui,
            Space = CoordinateSpace.ViewportSpace
        };
        ref var viewportTf = ref world.Get<Transform>(viewport);
        viewportTf.WorldPosition = new Vector2D<float>(500f, 500f);

        var sys = new SpriteRenderSystem(host);
        var spec = SystemQuerySpec.All<Sprite, Transform>();
        var query = world.QueryChunks(spec);
        sys.OnStart(world, query);
        sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());

        Assert.Equal(3, renderer.Sprites.Count);
    }

    [Fact]
    public void ClassifyDeferredTransparent_straight_alpha_tint_routes_transparent_even_when_component_flag_false()
    {
        Sprite s = default;
        s.Alpha = 1f;
        s.Transparent = false;
        s.ColorMultiply = new Vector4D<float>(0.2f, 0.8f, 0.25f, 0.4f);
        Assert.True(SpriteRenderSystem.ClassifyDeferredTransparent(in s));
    }

    [Fact]
    public void ClassifyDeferredTransparent_fully_opaque_mouse_chase_courier_is_not_transparent()
    {
        Sprite s = default;
        s.Alpha = 1f;
        s.Transparent = false;
        s.ColorMultiply = new Vector4D<float>(0.2f, 0.95f, 1f, 1f);
        Assert.False(SpriteRenderSystem.ClassifyDeferredTransparent(in s));
    }

    [Fact]
    public void SpriteRenderSystem_skips_camera_culling_when_runtime_state_invalid()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer };
        host.CameraRuntimeState = new CameraRuntimeState(
            ViewportSizeWorld: new Vector2D<int>(0, 0),
            PositionWorld: default,
            RotationRadians: 0f,
            BackgroundColor: default,
            Priority: 0,
            Valid: false);

        var world = new World();
        var spriteEntity = world.CreateEntity();
        world.GetOrAdd<Transform>(spriteEntity) = Transform.Identity;
        world.GetOrAdd<Sprite>(spriteEntity) = new Sprite
        {
            Visible = true,
            HalfExtents = new Vector2D<float>(8f, 8f),
            Layer = (int)SpriteLayer.World,
            Space = CoordinateSpace.WorldSpace
        };
        ref var tf = ref world.Get<Transform>(spriteEntity);
        tf.WorldPosition = new Vector2D<float>(2000f, 2000f);

        var sys = new SpriteRenderSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<Sprite, Transform>());
        sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());
        Assert.Single(renderer.Sprites);
    }

    [Fact]
    public void SpriteRenderSystem_skips_camera_culling_when_camera_viewport_is_non_positive()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer };
        host.CameraRuntimeState = new CameraRuntimeState(
            ViewportSizeWorld: new Vector2D<int>(0, 0),
            PositionWorld: new Vector2D<float>(50f, 50f),
            RotationRadians: 0f,
            BackgroundColor: default,
            Priority: 0,
            Valid: true);

        var world = new World();
        var spriteEntity = world.CreateEntity();
        world.GetOrAdd<Transform>(spriteEntity) = Transform.Identity;
        world.GetOrAdd<Sprite>(spriteEntity) = new Sprite
        {
            Visible = true,
            HalfExtents = new Vector2D<float>(8f, 8f),
            Layer = (int)SpriteLayer.World,
            Space = CoordinateSpace.WorldSpace
        };
        ref var tf = ref world.Get<Transform>(spriteEntity);
        tf.WorldPosition = new Vector2D<float>(500f, 500f);

        var sys = new SpriteRenderSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<Sprite, Transform>());
        sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());
        Assert.Single(renderer.Sprites);
    }

    [Fact]
    public void SpriteRenderSystem_submits_mouse_chase_like_opaque_courier_pose()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices { Renderer = renderer };
        host.CameraRuntimeState = new CameraRuntimeState(
            ViewportSizeWorld: new Vector2D<int>(1280, 720),
            PositionWorld: new Vector2D<float>(640f, 360f),
            RotationRadians: 0f,
            BackgroundColor: default,
            Priority: 0,
            Valid: true);

        var world = new World();
        var courier = world.CreateEntity();
        world.GetOrAdd<Transform>(courier) = Transform.Identity;
        ref var tf = ref world.Get<Transform>(courier);
        tf.WorldPosition = new Vector2D<float>(260f, 360f);
        world.GetOrAdd<Sprite>(courier) = new Sprite
        {
            Visible = true,
            HalfExtents = new Vector2D<float>(22f, 22f),
            AlbedoTextureId = renderer.WhiteTextureId,
            NormalTextureId = renderer.DefaultNormalTextureId,
            Layer = (int)SpriteLayer.World,
            Space = CoordinateSpace.WorldSpace,
            Transparent = false,
            ColorMultiply = new Vector4D<float>(0.2f, 0.95f, 1f, 1f),
            Alpha = 1f
        };

        var sys = new SpriteRenderSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<Sprite, Transform>());
        sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());
        Assert.Single(renderer.Sprites);
    }

    [Fact]
    public void TilemapRenderSystem_limits_iteration_to_visible_camera_region()
    {
        var renderer = new RecordingRenderer();
        var tilemaps = new TilemapDataStore();
        var host = new GameHostServices { Renderer = renderer, Tilemaps = tilemaps };
        host.CameraRuntimeState = new CameraRuntimeState(
            ViewportSizeWorld: new Vector2D<int>(100, 100),
            PositionWorld: new Vector2D<float>(50f, -50f),
            RotationRadians: 0f,
            BackgroundColor: default,
            Priority: 0,
            Valid: true);

        var world = new World();
        var tilemapEntity = world.CreateEntity();
        world.GetOrAdd<Transform>(tilemapEntity) = Transform.Identity;
        world.GetOrAdd<Tilemap>(tilemapEntity) = new Tilemap
        {
            TileWidth = 10f,
            TileHeight = 10f,
            AtlasAlbedoTextureId = 0,
            Layer = (int)SpriteLayer.World,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1,
            AtlasColumns = 1,
            AtlasRows = 1
        };

        var cols = 100;
        var rows = 100;
        var tiles = new int[cols * rows];
        Array.Fill(tiles, 1);
        tilemaps.Register(tilemapEntity, tiles, cols, rows);

        var sys = new TilemapRenderSystem(host);
        var spec = SystemQuerySpec.All<Tilemap, Transform>();
        var query = world.QueryChunks(spec);
        sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());

        Assert.True(renderer.Sprites.Count > 0);
        Assert.InRange(renderer.Sprites.Count, 100, 144);
    }

    [Fact]
    public void TilemapRenderSystem_skips_submit_when_camera_region_does_not_intersect_map()
    {
        var renderer = new RecordingRenderer();
        var tilemaps = new TilemapDataStore();
        var host = new GameHostServices { Renderer = renderer, Tilemaps = tilemaps };
        host.CameraRuntimeState = new CameraRuntimeState(
            ViewportSizeWorld: new Vector2D<int>(100, 100),
            PositionWorld: new Vector2D<float>(10000f, 10000f),
            RotationRadians: 0f,
            BackgroundColor: default,
            Priority: 0,
            Valid: true);

        var world = new World();
        var tilemapEntity = world.CreateEntity();
        world.GetOrAdd<Transform>(tilemapEntity) = Transform.Identity;
        world.GetOrAdd<Tilemap>(tilemapEntity) = new Tilemap
        {
            TileWidth = 10f,
            TileHeight = 10f,
            AtlasAlbedoTextureId = 0,
            Layer = (int)SpriteLayer.World,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1,
            AtlasColumns = 1,
            AtlasRows = 1
        };

        var tiles = new int[100];
        Array.Fill(tiles, 1);
        tilemaps.Register(tilemapEntity, tiles, 10, 10);

        var sys = new TilemapRenderSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<Tilemap, Transform>());
        sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());
        Assert.Empty(renderer.Sprites);
    }

    [Fact]
    public void TilemapRenderSystem_falls_back_when_tilemap_matrix_is_non_invertible()
    {
        var renderer = new RecordingRenderer();
        var tilemaps = new TilemapDataStore();
        var host = new GameHostServices { Renderer = renderer, Tilemaps = tilemaps };
        host.CameraRuntimeState = new CameraRuntimeState(
            ViewportSizeWorld: new Vector2D<int>(50, 50),
            PositionWorld: new Vector2D<float>(20f, -20f),
            RotationRadians: 0f,
            BackgroundColor: default,
            Priority: 0,
            Valid: true);

        var world = new World();
        var tilemapEntity = world.CreateEntity();
        var tf = Transform.Identity;
        tf.LocalScale = new Vector2D<float>(0f, 1f);
        world.GetOrAdd<Transform>(tilemapEntity) = tf;
        world.GetOrAdd<Tilemap>(tilemapEntity) = new Tilemap
        {
            TileWidth = 10f,
            TileHeight = 10f,
            AtlasAlbedoTextureId = 0,
            Layer = (int)SpriteLayer.World,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1,
            AtlasColumns = 1,
            AtlasRows = 1
        };

        var tiles = new int[25];
        Array.Fill(tiles, 1);
        tilemaps.Register(tilemapEntity, tiles, 5, 5);

        var sys = new TilemapRenderSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<Tilemap, Transform>());
        sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());
        Assert.Equal(25, renderer.Sprites.Count);
    }

    [Fact]
    public void TilemapRenderSystem_handles_non_positive_tile_size_without_visibility_bounds_math()
    {
        var renderer = new RecordingRenderer();
        var tilemaps = new TilemapDataStore();
        var host = new GameHostServices { Renderer = renderer, Tilemaps = tilemaps };
        host.CameraRuntimeState = new CameraRuntimeState(
            ViewportSizeWorld: new Vector2D<int>(64, 64),
            PositionWorld: default,
            RotationRadians: 0f,
            BackgroundColor: default,
            Priority: 0,
            Valid: true);

        var world = new World();
        var tilemapEntity = world.CreateEntity();
        world.GetOrAdd<Transform>(tilemapEntity) = Transform.Identity;
        world.GetOrAdd<Tilemap>(tilemapEntity) = new Tilemap
        {
            TileWidth = 0f,
            TileHeight = 8f,
            AtlasAlbedoTextureId = 0,
            Layer = (int)SpriteLayer.World,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1,
            AtlasColumns = 1,
            AtlasRows = 1
        };

        tilemaps.Register(tilemapEntity, new[] { 1 }, 1, 1);
        var sys = new TilemapRenderSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<Tilemap, Transform>());
        sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());
        Assert.Single(renderer.Sprites);
    }

    [Fact]
    public void TilemapRenderSystem_does_not_emit_batch_cap_warning_when_chunking_large_maps()
    {
        var sink = new RecordingDiagSink();
        var prevSink = EngineDiagnostics.SinkOverride;
        EngineDiagnostics.SinkOverride = sink;
        try
        {
            var renderer = new RecordingRenderer();
            var tilemaps = new TilemapDataStore();
            var host = new GameHostServices { Renderer = renderer, Tilemaps = tilemaps };
            host.CameraRuntimeState = new CameraRuntimeState(
                ViewportSizeWorld: new Vector2D<int>(2000, 2000),
                PositionWorld: new Vector2D<float>(1000f, -1000f),
                RotationRadians: 0f,
                BackgroundColor: default,
                Priority: 0,
                Valid: true);

            var world = new World();
            var tilemapEntity = world.CreateEntity();
            world.GetOrAdd<Transform>(tilemapEntity) = Transform.Identity;
            world.GetOrAdd<Tilemap>(tilemapEntity) = new Tilemap
            {
                TileWidth = 8f,
                TileHeight = 8f,
                AtlasAlbedoTextureId = 0,
                Layer = (int)SpriteLayer.World,
                SortKey = 0f,
                NonEmptyTileMinIndex = 1,
                AtlasColumns = 1,
                AtlasRows = 1
            };

            const int cols = 128;
            const int rows = 128;
            var tiles = new int[cols * rows];
            Array.Fill(tiles, 1);
            tilemaps.Register(tilemapEntity, tiles, cols, rows);

            var sys = new TilemapRenderSystem(host);
            var query = world.QueryChunks(SystemQuerySpec.All<Tilemap, Transform>());
            sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());
            sys.OnParallelLateUpdate(query, 0.016f, new ParallelismSettings().CreateParallelOptions());

            Assert.DoesNotContain(
                sink.Calls,
                c => c.Severity == EngineErrorSeverity.Warning &&
                     c.Title.Contains("TilemapRenderSystem", StringComparison.Ordinal));
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prevSink;
        }
    }

    private sealed class RecordingDiagSink : IEngineDiagnosticSink
    {
        public readonly List<(EngineErrorSeverity Severity, string Title, string Message)> Calls = [];

        public void Deliver(EngineErrorSeverity severity, string title, string message) =>
            Calls.Add((severity, title, message));
    }
}
