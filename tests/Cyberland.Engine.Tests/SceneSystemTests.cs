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

    private static GameHostServices Host(IRenderer r, TilemapDataStore? tm = null, ParticleStore? pt = null)
    {
        var kb = new KeyBindingStore();
        var h = new GameHostServices(kb) { Renderer = r };
        h.Tilemaps = tm ?? new TilemapDataStore();
        h.Particles = pt ?? new ParticleStore();
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
        world.Components<Position>().GetOrAdd(id) = new Position { X = x, Y = y };
        world.Components<Trigger>().GetOrAdd(id) = trigger;
        if (rotation != 0f)
            world.Components<Rotation>().GetOrAdd(id) = new Rotation { Radians = rotation };
        if (parent.Raw != 0)
        {
            ref var t = ref world.Components<Transform>().GetOrAdd(id);
            t = Transform.Identity;
            t.Parent = parent;
        }

        return id;
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

        new TransformHierarchySystem().OnParallelEarlyUpdate(w, w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());
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

        new TransformHierarchySystem().OnParallelEarlyUpdate(w, w.QueryChunks(SystemQuerySpec.All<Transform>()), 0.016f, ParOpts());

        var wp = w.Components<Position>().Get(child);
        Assert.Equal(13f, wp.X, 3);
        Assert.Equal(9f, wp.Y, 3);
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
        sys.OnParallelEarlyUpdate(w, w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());
        sys.OnParallelEarlyUpdate(w, w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());
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

        new SpriteRenderSystem(h).OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<Sprite>()), 0f, ParOpts());
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

        new SpriteRenderSystem(Host(r)).OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<Sprite>()), 0.016f, ParOpts());
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

        new SpriteRenderSystem(Host(r)).OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<Sprite>()), 0.016f, ParOpts());
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void SpriteAnimationSystem_noop_when_no_animation_chunks()
    {
        var w = new World();
        var opts = new ParallelismSettings().CreateParallelOptions();
        new SpriteAnimationSystem().OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<SpriteAnimation>()), 0.1f, opts);
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
        new SpriteAnimationSystem().OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<SpriteAnimation>()), 0.6f, opts);
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

        new TilemapRenderSystem(Host(r, tm)).OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<Tilemap>()), 0f, ParOpts());
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
        sim.OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0.5f, ParOpts());
        new ParticleRenderSystem(h).OnParallelLateUpdate(w, w.QueryChunks(SystemQuerySpec.All<ParticleEmitter>()), 0f, ParOpts());
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
        sys.OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.Contains((b.Raw, TriggerEventKind.OnTriggerEnter), EventSet(w, a));
        Assert.Contains((a.Raw, TriggerEventKind.OnTriggerEnter), EventSet(w, b));

        sys.OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.Contains((b.Raw, TriggerEventKind.OnTriggerStay), EventSet(w, a));
        Assert.Contains((a.Raw, TriggerEventKind.OnTriggerStay), EventSet(w, b));

        ref var pb = ref w.Components<Position>().Get(b);
        pb.X = 20f;
        sys.OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
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

        new TriggerSystem().OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.Empty(EventSet(w, parent));
        Assert.Empty(EventSet(w, child));
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

        new TriggerSystem().OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
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

        new TriggerSystem().OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
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
        parallelSys.OnParallelFixedUpdate(parallelWorld, parallelWorld.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        singleSys.OnParallelFixedUpdate(singleWorld, singleWorld.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, SingleThreadOpts());

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
        sys.OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.NotEmpty(EventSet(w, a));

        w.Components<Trigger>().Remove(a);
        w.Components<Trigger>().Remove(b);
        sys.OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
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

        new TriggerSystem().OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
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

        new TriggerSystem().OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
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

        new TriggerSystem().OnParallelFixedUpdate(w, w.QueryChunks(SystemQuerySpec.All<Trigger>()), 1f / 60f, ParOpts());
        Assert.Empty(EventSet(w, missingParent));
        Assert.Empty(EventSet(w, chainChild));
        Assert.Empty(EventSet(w, unrelated));
    }
}
