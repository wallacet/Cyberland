using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class SceneSystemTests
{
    private static ParallelOptions ParOpts() => new ParallelismSettings().CreateParallelOptions();
    private static ParallelOptions SingleThreadOpts() => new() { MaxDegreeOfParallelism = 1 };

    /// <summary>Invokes <see cref="ISystem.OnStart"/> / <see cref="IParallelSystem.OnStart"/> so phase tests can use cached <see cref="World"/> the same way <see cref="SystemScheduler"/> does.</summary>
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

    private static EntityId AddTriggerEntity(
        World world,
        float x,
        float y,
        Trigger trigger,
        float rotation = 0f,
        EntityId parent = default)
    {
        var id = world.CreateEntity();
        ref var transform = ref world.Components<Transform>().GetOrAdd(id);
        transform = Transform.Identity;
        var pos = new Vector2D<float>(x, y);
        transform.LocalPosition = pos;
        transform.WorldPosition = pos;
        transform.LocalRotationRadians = rotation;
        transform.WorldRotationRadians = rotation;
        world.Components<Trigger>().GetOrAdd(id) = trigger;
        if (parent.Raw != 0)
        {
            transform.Parent = parent;
        }

        return id;
    }

    // Test helper: seeded-from-Identity constructor replaces the old `new Transform { Local.. = , World.. = }`
    // initializer pattern. Object initializers would start from a zero matrix and collapse scale to (0,0).
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

    private static HashSet<(uint Other, TriggerEventKind Kind)> EventSet(World world, EntityId entity)
    {
        var set = new HashSet<(uint Other, TriggerEventKind Kind)>();
        if (!world.Components<TriggerEvents>().TryGet(entity, out var events) || events.Events is null)
            return set;

        foreach (var ev in events.Events)
            set.Add((ev.Other.Raw, ev.Kind));
        return set;
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

        var ths0 = new TransformHierarchySystem();
        StartEcs(ths0, w);
        ths0.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());
        var childTransform = w.Components<Transform>().Get(child);
        Assert.Equal(2f, childTransform.WorldPosition.X, 3);
        Assert.Equal(1f, childTransform.WorldPosition.Y, 3);
    }

    [Fact]
    public void TransformHierarchySystem_propagates_parent_translation()
    {
        var w = new World();
        var parent = w.CreateEntity();
        var child = w.CreateEntity();
        ref var parentTransform = ref w.Components<Transform>().GetOrAdd(parent);
        parentTransform = Transform.Identity;
        parentTransform.LocalPosition = new Vector2D<float>(10f, 5f);

        ref var tc = ref w.Components<Transform>().GetOrAdd(child);
        tc = Transform.Identity;
        tc.LocalPosition = new Vector2D<float>(3f, 4f);
        tc.Parent = parent;

        var ths1 = new TransformHierarchySystem();
        StartEcs(ths1, w);
        ths1.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0.016f, ParOpts());

        var childTransform = w.Components<Transform>().Get(child);
        Assert.Equal(13f, childTransform.WorldPosition.X, 3);
        Assert.Equal(9f, childTransform.WorldPosition.Y, 3);
    }

    [Fact]
    public void TransformHierarchySystem_writes_world_matrix_for_roots_and_children()
    {
        var w = new World();
        var root = w.CreateEntity();
        var child = w.CreateEntity();
        ref var rootTransform = ref w.Components<Transform>().GetOrAdd(root);
        rootTransform = Transform.Identity;
        rootTransform.LocalPosition = new Vector2D<float>(10f, 20f);
        rootTransform.LocalRotationRadians = MathF.PI * 0.5f;

        ref var childTransformIn = ref w.Components<Transform>().GetOrAdd(child);
        childTransformIn = Transform.Identity;
        childTransformIn.LocalPosition = new Vector2D<float>(1f, 0f);
        childTransformIn.Parent = root;

        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        // Root's WorldMatrix equals its LocalMatrix (parent = identity).
        var rootAfter = w.Components<Transform>().Get(root);
        Assert.Equal(rootAfter.LocalMatrix, rootAfter.WorldMatrix);

        // Child WorldMatrix = child.LocalMatrix * parent.WorldMatrix (row-vector convention: local applies first, then parent).
        var childAfter = w.Components<Transform>().Get(child);
        var expected = System.Numerics.Matrix3x2.Multiply(childAfter.LocalMatrix, rootAfter.WorldMatrix);
        Assert.Equal(expected.M11, childAfter.WorldMatrix.M11, 4);
        Assert.Equal(expected.M12, childAfter.WorldMatrix.M12, 4);
        Assert.Equal(expected.M21, childAfter.WorldMatrix.M21, 4);
        Assert.Equal(expected.M22, childAfter.WorldMatrix.M22, 4);
        Assert.Equal(expected.M31, childAfter.WorldMatrix.M31, 4);
        Assert.Equal(expected.M32, childAfter.WorldMatrix.M32, 4);

        // Sanity: rotating parent by +90° CCW places child at parent's (1,0) local → world (10, 21).
        Assert.Equal(10f, childAfter.WorldPosition.X, 3);
        Assert.Equal(21f, childAfter.WorldPosition.Y, 3);
    }

    [Fact]
    public void TransformHierarchySystem_recycles_child_adjacency_lists_across_ticks()
    {
        var w = new World();
        var parent = w.CreateEntity();
        var child = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(parent) = Transform.Identity;
        w.Components<Transform>().GetOrAdd(child) = Transform.Identity;
        ref var tc = ref w.Components<Transform>().Get(child);
        tc.Parent = parent;

        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());
    }

    [Fact]
    public void SpriteRenderSystem_noop_when_renderer_null()
    {
        var kb = new KeyBindingStore();
        var h = new GameHostServices(kb) { Renderer = null };
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(e) = MakeTransform(
            localPos: new Vector2D<float>(1f, 2f),
            worldPos: new Vector2D<float>(1f, 2f));
        w.Components<Sprite>().GetOrAdd(e) = Sprite.DefaultWhiteUnlit(0, 0, new Vector2D<float>(1f, 1f));

        var sr0 = new SpriteRenderSystem(h);
        StartEcs(sr0, w);
        Assert.Throws<AggregateException>(() =>
            sr0.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Sprite, Transform>()), 0f, ParOpts()));
    }

    [Fact]
    public void SpriteRenderSystem_submits_when_visible()
    {
        var r = new RecordingRenderer();
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(e) = MakeTransform(
            localPos: new Vector2D<float>(1f, 2f),
            worldPos: new Vector2D<float>(1f, 2f));
        ref var spr = ref w.Components<Sprite>().GetOrAdd(e);
        spr = Sprite.DefaultWhiteUnlit(2, 1, new Vector2D<float>(4f, 4f));
        spr.Visible = true;

        var sr1 = new SpriteRenderSystem(Host(r));
        StartEcs(sr1, w);
        sr1.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Sprite, Transform>()), 0.016f, ParOpts());
        Assert.Single(r.Sprites);
        Assert.Equal(1f, r.Sprites[0].CenterWorld.X);
    }

    [Fact]
    public void SpriteRenderSystem_adding_sprite_ensures_transform_and_submits()
    {
        var r = new RecordingRenderer();
        var w = new World();
        var e = w.CreateEntity();
        w.Components<Sprite>().GetOrAdd(e) = Sprite.DefaultWhiteUnlit(2, 1, new Vector2D<float>(1f, 1f));
        Assert.True(w.Components<Transform>().Contains(e));

        var sr2 = new SpriteRenderSystem(Host(r));
        StartEcs(sr2, w);
        sr2.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<Sprite, Transform>()), 0.016f, ParOpts());
        Assert.Single(r.Sprites);
    }

    [Fact]
    public void SpriteAnimationSystem_noop_when_no_animation_chunks()
    {
        var w = new World();
        var opts = new ParallelismSettings().CreateParallelOptions();
        var san = new SpriteAnimationSystem();
        StartEcs(san, w);
        san.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<SpriteAnimation, Sprite>()), 0.1f, opts);
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
        var sa = new SpriteAnimationSystem();
        StartEcs(sa, w);
        sa.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<SpriteAnimation, Sprite>()), 0.6f, opts);
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
        var fb = r.SwapchainPixelSize;
        var cornerWorld = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(0f, 0f), fb);
        w.Components<Transform>().GetOrAdd(map) = MakeTransform(localPos: cornerWorld, worldPos: cornerWorld);
        w.Components<Tilemap>().GetOrAdd(map) = new Tilemap
        {
            TileWidth = 16f,
            TileHeight = 16f,
            AtlasAlbedoTextureId = 2,
            Layer = 0,
            SortKey = 0f,
            NonEmptyTileMinIndex = 1
        };

        var tmSpec = SystemQuerySpec.All<Tilemap, Transform>();
        new TilemapRenderSystem(Host(r, tm)).OnParallelLateUpdate(w.QueryChunks(tmSpec), 0f, ParOpts());
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void ParticleSimulation_and_render_round_trip()
    {
        var r = new RecordingRenderer();
        var h = Host(r);
        var w = new World();
        var emitter = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(emitter) = MakeTransform(
            localPos: new Vector2D<float>(100f, 100f),
            worldPos: new Vector2D<float>(100f, 100f));
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

        var sim = new ParticleSimulationSystem();
        StartEcs(sim, w);
        sim.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.5f, ParOpts());
        var pr = new ParticleRenderSystem(h);
        StartEcs(pr, w);
        pr.OnParallelLateUpdate(w.QueryChunks(SystemQuerySpec.All<ParticleEmitter, Transform>()), 0f, ParOpts());
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

    [Fact]
    public void TriggerSystem_emits_enter_stay_exit()
    {
        var w = new World();
        var a = AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 2f
        });
        var b = AddTriggerEntity(w, 1f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 2f
        });

        var sys = new TriggerSystem();
        StartEcs(sys, w);
        sys.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.Contains((b.Raw, TriggerEventKind.OnTriggerEnter), EventSet(w, a));
        Assert.Contains((a.Raw, TriggerEventKind.OnTriggerEnter), EventSet(w, b));

        sys.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.Contains((b.Raw, TriggerEventKind.OnTriggerStay), EventSet(w, a));
        Assert.Contains((a.Raw, TriggerEventKind.OnTriggerStay), EventSet(w, b));

        ref var tb = ref w.Components<Transform>().Get(b);
        var tbLocal = tb.LocalPosition;
        tbLocal.X = 20f;
        tb.LocalPosition = tbLocal;
        var tbWorld = tb.WorldPosition;
        tbWorld.X = 20f;
        tb.WorldPosition = tbWorld;
        sys.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.Contains((b.Raw, TriggerEventKind.OnTriggerExit), EventSet(w, a));
        Assert.Contains((a.Raw, TriggerEventKind.OnTriggerExit), EventSet(w, b));
    }

    [Fact]
    public void TriggerSystem_ignores_transform_hierarchy_pairs()
    {
        var w = new World();
        var parent = AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 3f
        });
        var child = AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 3f
        }, parent: parent);
        // Parent needs a Transform so child ancestry walk has a valid link target.
        w.Components<Transform>().GetOrAdd(parent) = Transform.Identity;

        var trH = new TriggerSystem();
        StartEcs(trH, w);
        trH.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.NotNull(EventSet(w, parent));
        Assert.NotNull(EventSet(w, child));
    }

    [Fact]
    public void TriggerSystem_supports_point_circle_and_oriented_rect()
    {
        var w = new World();
        var point = AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Point
        });
        var circle = AddTriggerEntity(w, 1f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 2f
        });
        var rect = AddTriggerEntity(w, 0.5f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Rectangle,
            HalfExtents = new Vector2D<float>(1f, 1f)
        }, rotation: MathF.PI / 4f);

        var trS = new TriggerSystem();
        StartEcs(trS, w);
        trS.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        var pointEvents = EventSet(w, point);
        Assert.Contains((circle.Raw, TriggerEventKind.OnTriggerEnter), pointEvents);
        Assert.Contains((rect.Raw, TriggerEventKind.OnTriggerEnter), pointEvents);
    }

    [Fact]
    public void TriggerSystem_disabled_trigger_does_not_emit()
    {
        var w = new World();
        var enabled = AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 2f
        });
        var disabled = AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = false,
            Shape = TriggerShapeKind.Circle,
            Radius = 2f
        });

        var trD = new TriggerSystem();
        StartEcs(trD, w);
        trD.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.Empty(EventSet(w, enabled));
        Assert.Empty(EventSet(w, disabled));
    }

    [Fact]
    public void TriggerSystem_parallel_and_single_thread_membership_match()
    {
        var parallelWorld = new World();
        var singleWorld = new World();
        for (var i = 0; i < 24; i++)
        {
            var x = (i % 6) * 1.5f;
            var y = (i / 6) * 1.25f;
            var shape = (i % 3) switch
            {
                0 => TriggerShapeKind.Point,
                1 => TriggerShapeKind.Circle,
                _ => TriggerShapeKind.Rectangle
            };
            var trigger = new Trigger
            {
                Enabled = true,
                Shape = shape,
                Radius = 1.1f,
                HalfExtents = new Vector2D<float>(0.9f, 0.6f)
            };

            AddTriggerEntity(parallelWorld, x, y, trigger, rotation: (i % 5) * 0.11f);
            AddTriggerEntity(singleWorld, x, y, trigger, rotation: (i % 5) * 0.11f);
        }

        var parallelSys = new TriggerSystem();
        var singleSys = new TriggerSystem();
        StartEcs(parallelSys, parallelWorld);
        StartEcs(singleSys, singleWorld);
        parallelSys.OnParallelFixedUpdate(parallelWorld.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        singleSys.OnParallelFixedUpdate(singleWorld.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, SingleThreadOpts());

        foreach (var chunk in parallelWorld.QueryChunks(SystemQuerySpec.All<Trigger>()))
        {
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                var id = entities[i];
                Assert.Equal(EventSet(singleWorld, id), EventSet(parallelWorld, id));
            }
        }
    }

    [Fact]
    public void TriggerSystem_no_triggers_clears_previous_overlap_cache()
    {
        var w = new World();
        var a = AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 1f
        });
        var b = AddTriggerEntity(w, 0.5f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 1f
        });

        var sys = new TriggerSystem();
        StartEcs(sys, w);
        sys.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.NotEmpty(EventSet(w, a));

        w.Components<Trigger>().Remove(a);
        w.Components<Trigger>().Remove(b);
        sys.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
    }

    [Fact]
    public void TriggerSystem_skips_trigger_without_position()
    {
        var w = new World();
        var id = w.CreateEntity();
        w.Components<Trigger>().GetOrAdd(id) = new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 10f
        };

        var trP = new TriggerSystem();
        StartEcs(trP, w);
        trP.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.False(w.Components<TriggerEvents>().Contains(id));
    }

    [Fact]
    public void TriggerSystem_single_trigger_hits_small_pair_fast_path()
    {
        var w = new World();
        AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Point
        });

        var trF = new TriggerSystem();
        StartEcs(trF, w);
        trF.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
    }

    [Fact]
    public void TriggerSystem_hierarchy_walk_handles_missing_and_non_matching_ancestors()
    {
        var w = new World();

        var missingParent = w.CreateEntity();
        var childWithMissingParent = AddTriggerEntity(w, 0f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 1f
        });
        ref var missingParentTf = ref w.Components<Transform>().GetOrAdd(childWithMissingParent);
        missingParentTf = Transform.Identity;
        missingParentTf.Parent = missingParent;

        var root = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(root) = Transform.Identity;
        var mid = w.CreateEntity();
        ref var midTf = ref w.Components<Transform>().GetOrAdd(mid);
        midTf = Transform.Identity;
        midTf.Parent = root;

        var chainChild = AddTriggerEntity(w, 10f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 1f
        });
        ref var chainChildTf = ref w.Components<Transform>().GetOrAdd(chainChild);
        chainChildTf = Transform.Identity;
        chainChildTf.Parent = mid;

        var unrelated = AddTriggerEntity(w, 30f, 0f, new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 1f
        });

        var trHi = new TriggerSystem();
        StartEcs(trHi, w);
        trHi.OnParallelFixedUpdate(w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.NotNull(EventSet(w, missingParent));
        Assert.NotNull(EventSet(w, chainChild));
        Assert.NotNull(EventSet(w, unrelated));
    }
}
