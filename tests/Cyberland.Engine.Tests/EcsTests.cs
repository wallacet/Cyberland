using System.Numerics;
using System.Runtime.InteropServices;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Tests;

public sealed class EcsTests
{
    [Fact]
    public void EntityId_FromParts_masks_index_and_preserves_generation()
    {
        var hi = 7u << EntityId.IndexBits;
        var id = EntityId.FromParts(0xFFFFF, 7);
        Assert.Equal(0xFFFFFu, id.Index);
        Assert.Equal(7u, id.Generation);
        Assert.Equal(hi | 0xFFFFFu, id.Raw);
    }

    [Fact]
    public void EntityId_equality_and_hash()
    {
        var a = EntityId.FromParts(3, 2);
        var b = EntityId.FromParts(3, 2);
        var c = EntityId.FromParts(3, 1);
        Assert.True(a == b);
        Assert.True(a != c);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void EntityRegistry_recycles_indices_and_bumps_generation()
    {
        var r = new EntityRegistry();
        var a = r.Create();
        Assert.True(r.IsAlive(a));
        r.Destroy(a);
        Assert.False(r.IsAlive(a));
        var b = r.Create();
        Assert.Equal(a.Index, b.Index);
        Assert.True(b.Generation > a.Generation);
    }

    [Fact]
    public void EntityRegistry_Destroy_invalid_index_is_noop()
    {
        var r = new EntityRegistry();
        var bad = EntityId.FromParts(99_999u, 1);
        r.Destroy(bad);
        /* should not throw */
    }

    [Fact]
    public void EntityRegistry_IsAlive_rejects_out_of_range()
    {
        var r = new EntityRegistry();
        Assert.False(r.IsAlive(EntityId.FromParts(50_000u, 0)));
    }

    private struct CmpA : IComponent
    {
        public int V;
    }

    private struct CmpB : IComponent
    {
        public float X;
    }

    private struct CmpC : IComponent
    {
    }

    private struct CmpWithFieldInitializer : IComponent
    {
        public int V = 42;

        public CmpWithFieldInitializer()
        {
        }
    }

    [Fact]
    public void GetOrAdd_without_initial_uses_new_T_for_struct_field_initializers()
    {
        var world = new World();
        var e = world.CreateEntity();
        ref var c = ref world.Components<CmpWithFieldInitializer>().GetOrAdd(e);
        Assert.Equal(42, c.V);
    }

    [Fact]
    public void Components_GetOrAdd_TryGet_Get_Remove_and_sparse_record_grow()
    {
        var world = new World();
        var store = world.Components<CmpA>();
        var e0 = world.CreateEntity();
        var e1 = world.CreateEntity();

        ref var a0 = ref store.GetOrAdd(e0, new CmpA { V = 1 });
        Assert.Equal(1, a0.V);
        ref var a1 = ref store.GetOrAdd(e1, new CmpA { V = 2 });
        a1.V = 22;
        Assert.True(store.TryGet(e1, out var got));
        Assert.Equal(22, got.V);

        world.GrowRecordsForIndexForTests(10_000);
        var eHigh = EntityId.FromParts(10_000, 1);
        ref var aH = ref store.GetOrAdd(eHigh);
        aH.V = 99;
        Assert.True(store.Contains(eHigh));

        var ex = Assert.Throws<InvalidOperationException>(() => _ = store.Get(world.CreateEntity()));
        Assert.Contains("missing", ex.Message);

        store.Remove(e0);
        Assert.False(store.TryGet(e0, out _));

        store.Remove(e1);
        store.Remove(eHigh);
    }

    [Fact]
    public void World_DestroyEntity_removes_components()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 5;
        world.Components<CmpB>().GetOrAdd(e).X = 3.5f;
        world.DestroyEntity(e);
        Assert.False(world.Components<CmpA>().Contains(e));
        Assert.False(world.IsAlive(e));
    }

    [Fact]
    public void World_Components_returns_same_store_per_type()
    {
        var world = new World();
        Assert.Same(world.Components<CmpA>(), world.Components<CmpA>());
    }

