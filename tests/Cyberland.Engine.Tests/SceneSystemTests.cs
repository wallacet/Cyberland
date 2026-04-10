using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class SceneSystemTests
{
    private static ParallelOptions ParOpts() => new ParallelismSettings().CreateParallelOptions();

    private static GameHostServices Host(IRenderer r, TilemapDataStore? tm = null, ParticleStore? pt = null)
    {
        var kb = new KeyBindingStore();
        var h = new GameHostServices(kb) { Renderer = r };
        h.Tilemaps = tm ?? new TilemapDataStore();
        h.Particles = pt ?? new ParticleStore();
        return h;
    }

    [Fact]
    public void TransformHierarchySystem_parent_with_Transform_composes_world_matrix()
    {
        var w = new World();
        var root = w.CreateEntity();
        var child = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(root) = Transform.Identity;
        ref var tc = ref w.Components<Transform>().GetOrAdd(child);
        tc = Transform.Identity;
        tc.LocalPosition = new Vector2D<float>(2f, 1f);
        tc.Parent = root;

        new TransformHierarchySystem().OnParallelUpdate(w, 0f, ParOpts());
        var wp = w.Components<Position>().Get(child);
        Assert.Equal(2f, wp.X, 3);
        Assert.Equal(1f, wp.Y, 3);
    }

    [Fact]
    public void TransformHierarchySystem_propagates_parent_translation()
    {
        var w = new World();
        var parent = w.CreateEntity();
        var child = w.CreateEntity();
        ref var pp = ref w.Components<Position>().GetOrAdd(parent);
        pp.X = 10f;
        pp.Y = 5f;

        ref var tc = ref w.Components<Transform>().GetOrAdd(child);
        tc = Transform.Identity;
        tc.LocalPosition = new Vector2D<float>(3f, 4f);
        tc.Parent = parent;

        new TransformHierarchySystem().OnParallelUpdate(w, 0.016f, ParOpts());

        var wp = w.Components<Position>().Get(child);
        Assert.Equal(13f, wp.X, 3);
        Assert.Equal(9f, wp.Y, 3);
    }

    [Fact]
    public void SpriteRenderSystem_noop_when_renderer_null()
    {
        var kb = new KeyBindingStore();
        var h = new GameHostServices(kb) { Renderer = null };
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Position>().GetOrAdd(e) = new Position { X = 1f, Y = 2f };
        w.Components<Sprite>().GetOrAdd(e) = Sprite.DefaultWhiteUnlit(0, 0, new Vector2D<float>(1f, 1f));

        new SpriteRenderSystem(h).OnParallelUpdate(w, 0f, ParOpts());
        // No throw; early exit when host has no renderer (e.g. headless or pre-init).
    }

    [Fact]
    public void SpriteRenderSystem_submits_when_visible()
    {
        var r = new RecordingRenderer();
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Position>().GetOrAdd(e) = new Position { X = 1f, Y = 2f };
        ref var spr = ref w.Components<Sprite>().GetOrAdd(e);
        spr = Sprite.DefaultWhiteUnlit(2, 1, new Vector2D<float>(4f, 4f));
        spr.Visible = true;

        new SpriteRenderSystem(Host(r)).OnParallelUpdate(w, 0.016f, ParOpts());
        Assert.Single(r.Sprites);
        Assert.Equal(1f, r.Sprites[0].CenterWorld.X);
    }

    [Fact]
    public void SpriteRenderSystem_skips_without_Position()
    {
        var r = new RecordingRenderer();
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Sprite>().GetOrAdd(e) = Sprite.DefaultWhiteUnlit(2, 1, new Vector2D<float>(1f, 1f));

        new SpriteRenderSystem(Host(r)).OnParallelUpdate(w, 0.016f, ParOpts());
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void SpriteAnimationSystem_noop_when_no_animation_chunks()
    {
        var w = new World();
        var opts = new ParallelismSettings().CreateParallelOptions();
        new SpriteAnimationSystem().OnParallelUpdate(w, 0.1f, opts);
    }

    [Fact]
    public void SpriteAnimationSystem_writes_uv_rect()
    {
        var w = new World();
        var e = w.CreateEntity();
        ref var spr = ref w.Components<Sprite>().GetOrAdd(e);
        spr = Sprite.DefaultWhiteUnlit(0, 0, new Vector2D<float>(1f, 1f));
        w.Components<SpriteAnimation>().GetOrAdd(e) = new SpriteAnimation
        {
            ElapsedSeconds = 0f,
            SecondsPerFrame = 0.5f,
            FrameCount = 4,
            AtlasColumns = 2,
            Loop = true
        };

        var opts = new ParallelismSettings().CreateParallelOptions();
        new SpriteAnimationSystem().OnParallelUpdate(w, 0.6f, opts);
        var outSpr = w.Components<Sprite>().Get(e);
        Assert.True(outSpr.UvRect.Z > outSpr.UvRect.X);
    }

    [Fact]
    public void TilemapRenderSystem_emits_tiles()
    {
        var r = new RecordingRenderer();
        var tm = new TilemapDataStore();
        var w = new World();
        var map = w.CreateEntity();
        tm.Register(map, new[] { 1, 0 }, 1, 2);
        w.Components<Tilemap>().GetOrAdd(map) = new Tilemap
        {
            TileWidth = 16f,
            TileHeight = 16f,
            OriginX = 0f,
            OriginY = 0f,
            AtlasAlbedoTextureId = 2,
            Layer = 0,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1
        };

        new TilemapRenderSystem(Host(r, tm)).OnParallelUpdate(w, 0f, ParOpts());
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void ParticleSimulation_and_render_round_trip()
    {
        var r = new RecordingRenderer();
        var pt = new ParticleStore();
        var h = Host(r, pt: pt);
        var w = new World();
        var emitter = w.CreateEntity();
        w.Components<Position>().GetOrAdd(emitter) = new Position { X = 100f, Y = 100f };
        w.Components<ParticleEmitter>().GetOrAdd(emitter) = new ParticleEmitter
        {
            Active = true,
            MaxParticles = 4,
            ParticleLifeSeconds = 1f,
            SpawnIntervalSeconds = 0.1f,
            EmissionVelocity = new Vector2D<float>(0f, 50f),
            GravityY = -10f,
            AlbedoTextureId = 2,
            Layer = 1,
            SortKey = 0f,
            HalfExtent = 4f
        };

        var sim = new ParticleSimulationSystem(h);
        sim.OnParallelUpdate(w, 0.5f, ParOpts());
        new ParticleRenderSystem(h).OnParallelUpdate(w, 0f, ParOpts());
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void SpriteAnimationMath_clamps_non_loop()
    {
        var a = new SpriteAnimation
        {
            ElapsedSeconds = 100f,
            SecondsPerFrame = 1f,
            FrameCount = 2,
            AtlasColumns = 2,
            Loop = false
        };
        var spr = Sprite.DefaultWhiteUnlit(0, 0, new Vector2D<float>(1f, 1f));
        SpriteAnimationMath.Apply(ref a, ref spr);
        Assert.Equal(0.5f, spr.UvRect.Z - spr.UvRect.X, 3);
    }
}
