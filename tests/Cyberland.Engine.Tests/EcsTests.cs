using Cyberland.Engine.Core.Ecs;
using Xunit;

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

    private struct CmpA
    {
        public int V;
    }

    private struct CmpB
    {
        public float X;
    }

    [Fact]
    public void ComponentStore_GetOrAdd_TryGet_Get_Remove_swap_tail_and_sparse_grow()
    {
        var store = new ComponentStore<CmpA>();
        var e0 = EntityId.FromParts(0, 1);
        var e1 = EntityId.FromParts(1, 1);
        var eHigh = EntityId.FromParts(10_000, 1);

        ref var a0 = ref store.GetOrAdd(e0, new CmpA { V = 1 });
        Assert.Equal(1, a0.V);
        ref var a1 = ref store.GetOrAdd(e1, new CmpA { V = 2 });
        a1.V = 22;
        Assert.True(store.TryGet(e1, out var got));
        Assert.Equal(22, got.V);

        ref var aH = ref store.GetOrAdd(eHigh);
        aH.V = 99;
        Assert.True(store.Contains(eHigh));

        var ex = Assert.Throws<InvalidOperationException>(() => _ = store.Get(EntityId.FromParts(2, 1)));
        Assert.Contains("missing", ex.Message);

        store.Remove(e0);
        Assert.False(store.TryGet(e0, out _));

        store.Remove(e1);
        store.Remove(eHigh);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void World_DestroyEntity_removes_from_all_stores()
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
}