    [Fact]
    public void Add_second_component_migrates_archetype()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 7;
        world.Components<CmpB>().GetOrAdd(e).X = 2.5f;
        Assert.True(world.Components<CmpA>().TryGet(e, out var a));
        Assert.Equal(7, a.V);
        Assert.True(world.Components<CmpB>().TryGet(e, out var b));
        Assert.Equal(2.5f, b.X);
    }

    [Fact]
    public void Remove_one_component_migrates_down()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 1;
        world.Components<CmpB>().GetOrAdd(e).X = 3f;
        world.Components<CmpB>().Remove(e);
        Assert.True(world.Components<CmpA>().Contains(e));
        Assert.False(world.Components<CmpB>().Contains(e));
    }

    [Fact]
    public void Remove_last_component_clears_layout()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 9;
        world.Components<CmpA>().Remove(e);
        Assert.False(world.Components<CmpA>().Contains(e));
    }

    [Fact]
    public void QueryChunks_yields_chunk_spans()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 42;

        var found = false;
        foreach (var chunk in world.QueryChunks<CmpA>())
        {
            Assert.True(chunk.Count > 0);
            Assert.Equal(42, chunk.Components[0].V);
            Assert.Equal(e, chunk.Entities[0]);
            found = true;
        }

        Assert.True(found);
    }

    [Fact]
    public void QueryChunks2_matches_both()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 1;
        world.Components<CmpB>().GetOrAdd(e).X = 4f;

        var found = false;
        foreach (var chunk in world.QueryChunks<CmpA, CmpB>())
        {
            Assert.Equal(1, chunk.Components0[0].V);
            Assert.Equal(4f, chunk.Components1[0].X);
            Assert.Equal(1, chunk.Count);
            Assert.Equal(e, chunk.Entities[0]);
            found = true;
        }

        Assert.True(found);
    }

    [Fact]
    public void QueryChunks2_skips_archetype_that_only_has_first_component()
    {
        var world = new World();
        var onlyA = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(onlyA).V = 9;
        var both = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(both).V = 1;
        world.Components<CmpB>().GetOrAdd(both).X = 2f;

        var n = 0;
        foreach (var chunk in world.QueryChunks<CmpA, CmpB>())
        {
            Assert.Equal(1, chunk.Count);
            Assert.Equal(1, chunk.Components0[0].V);
            Assert.Equal(2f, chunk.Components1[0].X);
            Assert.Equal(both, chunk.Entities[0]);
            n++;
        }

        Assert.Equal(1, n);
    }

    [Fact]
    public void QueryChunks_empty_when_no_component()
    {
        var world = new World();
        foreach (var _ in world.QueryChunks<CmpC>())
            Assert.Fail("unexpected chunk");
    }

    [Fact]
    public void QueryChunks2_empty_when_no_archetype_for_first_type()
    {
        var world = new World();
        foreach (var _ in world.QueryChunks<CmpA, CmpB>())
            Assert.Fail("unexpected chunk");
    }

    [Fact]
    public void SimdFloat_MultiplyInPlace_empty_and_vector_tail()
    {
        Span<float> empty = stackalloc float[0];
        SimdFloat.MultiplyInPlace(empty, 2f);

        Span<float> small = stackalloc float[3];
        small[0] = 2f;
        small[1] = 4f;
        small[2] = 8f;
        SimdFloat.MultiplyInPlace(small, 0.5f);
        Assert.Equal(1f, small[0]);
        Assert.Equal(2f, small[1]);
        Assert.Equal(4f, small[2]);

        var wide = new float[Vector<float>.Count + 2];
        for (var i = 0; i < wide.Length; i++)
            wide[i] = 2f;
        SimdFloat.MultiplyInPlace(wide, 3f);
        foreach (var x in wide)
            Assert.Equal(6f, x);
    }

    [Fact]
    public void SimdFloat_MultiplyElementWise_min_length_and_zero()
    {
        Span<float> a = stackalloc float[] { 2f, 3f };
        Span<float> b = stackalloc float[] { 4f, 5f };
        Span<float> dst = stackalloc float[2];
        SimdFloat.MultiplyElementWise(a, b, dst);
        Assert.Equal(8f, dst[0]);
        Assert.Equal(15f, dst[1]);

        Span<float> z = stackalloc float[0];
        SimdFloat.MultiplyElementWise(z, z, z);
    }

    [Fact]
    public void SimdFloat_MultiplyElementWise_vector_loop()
    {
        var n = Vector<float>.Count + 3;
        var a = new float[n];
        var b = new float[n];
        var dst = new float[n];
        for (var i = 0; i < n; i++)
        {
            a[i] = i + 1f;
            b[i] = 2f;
        }

        SimdFloat.MultiplyElementWise(a, b, dst);
        for (var i = 0; i < n; i++)
            Assert.Equal(a[i] * 2f, dst[i]);
    }

    private struct TwoFloats
    {
        public float X;
        public float Y;
    }

    [Fact]
    public void SimdFloat_chunk_style_cast_multiply()
    {
        Span<TwoFloats> pair = stackalloc TwoFloats[2];
        pair[0] = new TwoFloats { X = 1f, Y = 2f };
        pair[1] = new TwoFloats { X = 4f, Y = 5f };
        var f = MemoryMarshal.Cast<TwoFloats, float>(pair);
        SimdFloat.MultiplyInPlace(f, 2f);
        Assert.Equal(2f, pair[0].X);
        Assert.Equal(4f, pair[0].Y);
        Assert.Equal(8f, pair[1].X);
        Assert.Equal(10f, pair[1].Y);
    }

    [Fact]
    public void World_DestroyEntity_on_dead_id_is_noop()
    {
        var world = new World();
        var dead = EntityId.FromParts(0, 99);
        world.DestroyEntity(dead);
    }

    [Fact]
    public void Get_throws_when_component_not_in_archetype()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 1;
        var ex = Assert.Throws<InvalidOperationException>(() => _ = world.Components<CmpB>().Get(e));
        Assert.Contains("archetype", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveComponent_noop_when_type_not_present()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 3;
        world.Components<CmpB>().Remove(e);
        Assert.True(world.Components<CmpA>().TryGet(e, out var a) && a.V == 3);
    }

    [Fact]
    public void TryGet_false_when_entity_index_outside_record_table()
    {
        var world = new World();
        var far = EntityId.FromParts(50_000u, 1);
        Assert.False(world.Components<CmpA>().TryGet(far, out _));
        Assert.False(world.Components<CmpA>().Contains(far));
    }

    [Fact]
    public void TryGet_false_when_component_not_in_archetype()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 1;
        Assert.False(world.Components<CmpB>().TryGet(e, out _));
    }

    [Fact]
    public void QueryChunks_skips_empty_chunks_after_destroy()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 7;
        world.DestroyEntity(e);

        var n = 0;
        foreach (var chunk in world.QueryChunks<CmpA>())
        {
            Assert.True(chunk.Count > 0);
            n++;
        }

        Assert.Equal(0, n);
    }

    [Fact]
    public void QueryChunks2_skips_empty_chunks_after_destroy()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<CmpA>().GetOrAdd(e).V = 1;
        world.Components<CmpB>().GetOrAdd(e).X = 2f;
        world.DestroyEntity(e);

        var n = 0;
        foreach (var _ in world.QueryChunks<CmpA, CmpB>())
            n++;

        Assert.Equal(0, n);
    }

    [Fact]
    public void SignatureHelpers_and_comparer_branches()
    {
        ReadOnlySpan<uint> s = stackalloc uint[] { 10, 20, 30 };
        Assert.True(SignatureHelpers.BinarySearchUint(s, 5u) < 0);

        var dup = new uint[] { 1u, 3u };
        Assert.Equal(dup, SignatureHelpers.InsertSorted(dup, 3u));

        var miss = new uint[] { 1u, 2u };
        Assert.Equal(miss, SignatureHelpers.RemoveSorted(miss, 99u));

        var c = SignatureComparer.Instance;
        var same = new uint[] { 1, 2 };
        Assert.True(c.Equals(same, same));
        Assert.False(c.Equals(new uint[] { 1 }, new uint[] { 1, 2 }));
        Assert.False(c.Equals(new uint[] { 1, 3 }, new uint[] { 1, 2 }));
        Assert.Equal(c.GetHashCode(new uint[] { 1, 2 }), c.GetHashCode(new uint[] { 1, 2 }));
    }
}
