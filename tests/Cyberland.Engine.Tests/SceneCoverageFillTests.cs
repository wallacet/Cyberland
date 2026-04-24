using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
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
        var h = new GameHostServices() { Renderer = r };
        h.Tilemaps = tm ?? new TilemapDataStore();
        return h;
    }

    // Seeded-from-Identity helper: `new Transform { LocalPosition = ... }` would start from a zero matrix, which
    // decomposes to zero scale and collapses the transform. Tests use this factory instead.
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
    public void Transform_identity_has_identity_matrices_and_cached_prs()
    {
        var t = Transform.Identity;
        Assert.Equal(System.Numerics.Matrix3x2.Identity, t.LocalMatrix);
        Assert.Equal(System.Numerics.Matrix3x2.Identity, t.WorldMatrix);
        Assert.Equal(0f, t.LocalPosition.X);
        Assert.Equal(0f, t.LocalPosition.Y);
        Assert.Equal(0f, t.LocalRotationRadians);
        Assert.Equal(0f, t.WorldRotationRadians);
        Assert.Equal(1f, t.LocalScale.X);
        Assert.Equal(1f, t.LocalScale.Y);
        Assert.Equal(1f, t.WorldScale.X);
        Assert.Equal(1f, t.WorldScale.Y);
    }

    [Fact]
    public void Transform_prs_setters_update_matrix_components()
    {
        var t = Transform.Identity;
        t.LocalPosition = new Vector2D<float>(5f, -3f);
        Assert.Equal(5f, t.LocalMatrix.M31, 4);
        Assert.Equal(-3f, t.LocalMatrix.M32, 4);

        t.LocalScale = new Vector2D<float>(2f, 3f);
        // Matrix column lengths reflect scale (column 0 = M11,M21; column 1 = M12,M22).
        var col0 = MathF.Sqrt(t.LocalMatrix.M11 * t.LocalMatrix.M11 + t.LocalMatrix.M21 * t.LocalMatrix.M21);
        var col1 = MathF.Sqrt(t.LocalMatrix.M12 * t.LocalMatrix.M12 + t.LocalMatrix.M22 * t.LocalMatrix.M22);
        Assert.Equal(2f, col0, 4);
        Assert.Equal(3f, col1, 4);

        t.LocalRotationRadians = MathF.PI * 0.5f;
        Assert.Equal(MathF.PI * 0.5f, t.LocalRotationRadians, 4);
        // Position and scale survive a rotation-only change (same PRS convention applies end to end).
        Assert.Equal(5f, t.LocalPosition.X, 3);
        Assert.Equal(-3f, t.LocalPosition.Y, 3);
        Assert.Equal(2f, t.LocalScale.X, 3);
        Assert.Equal(3f, t.LocalScale.Y, 3);
    }

    [Fact]
    public void Transform_world_prs_setters_rebuild_world_matrix()
    {
        var t = Transform.Identity;
        t.WorldPosition = new Vector2D<float>(7f, 11f);
        t.WorldRotationRadians = MathF.PI * 0.25f;
        t.WorldScale = new Vector2D<float>(4f, 5f);

        TransformMath.DecomposeToPRS(t.WorldMatrix, out var pos, out var rad, out var scale);
        Assert.Equal(7f, pos.X, 3);
        Assert.Equal(11f, pos.Y, 3);
        Assert.Equal(MathF.PI * 0.25f, rad, 3);
        Assert.Equal(4f, scale.X, 3);
        Assert.Equal(5f, scale.Y, 3);
    }

    [Fact]
    public void Transform_direct_matrix_mutation_invalidates_prs_cache()
    {
        var t = Transform.Identity;
        t.LocalPosition = new Vector2D<float>(1f, 2f);
        // Cache was seeded by the setter: reading the property again hits the valid cache path.
        Assert.Equal(1f, t.LocalPosition.X);

        // Direct mutation of the matrix bypasses setters; next read should re-decompose rather than return stale data.
        t.LocalMatrix = TransformMath.MatrixFromPositionRotationScale(
            new Vector2D<float>(9f, 8f),
            0f,
            new Vector2D<float>(1f, 1f));
        Assert.Equal(9f, t.LocalPosition.X, 3);
        Assert.Equal(8f, t.LocalPosition.Y, 3);

        t.WorldMatrix = TransformMath.MatrixFromPositionRotationScale(
            new Vector2D<float>(-1f, -2f),
            0f,
            new Vector2D<float>(1f, 1f));
        Assert.Equal(-1f, t.WorldPosition.X, 3);
        Assert.Equal(-2f, t.WorldPosition.Y, 3);
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
        w.Components<Transform>().GetOrAdd(e) = MakeTransform(
            localPos: new Vector2D<float>(1f, 1f),
            worldPos: new Vector2D<float>(1f, 1f),
            localRotation: MathF.PI / 4f,
            worldRotation: MathF.PI / 4f,
            localScale: new Vector2D<float>(2f, 2f),
            worldScale: new Vector2D<float>(2f, 2f));
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
        var tmStore = new TilemapDataStore();
        var hNullR = new GameHostServices() { Renderer = null, Tilemaps = tmStore };
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
        var hNullTm = new GameHostServices() { Renderer = r, Tilemaps = null };
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
        w.Components<Transform>().GetOrAdd(map) = MakeTransform(localPos: cornerWorld, worldPos: cornerWorld);
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
        var r = new RecordingRenderer();
        var hNoPt = new GameHostServices() { Renderer = r };
        var emptyWorld = new World();
        var simNo = new ParticleSimulationSystem();
        StartEcs(simNo, emptyWorld);
        simNo.OnParallelFixedUpdate(emptyWorld.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.1f, ParOpts());
        var prNo = new ParticleRenderSystem(hNoPt);
        StartEcs(prNo, emptyWorld);
        prNo.OnParallelLateUpdate(emptyWorld.QueryChunks(SystemQuerySpec.All<ParticleEmitter, Transform>()), 0f, ParOpts());

        var hNoR = new GameHostServices() { Renderer = null };
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
        w.Components<Transform>().GetOrAdd(map) = MakeTransform(localPos: corner2, worldPos: corner2);
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
        ref var childTransform = ref world.Components<Transform>().GetOrAdd(child);
        childTransform = Transform.Identity;
        childTransform.Parent = parent;
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
        var host = new GameHostServices();
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
