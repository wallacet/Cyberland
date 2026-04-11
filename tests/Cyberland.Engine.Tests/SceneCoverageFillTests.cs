using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

/// <summary>Exercises remaining branches/paths for line coverage on Scene APIs.</summary>
public sealed class SceneCoverageFillTests
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
    public void Position_AsVector_FromVector_round_trip()
    {
        var v = new Vector2D<float>(2f, 3f);
        var p = Position.FromVector(v);
        Assert.Equal(2f, p.AsVector().X);
        Assert.Equal(3f, p.AsVector().Y);
    }

    [Fact]
    public void Scale_One_and_AsVector()
    {
        var s = Scale.One;
        Assert.Equal(1f, s.X);
        Assert.Equal(1f, s.AsVector().Y);
    }

    [Fact]
    public void Transform_Identity_values()
    {
        var t = Transform.Identity;
        Assert.Equal(0f, t.Parent.Raw);
        Assert.Equal(1f, t.LocalScale.X);
    }

    [Fact]
    public void Sprite_DefaultWhiteUnlit_populates()
    {
        var s = Sprite.DefaultWhiteUnlit(9, 8, new Vector2D<float>(3f, 4f));
        Assert.Equal(9, s.AlbedoTextureId);
        Assert.True(s.Visible);
    }

    [Fact]
    public void SpriteRenderSystem_skips_invisible_and_uses_rotation_scale()
    {
        var r = new RecordingRenderer();
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Position>().GetOrAdd(e) = new Position { X = 1f, Y = 1f };
        w.Components<Rotation>().GetOrAdd(e) = new Rotation { Radians = MathF.PI / 4f };
        w.Components<Scale>().GetOrAdd(e) = new Scale { X = 2f, Y = 2f };
        ref var spr = ref w.Components<Sprite>().GetOrAdd(e);
        spr = Sprite.DefaultWhiteUnlit(2, 1, new Vector2D<float>(5f, 5f));
        spr.Visible = false;

        new SpriteRenderSystem(Host(r)).OnParallelUpdate(w, 0f, ParOpts());
        Assert.Empty(r.Sprites);

        spr.Visible = true;
        r.Sprites.Clear();
        new SpriteRenderSystem(Host(r)).OnParallelUpdate(w, 0f, ParOpts());
        Assert.Single(r.Sprites);
        Assert.NotEqual(0f, r.Sprites[0].RotationRadians);
    }

    [Fact]
    public void TilemapRenderSystem_no_renderer_no_store_noop()
    {
        var w = new World();
        var kb = new KeyBindingStore();
        var hNullR = new GameHostServices(kb) { Renderer = null, Tilemaps = new TilemapDataStore() };
        new TilemapRenderSystem(hNullR).OnParallelUpdate(w, 0f, ParOpts());

        var r = new RecordingRenderer();
        var hNullTm = new GameHostServices(kb) { Renderer = r, Tilemaps = null };
        new TilemapRenderSystem(hNullTm).OnParallelUpdate(w, 0f, ParOpts());
    }

    [Fact]
    public void TilemapRenderSystem_skips_empty_tile_index()
    {
        var r = new RecordingRenderer();
        var tm = new TilemapDataStore();
        var w = new World();
        var map = w.CreateEntity();
        tm.Register(map, new[] { 0, 1 }, 1, 2);
        w.Components<Tilemap>().GetOrAdd(map) = new Tilemap
        {
            TileWidth = 10f,
            TileHeight = 10f,
            OriginX = 0f,
            OriginY = 0f,
            AtlasAlbedoTextureId = 2,
            Layer = 0,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1
        };

        new TilemapRenderSystem(Host(r, tm)).OnParallelUpdate(w, 0f, ParOpts());
        Assert.Single(r.Sprites);
    }

    [Fact]
    public void ParticleSimulationSystem_skips_inactive_emitter()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Position>().GetOrAdd(e) = new Position();
        w.Components<ParticleEmitter>().GetOrAdd(e) = new ParticleEmitter { Active = false, MaxParticles = 2 };

        new ParticleSimulationSystem(h).OnParallelUpdate(w, 0.1f, ParOpts());
    }

    [Fact]
    public void ParticleSimulationSystem_removes_expired_with_swap()
    {
        var r = new RecordingRenderer();
        var pt = new ParticleStore();
        var h = Host(r, pt: pt);
        var w = new World();
        var emitter = w.CreateEntity();
        w.Components<Position>().GetOrAdd(emitter) = new Position();
        w.Components<ParticleEmitter>().GetOrAdd(emitter) = new ParticleEmitter
        {
            Active = true,
            MaxParticles = 2,
            ParticleLifeSeconds = 0.08f,
            SpawnIntervalSeconds = 0.01f,
            EmissionVelocity = default,
            GravityY = 0f,
            AlbedoTextureId = 2,
            Layer = 0,
            SortKey = 0f,
            HalfExtent = 2f
        };

        var sim = new ParticleSimulationSystem(h);
        sim.OnParallelUpdate(w, 0.2f, ParOpts());
        sim.OnParallelUpdate(w, 0.2f, ParOpts());
    }

    [Fact]
    public void ParticleRenderSystem_skips_when_no_position_or_empty_bucket()
    {
        var r = new RecordingRenderer();
        var pt = new ParticleStore();
        var h = Host(r, pt: pt);
        var w = new World();

        var noPos = w.CreateEntity();
        w.Components<ParticleEmitter>().GetOrAdd(noPos) = new ParticleEmitter { Active = false, MaxParticles = 2 };

        var emptyBucket = w.CreateEntity();
        w.Components<Position>().GetOrAdd(emptyBucket) = new Position();
        w.Components<ParticleEmitter>().GetOrAdd(emptyBucket) = new ParticleEmitter
        {
            Active = true,
            MaxParticles = 2,
            SpawnIntervalSeconds = 1e6f,
            ParticleLifeSeconds = 1f,
            EmissionVelocity = default,
            GravityY = 0f,
            AlbedoTextureId = 1,
            Layer = 0,
            SortKey = 0f,
            HalfExtent = 1f
        };

        var sim = new ParticleSimulationSystem(h);
        sim.OnParallelUpdate(w, 0.01f, ParOpts());
        new ParticleRenderSystem(h).OnParallelUpdate(w, 0f, ParOpts());
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void ParticleSimulationSystem_inactive_and_render_null_paths()
    {
        var kb = new KeyBindingStore();
        var r = new RecordingRenderer();
        var hNoPt = new GameHostServices(kb) { Renderer = r, Particles = null };
        new ParticleSimulationSystem(hNoPt).OnParallelUpdate(new World(), 0.1f, ParOpts());
        new ParticleRenderSystem(hNoPt).OnParallelUpdate(new World(), 0f, ParOpts());

        var hNoR = new GameHostServices(kb) { Renderer = null, Particles = new ParticleStore() };
        new ParticleRenderSystem(hNoR).OnParallelUpdate(new World(), 0f, ParOpts());
    }

    [Fact]
    public void ParticleSimulationSystem_spawns_until_cap_and_removes_dead()
    {
        var r = new RecordingRenderer();
        var pt = new ParticleStore();
        var h = Host(r, pt: pt);
        var w = new World();
        var emitter = w.CreateEntity();
        w.Components<Position>().GetOrAdd(emitter) = new Position();
        w.Components<ParticleEmitter>().GetOrAdd(emitter) = new ParticleEmitter
        {
            Active = true,
            MaxParticles = 2,
            ParticleLifeSeconds = 5f,
            SpawnIntervalSeconds = 0.01f,
            EmissionVelocity = default,
            GravityY = 0f,
            AlbedoTextureId = 2,
            Layer = 0,
            SortKey = 0f,
            HalfExtent = 2f
        };

        var sim = new ParticleSimulationSystem(h);
        sim.OnParallelUpdate(w, 0.1f, ParOpts());
        Assert.True(pt.TryGetBucket(emitter, out var b) && b!.Count > 0);
        new ParticleRenderSystem(h).OnParallelUpdate(w, 0f, ParOpts());
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void Parallel_scene_systems_noop_when_no_relevant_chunks()
    {
        var w = new World();
        var h = Host(new RecordingRenderer());
        var o = ParOpts();
        new SpriteRenderSystem(h).OnParallelUpdate(w, 0f, o);
        new TilemapRenderSystem(h).OnParallelUpdate(w, 0f, o);
        new ParticleSimulationSystem(h).OnParallelUpdate(w, 0f, o);
        new ParticleRenderSystem(h).OnParallelUpdate(w, 0f, o);
    }

    [Fact]
    public void SpriteAnimationSystem_skips_without_sprite_component()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.Components<SpriteAnimation>().GetOrAdd(e) = new SpriteAnimation
        {
            SecondsPerFrame = 1f,
            FrameCount = 1,
            AtlasColumns = 1,
            Loop = true
        };

        new SpriteAnimationSystem().OnParallelUpdate(w, 1f, new ParallelismSettings().CreateParallelOptions());
    }

    [Fact]
    public void SpriteAnimationSystem_skips_when_animation_config_invalid()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Sprite>().GetOrAdd(e) = Sprite.DefaultWhiteUnlit(1, 1, new Vector2D<float>(4f, 4f));
        w.Components<SpriteAnimation>().GetOrAdd(e) = new SpriteAnimation
        {
            SecondsPerFrame = 1f,
            FrameCount = 0,
            AtlasColumns = 1,
            Loop = true
        };

        new SpriteAnimationSystem().OnParallelUpdate(w, 1f, ParOpts());
    }

    [Fact]
    public void TilemapRenderSystem_skips_when_tile_buffer_not_registered()
    {
        var r = new RecordingRenderer();
        var store = new TilemapDataStore();
        var w = new World();
        var map = w.CreateEntity();
        w.Components<Tilemap>().GetOrAdd(map) = new Tilemap
        {
            TileWidth = 8f,
            TileHeight = 8f,
            OriginX = 0f,
            OriginY = 0f,
            AtlasAlbedoTextureId = 1,
            Layer = 0,
            SortKey = 0f,
            NonEmptyTileMinIndex = 0
        };

        new TilemapRenderSystem(Host(r, store)).OnParallelUpdate(w, 0f, ParOpts());
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TransformHierarchySystem_cycle_eventually_completes()
    {
        var w = new World();
        var a = w.CreateEntity();
        var b = w.CreateEntity();
        ref var ta = ref w.Components<Transform>().GetOrAdd(a);
        ta = Transform.Identity;
        ta.LocalPosition = new Vector2D<float>(1f, 0f);
        ta.Parent = b;
        ref var tb = ref w.Components<Transform>().GetOrAdd(b);
        tb = Transform.Identity;
        tb.LocalPosition = new Vector2D<float>(0f, 1f);
        tb.Parent = a;

        new TransformHierarchySystem().OnParallelUpdate(w, 0f, ParOpts());
        Assert.True(w.Components<Position>().Contains(a));
        Assert.True(w.Components<Position>().Contains(b));
    }
}
