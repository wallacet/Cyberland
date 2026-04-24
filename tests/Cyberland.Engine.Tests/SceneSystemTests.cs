using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
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
        var h = new GameHostServices() { Renderer = r };
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
    public void Transform_WorldPosition_setter_survives_hierarchy_pass_for_root()
    {
        // Assigning WorldPosition on a root entity must also back-propagate to LocalMatrix so the next
        // hierarchy pass reproduces the requested pose (rather than resetting to identity).
        var w = new World();
        var e = w.CreateEntity();
        var tf = Transform.Identity;
        tf.WorldPosition = new Vector2D<float>(640f, 360f);
        w.Components<Transform>().GetOrAdd(e) = tf;

        // Sanity: read-back immediately after the setter returns what was written.
        Assert.Equal(640f, w.Components<Transform>().Get(e).WorldPosition.X, 3);
        Assert.Equal(360f, w.Components<Transform>().Get(e).WorldPosition.Y, 3);

        // Hierarchy pass recomputes WorldMatrix from LocalMatrix — the back-prop means this reproduces the
        // same world pose instead of wiping it to identity.
        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        var after = w.Components<Transform>().Get(e);
        Assert.Equal(640f, after.WorldPosition.X, 3);
        Assert.Equal(360f, after.WorldPosition.Y, 3);
    }

    [Fact]
    public void Transform_WorldRotation_and_WorldScale_setters_survive_hierarchy_pass_for_root()
    {
        var w = new World();
        var e = w.CreateEntity();
        var tf = Transform.Identity;
        tf.WorldPosition = new Vector2D<float>(50f, -25f);
        tf.WorldRotationRadians = MathF.PI * 0.25f;
        tf.WorldScale = new Vector2D<float>(2f, 3f);
        w.Components<Transform>().GetOrAdd(e) = tf;

        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        var after = w.Components<Transform>().Get(e);
        Assert.Equal(50f, after.WorldPosition.X, 3);
        Assert.Equal(-25f, after.WorldPosition.Y, 3);
        Assert.Equal(MathF.PI * 0.25f, after.WorldRotationRadians, 4);
        Assert.Equal(2f, after.WorldScale.X, 3);
        Assert.Equal(3f, after.WorldScale.Y, 3);
    }

    [Fact]
    public void Transform_WorldPosition_setter_preserves_parent_relationship()
    {
        // A child parented to a translated root: writing the child's WorldPosition should move the child to
        // that world point while keeping it parented. Specifically, after the next hierarchy pass the child's
        // WorldPosition must equal what was set (parent hasn't moved), not drift due to the parent offset
        // being re-applied on top of an unrelated local.
        var w = new World();
        var parent = w.CreateEntity();
        var child = w.CreateEntity();

        ref var parentTf = ref w.Components<Transform>().GetOrAdd(parent);
        parentTf = Transform.Identity;
        parentTf.LocalPosition = new Vector2D<float>(100f, 0f);

        ref var childTf = ref w.Components<Transform>().GetOrAdd(child);
        childTf = Transform.Identity;
        childTf.LocalPosition = new Vector2D<float>(50f, 50f);
        childTf.Parent = parent;

        // First hierarchy pass establishes child's current world = parent (100,0) + local (50,50) = (150,50).
        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        // Move the child by writing WorldPosition directly; the setter should back-propagate to a LocalMatrix
        // that, composed with parent's WorldMatrix, yields (200, 100).
        ref var childRef = ref w.Components<Transform>().Get(child);
        childRef.WorldPosition = new Vector2D<float>(200f, 100f);

        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        var after = w.Components<Transform>().Get(child);
        Assert.Equal(200f, after.WorldPosition.X, 3);
        Assert.Equal(100f, after.WorldPosition.Y, 3);

        // The child's local offset from its parent should now be (100, 100) — i.e. the back-prop solved for
        // the correct local so the parent's translation is not double-counted.
        Assert.Equal(100f, after.LocalPosition.X, 3);
        Assert.Equal(100f, after.LocalPosition.Y, 3);
    }

    [Fact]
    public void Transform_WorldPosition_setter_falls_back_to_root_on_degenerate_world()
    {
        // default(Transform) has a zero matrix; inverting the old world fails, so the setter falls back to
        // root semantics (LocalMatrix = newWorld). The hierarchy pass then produces the requested pose.
        var w = new World();
        var e = w.CreateEntity();
        Transform tf = default; // zero matrices
        tf.WorldPosition = new Vector2D<float>(7f, 9f);
        w.Components<Transform>().GetOrAdd(e) = tf;

        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        var after = w.Components<Transform>().Get(e);
        Assert.Equal(7f, after.WorldPosition.X, 3);
        Assert.Equal(9f, after.WorldPosition.Y, 3);
    }

    [Fact]
    public void Transform_WorldPosition_setter_falls_back_to_root_on_degenerate_parented_world()
    {
        // A parented child whose own WorldMatrix is still degenerate (e.g. default(Transform) before any
        // hierarchy pass) cannot invert to recover the parent's world; the setter must fall back to
        // LocalMatrix = newWorld so the pose is at least reachable after the next hierarchy pass.
        var w = new World();
        var parent = w.CreateEntity();
        var child = w.CreateEntity();

        ref var parentTf = ref w.Components<Transform>().GetOrAdd(parent);
        parentTf = Transform.Identity;

        ref var childTf = ref w.Components<Transform>().GetOrAdd(child);
        childTf = default; // zero matrices: WorldMatrix is non-invertible
        childTf.Parent = parent;
        childTf.WorldPosition = new Vector2D<float>(11f, 13f);

        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        var after = w.Components<Transform>().Get(child);
        Assert.Equal(11f, after.WorldPosition.X, 3);
        Assert.Equal(13f, after.WorldPosition.Y, 3);
    }

    [Fact]
    public void Transform_root_LocalPosition_setter_keeps_WorldMatrix_in_sync()
    {
        // Setters on a root must leave Local and World consistent so reads between setters (and downstream
        // systems that read WorldMatrix directly, e.g. SpriteRenderSystem) see the requested pose without
        // waiting for the next hierarchy pass.
        var t = Transform.Identity;
        t.LocalPosition = new Vector2D<float>(7f, 11f);
        Assert.Equal(7f, t.WorldPosition.X, 3);
        Assert.Equal(11f, t.WorldPosition.Y, 3);
        Assert.Equal(7f, t.WorldMatrix.M31, 3);
        Assert.Equal(11f, t.WorldMatrix.M32, 3);
    }

    [Fact]
    public void Transform_root_LocalRotation_and_LocalScale_setters_keep_WorldMatrix_in_sync()
    {
        var t = Transform.Identity;
        t.LocalPosition = new Vector2D<float>(3f, 4f);
        t.LocalRotationRadians = MathF.PI * 0.5f;
        t.LocalScale = new Vector2D<float>(2f, 3f);

        // Local and world must match component-wise since the root has no parent.
        Assert.Equal(3f, t.WorldPosition.X, 3);
        Assert.Equal(4f, t.WorldPosition.Y, 3);
        Assert.Equal(MathF.PI * 0.5f, t.WorldRotationRadians, 4);
        Assert.Equal(2f, t.WorldScale.X, 3);
        Assert.Equal(3f, t.WorldScale.Y, 3);
    }

    [Fact]
    public void Transform_root_interleaved_Local_and_World_writes_converge_to_last_write()
    {
        // Mods may freely interleave Local* and World* writes in any order, any number of times — the final
        // matrix pair reflects the last write and is internally consistent.
        var t = Transform.Identity;
        t.LocalPosition = new Vector2D<float>(1f, 2f);
        t.WorldPosition = new Vector2D<float>(5f, 6f);
        t.LocalPosition = new Vector2D<float>(9f, 10f);
        t.WorldRotationRadians = 0.5f;
        t.LocalScale = new Vector2D<float>(2f, 2f);
        t.WorldPosition = new Vector2D<float>(-3f, -4f);

        Assert.Equal(-3f, t.LocalPosition.X, 3);
        Assert.Equal(-4f, t.LocalPosition.Y, 3);
        Assert.Equal(-3f, t.WorldPosition.X, 3);
        Assert.Equal(-4f, t.WorldPosition.Y, 3);
        Assert.Equal(0.5f, t.WorldRotationRadians, 4);
        Assert.Equal(2f, t.WorldScale.X, 3);
        Assert.Equal(2f, t.WorldScale.Y, 3);
    }

    [Fact]
    public void Transform_child_LocalPosition_setter_updates_WorldMatrix_via_implicit_parent()
    {
        // After one hierarchy pass, the child's Local/World pair captures the parent's world. A subsequent
        // child.LocalPosition write must update WorldMatrix using that implicit parent so the render this
        // frame already reflects the new world pose — not wait for the next hierarchy pass.
        var w = new World();
        var parent = w.CreateEntity();
        var child = w.CreateEntity();

        ref var parentTf = ref w.Components<Transform>().GetOrAdd(parent);
        parentTf = Transform.Identity;
        parentTf.LocalPosition = new Vector2D<float>(100f, 0f);

        ref var childTf = ref w.Components<Transform>().GetOrAdd(child);
        childTf = Transform.Identity;
        childTf.LocalPosition = new Vector2D<float>(50f, 50f);
        childTf.Parent = parent;

        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        // Change only the child's local; world must immediately reflect local * parent.World = (160, 60).
        ref var childRef = ref w.Components<Transform>().Get(child);
        childRef.LocalPosition = new Vector2D<float>(60f, 60f);
        Assert.Equal(160f, childRef.WorldPosition.X, 3);
        Assert.Equal(60f, childRef.WorldPosition.Y, 3);

        // Hierarchy reruns idempotently.
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());
        var after = w.Components<Transform>().Get(child);
        Assert.Equal(160f, after.WorldPosition.X, 3);
        Assert.Equal(60f, after.WorldPosition.Y, 3);
        Assert.Equal(60f, after.LocalPosition.X, 3);
        Assert.Equal(60f, after.LocalPosition.Y, 3);
    }

    [Fact]
    public void Transform_LocalPosition_setter_falls_back_to_root_on_degenerate_old_local()
    {
        // default(Transform) is the degenerate "all zeros" state. The local setter cannot recover an implicit
        // parent from a non-invertible LocalMatrix, so it falls back to LocalMatrix = WorldMatrix = newLocal.
        var t = default(Transform);
        t.LocalPosition = new Vector2D<float>(2f, 5f);

        // World follows Local in the degenerate fallback (identity parent assumption).
        Assert.Equal(2f, t.WorldMatrix.M31, 3);
        Assert.Equal(5f, t.WorldMatrix.M32, 3);
    }

    [Fact]
    public void Transform_root_double_assign_LocalPosition_then_WorldPosition_stays_at_target_after_hierarchy()
    {
        // Regression: mods commonly write `LocalPosition = X; WorldPosition = X;` in late-phase systems so
        // the render sees the intended world pose this frame AND the pose survives the next frame's hierarchy
        // recompose. Without the root short-circuit the WorldPosition back-prop reads a stale WorldMatrix
        // (unchanged by the preceding LocalPosition write) and doubled the translation on the next pass.
        var w = new World();
        var e = w.CreateEntity();
        var tf = Transform.Identity;
        var target = new Vector2D<float>(640f, 360f);
        tf.LocalPosition = target;
        tf.WorldPosition = target;
        w.Components<Transform>().GetOrAdd(e) = tf;

        // Immediately after the setters: both caches report the requested pose.
        var immediate = w.Components<Transform>().Get(e);
        Assert.Equal(target.X, immediate.LocalPosition.X, 3);
        Assert.Equal(target.Y, immediate.LocalPosition.Y, 3);
        Assert.Equal(target.X, immediate.WorldPosition.X, 3);
        Assert.Equal(target.Y, immediate.WorldPosition.Y, 3);

        // After hierarchy: WorldMatrix recomposed from LocalMatrix must still land on the target, not 2*target.
        var sys = new TransformHierarchySystem();
        StartEcs(sys, w);
        sys.OnParallelEarlyUpdate(w.QueryChunks(SystemQuerySpec.All<Transform>()), 0f, ParOpts());

        var after = w.Components<Transform>().Get(e);
        Assert.Equal(target.X, after.WorldPosition.X, 3);
        Assert.Equal(target.Y, after.WorldPosition.Y, 3);
    }

    [Fact]
    public void SpriteRenderSystem_noop_when_renderer_null()
    {
        var h = new GameHostServices() { Renderer = null };
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
