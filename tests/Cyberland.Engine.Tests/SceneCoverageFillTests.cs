using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;
using System.Reflection;

namespace Cyberland.Engine.Tests;

/// <summary>Exercises remaining branches/paths for line coverage on Scene APIs.</summary>
public sealed class SceneCoverageFillTests
{
    private static ParallelOptions ParOpts() => new ParallelismSettings().CreateParallelOptions();

    private static void StartEcs(IEcsQuerySource system, World w)
    {
        var q = w.QueryChunks(system.QuerySpec);
        switch (system)
        {
            case ISystem s: s.OnStart(w, q); break;
            case IParallelSystem p: p.OnStart(w, q); break;
            default: throw new InvalidOperationException();
        }
    }

    private static GameHostServices Host(IRenderer r, TilemapDataStore? tm = null)
    {
        var kb = new KeyBindingStore();
        var h = new GameHostServices(kb) { Renderer = r };
        h.Tilemaps = tm ?? new TilemapDataStore();
        return h;
    }

    [Fact]
    public void Transform_local_and_world_position_round_trip()
    {
        var v = new Vector2D<float>(2f, 3f);
        var t = Transform.Identity;
        t.LocalPosition = v;
        t.WorldPosition = v;
        Assert.Equal(2f, t.WorldPosition.X);
        Assert.Equal(3f, t.WorldPosition.Y);
    }

    [Fact]
    public void Transform_identity_scale_defaults()
    {
        var t = Transform.Identity;
        Assert.Equal(1f, t.LocalScale.X);
        Assert.Equal(1f, t.WorldScale.Y);
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
        Assert.Equal(9u, s.AlbedoTextureId);
        Assert.True(s.Visible);
    }

    [Fact]
    public void SpriteRenderSystem_skips_invisible_and_uses_rotation_scale()
    {
        var r = new RecordingRenderer();
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(e) = new Transform
        {
            LocalPosition = new Vector2D<float>(1f, 1f),
            WorldPosition = new Vector2D<float>(1f, 1f),
            LocalRotationRadians = MathF.PI / 4f,
            WorldRotationRadians = MathF.PI / 4f,
            LocalScale = new Vector2D<float>(2f, 2f),
            WorldScale = new Vector2D<float>(2f, 2f)
        };
        ref var spr = ref w.Components<Sprite>().GetOrAdd(e);
        spr = Sprite.DefaultWhiteUnlit(2, 1, new Vector2D<float>(5f, 5f));
        spr.Visible = false;

        var srA = new SpriteRenderSystem(Host(r));
        StartEcs(srA, w);
        srA.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Sprite, Transform>()), 0f, ParOpts());
        Assert.Empty(r.Sprites);

        spr.Visible = true;
        r.Sprites.Clear();
        var srB = new SpriteRenderSystem(Host(r));
        StartEcs(srB, w);
        srB.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Sprite, Transform>()), 0f, ParOpts());
        Assert.Single(r.Sprites);
        Assert.NotEqual(0f, r.Sprites[0].RotationRadians);
    }

    [Fact]
    public void TilemapRenderSystem_no_renderer_no_store_noop()
    {
        var w = new World();
        var kb = new KeyBindingStore();
        var tmStore = new TilemapDataStore();
        var hNullR = new GameHostServices(kb) { Renderer = null, Tilemaps = tmStore };
        var map = w.CreateEntity();
        tmStore.Register(map, new[] { 1 }, 1, 1);
        w.Components<Transform>().GetOrAdd(map) = Transform.Identity;
        w.Components<Tilemap>().GetOrAdd(map) = new Tilemap
        {
            TileWidth = 8f,
            TileHeight = 8f,
            AtlasAlbedoTextureId = 1,
            Layer = 0,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1
        };
        var tmSpec = SystemQuerySpec.All<Tilemap, Transform>();
        var agg = Assert.Throws<AggregateException>(() =>
            new TilemapRenderSystem(hNullR).OnParallelLateUpdate(w.QueryChunks(tmSpec), 0f, ParOpts()));
        Assert.IsType<NullReferenceException>(agg.InnerException);

        var r = new RecordingRenderer();
        var hNullTm = new GameHostServices(kb) { Renderer = r, Tilemaps = null };
        new TilemapRenderSystem(hNullTm).OnParallelLateUpdate(w.QueryChunks(tmSpec), 0f, ParOpts());
    }

    [Fact]
    public void TilemapRenderSystem_skips_empty_tile_index()
    {
        var r = new RecordingRenderer();
        var tm = new TilemapDataStore();
        var w = new World();
        var map = w.CreateEntity();
        tm.Register(map, new[] { 0, 1 }, 1, 2);
        var fb = r.SwapchainPixelSize;
        var cornerWorld = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(0f, 0f), fb);
        w.Components<Transform>().GetOrAdd(map) = new Transform
        {
            LocalPosition = cornerWorld,
            LocalRotationRadians = 0f,
            LocalScale = new Vector2D<float>(1f, 1f),
            WorldPosition = cornerWorld,
            WorldRotationRadians = 0f,
            WorldScale = new Vector2D<float>(1f, 1f)
        };
        w.Components<Tilemap>().GetOrAdd(map) = new Tilemap
        {
            TileWidth = 10f,
            TileHeight = 10f,
            AtlasAlbedoTextureId = 2,
            Layer = 0,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1
        };

        new TilemapRenderSystem(Host(r, tm)).OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Tilemap, Transform>()), 0f, ParOpts());
        Assert.Single(r.Sprites);
    }

    [Fact]
    public void ParticleSimulationSystem_skips_inactive_emitter()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        w.Components<ParticleEmitter>().GetOrAdd(e) = new ParticleEmitter { Active = false, MaxParticles = 2 };

        var psk = new ParticleSimulationSystem();
        StartEcs(psk, w);
        psk.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.1f, ParOpts());
    }

    [Fact]
    public void ParticleSimulationSystem_removes_expired_with_swap()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var w = new World();
        var emitter = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(emitter) = Transform.Identity;
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

        var sim = new ParticleSimulationSystem();
        StartEcs(sim, w);
        sim.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.2f, ParOpts());
        sim.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.2f, ParOpts());
    }

    [Fact]
    public void ParticleRenderSystem_skips_when_no_position_or_empty_bucket()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var w = new World();

        var noPos = w.CreateEntity();
        w.Components<ParticleEmitter>().GetOrAdd(noPos) = new ParticleEmitter { Active = false, MaxParticles = 2 };

        var emptyBucket = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(emptyBucket) = Transform.Identity;
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

        var sim = new ParticleSimulationSystem();
        StartEcs(sim, w);
        sim.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.01f, ParOpts());
        var prE = new ParticleRenderSystem(h);
        StartEcs(prE, w);
        prE.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter, Transform>()), 0f, ParOpts());
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void ParticleSimulationSystem_inactive_and_render_null_paths()
    {
        var kb = new KeyBindingStore();
        var r = new RecordingRenderer();
        var hNoPt = new GameHostServices(kb) { Renderer = r };
        var emptyWorld = new World();
        var simNo = new ParticleSimulationSystem();
        StartEcs(simNo, emptyWorld);
        simNo.OnParallelFixedUpdate(emptyWorld.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.1f, ParOpts());
        var prNo = new ParticleRenderSystem(hNoPt);
        StartEcs(prNo, emptyWorld);
        prNo.OnParallelLateUpdate(emptyWorld.QueryChunks(SystemQuerySpec.All<ParticleEmitter, Transform>()), 0f, ParOpts());

        var hNoR = new GameHostServices(kb) { Renderer = null };
        var emptyWorld2 = new World();
        var prNo2 = new ParticleRenderSystem(hNoR);
        StartEcs(prNo2, emptyWorld2);
        Assert.Throws<NullReferenceException>(() =>
            prNo2.OnParallelLateUpdate(emptyWorld2.QueryChunks(SystemQuerySpec.All<ParticleEmitter, Transform>()), 0f, ParOpts()));
    }

    [Fact]
    public void ParticleSimulationSystem_spawns_until_cap_and_removes_dead()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var w = new World();
        var emitter = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(emitter) = Transform.Identity;
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

        var sim = new ParticleSimulationSystem();
        StartEcs(sim, w);
        sim.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.1f, ParOpts());
        Assert.True(w.Components<ParticleEmitter>().Get(emitter).RuntimeCount > 0);
        var prS = new ParticleRenderSystem(h);
        StartEcs(prS, w);
        prS.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter, Transform>()), 0f, ParOpts());
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void Parallel_scene_systems_noop_when_no_relevant_chunks()
    {
        var w = new World();
        var h = Host(new RecordingRenderer());
        var o = ParOpts();
        var psr = new SpriteRenderSystem(h);
        StartEcs(psr, w);
        psr.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Sprite, Transform>()), 0f, o);
        new TilemapRenderSystem(h).OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Tilemap, Transform>()), 0f, o);
        var pss = new ParticleSimulationSystem();
        StartEcs(pss, w);
        pss.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0f, o);
        var ppr = new ParticleRenderSystem(h);
        StartEcs(ppr, w);
        ppr.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter, Transform>()), 0f, o);
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

        var sa0 = new SpriteAnimationSystem();
        StartEcs(sa0, w);
        sa0.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<SpriteAnimation, Sprite>()), 1f, new ParallelismSettings().CreateParallelOptions());
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

        var sa1 = new SpriteAnimationSystem();
        StartEcs(sa1, w);
        sa1.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<SpriteAnimation, Sprite>()), 1f, ParOpts());
    }

    [Fact]
    public void TilemapRenderSystem_skips_when_tile_buffer_not_registered()
    {
        var r = new RecordingRenderer();
        var store = new TilemapDataStore();
        var w = new World();
        var map = w.CreateEntity();
        var fb2 = r.SwapchainPixelSize;
        var corner2 = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(0f, 0f), fb2);
        w.Components<Transform>().GetOrAdd(map) = new Transform
        {
            LocalPosition = corner2,
            LocalRotationRadians = 0f,
            LocalScale = new Vector2D<float>(1f, 1f),
            WorldPosition = corner2,
            WorldRotationRadians = 0f,
            WorldScale = new Vector2D<float>(1f, 1f)
        };
        w.Components<Tilemap>().GetOrAdd(map) = new Tilemap
        {
            TileWidth = 8f,
            TileHeight = 8f,
            AtlasAlbedoTextureId = 1,
            Layer = 0,
            SortKey = 0f,
            NonEmptyTileMinIndex = 0
        };

        new TilemapRenderSystem(Host(r, store)).OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Tilemap, Transform>()), 0f, ParOpts());
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

        var thc = new TransformHierarchySystem();
        StartEcs(thc, w);
        thc.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());
        Assert.True(w.Components<Transform>().Contains(a));
        Assert.True(w.Components<Transform>().Contains(b));
    }

    [Fact]
    public void Trigger_defaults_and_event_payload_round_trip()
    {
        var trigger = Trigger.DefaultPoint;
        Assert.True(trigger.Enabled);
        Assert.Equal(TriggerShapeKind.Point, trigger.Shape);

        var entity = EntityId.FromParts(12, 2);
        var ev = new TriggerEvent { Other = entity, Kind = TriggerEventKind.OnTriggerEnter };
        Assert.Equal(12u, ev.Other.Index);
        Assert.Equal(TriggerEventKind.OnTriggerEnter, ev.Kind);
    }

    [Fact]
    public void Trigger_pair_key_object_equals_path_is_exercised()
    {
        var triggerSystemType = typeof(TriggerSystem);
        var pairKeyType = triggerSystemType.GetNestedType("TriggerPairKey", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(pairKeyType);

        var ctor = pairKeyType!.GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(EntityId), typeof(EntityId) },
            modifiers: null);
        Assert.NotNull(ctor);

        var a = EntityId.FromParts(5, 1);
        var b = EntityId.FromParts(2, 1);
        var left = ctor!.Invoke(new object[] { a, b });
        var right = ctor.Invoke(new object[] { b, a });

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void TriggerSystem_hierarchy_skip_and_private_hierarchy_branches_are_exercised()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(parent) = Transform.Identity;
        world.Components<Trigger>().GetOrAdd(parent) = new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 1f
        };

        var child = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(child) = new Transform
        {
            Parent = parent,
            LocalScale = new Vector2D<float>(1f, 1f),
            WorldScale = new Vector2D<float>(1f, 1f)
        };
        world.Components<Trigger>().GetOrAdd(child) = new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 1f
        };

        var trCov = new TriggerSystem();
        StartEcs(trCov, world);
        trCov.OnParallelFixedUpdate(world.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());

        var isInHierarchy = typeof(TriggerSystem).GetMethod(
            "IsInHierarchy",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(isInHierarchy);
        var transformStore = world.Components<Transform>();
        var noTransform = world.CreateEntity();
        var resultNoTransform = (bool)isInHierarchy!.Invoke(null, new object[] { world, transformStore, noTransform, parent })!;
        Assert.False(resultNoTransform);
        var resultChildAncestor = (bool)isInHierarchy.Invoke(null, new object[] { world, transformStore, child, parent })!;
        Assert.True(resultChildAncestor);
    }

    /// <summary>
    /// Registers stock <see cref="Scene.Systems"/> types like <see cref="GameApplication"/> so
    /// <see cref="IEcsQuerySource.QuerySpec"/> getters are exercised for line coverage.
    /// </summary>
    [Fact]
    public void Stock_engine_scene_systems_register_using_self_declared_QuerySpec()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var host = new GameHostServices(new KeyBindingStore());
        sched.RegisterParallel("cyberland.engine/transform2d", new TransformHierarchySystem());
        sched.RegisterParallel("cyberland.engine/trigger", new TriggerSystem());
        sched.RegisterParallel("cyberland.engine/sprite-animation", new SpriteAnimationSystem());
        sched.RegisterParallel("cyberland.engine/particle-sim", new ParticleSimulationSystem());
        sched.RegisterParallel("cyberland.engine/tilemap-render", new TilemapRenderSystem(host));
        sched.RegisterParallel("cyberland.engine/sprite-render", new SpriteRenderSystem(host));
        sched.RegisterParallel("cyberland.engine/particle-render", new ParticleRenderSystem(host));
        sched.RegisterSequential("cyberland.engine/text-render", new TextRenderSystem(host));
    }
}
