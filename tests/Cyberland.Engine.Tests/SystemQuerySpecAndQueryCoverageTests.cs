using System.Reflection;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Covers <see cref="SystemQuerySpec"/>, multi-component query resolution, and related <see cref="World"/> / <see cref="ArchetypeWorld"/> paths.
/// </summary>
public sealed class SystemQuerySpecAndQueryCoverageTests
{
    [Fact]
    public void SystemQuerySpec_All_triple_sorts_types_and_iterates()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        world.Components<Sprite>().GetOrAdd(e);
        world.Components<Transform>().GetOrAdd(e);

        var spec = SystemQuerySpec.All<Position, Sprite, Transform>();
        var n = 0;
        foreach (var chunk in world.QueryChunks(spec))
        {
            n += chunk.Count;
            _ = chunk.Column<Position>(0);
            _ = chunk.Column<Sprite>(1);
            _ = chunk.Column<Transform>(2);
        }

        Assert.Equal(1, n);
        Assert.Equal(0, spec.GetColumnIndex<Position>(world));
        Assert.Equal(1, spec.GetColumnIndex<Sprite>(world));
        Assert.Equal(2, spec.GetColumnIndex<Transform>(world));
    }

    [Fact]
    public void SystemQuerySpec_duplicate_types_dedupes_sorted_ids_and_queries()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);

        var spec = new SystemQuerySpec(new[] { typeof(Position), typeof(Position) });
        var ecs = (ArchetypeWorld)typeof(World).GetField("_ecs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!;
        var ids = spec.ResolveSortedComponentIds(ecs.Registry);
        Assert.Single(ids);

        var n = 0;
        foreach (var chunk in world.QueryChunks(spec))
            n += chunk.Count;
        Assert.Equal(1, n);
    }

    [Fact]
    public void SystemQuerySpec_equals_object_and_hash_code()
    {
        var a = SystemQuerySpec.All<Position>();
        var b = SystemQuerySpec.All<Position>();
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals((object?)null));
        Assert.False(a.Equals(new object()));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void World_GetQueryColumnIndex_throws_when_component_not_in_spec()
    {
        var w = new World();
        var ex = Assert.Throws<ArgumentException>(() => w.GetQueryColumnIndex<Sprite>(SystemQuerySpec.All<Position>()));
        Assert.Contains("Sprite", ex.Message, StringComparison.Ordinal);
    }

    private struct UniqueUnregisteredStruct { }

    [Fact]
    public void ComponentRegistry_GetOrRegister_Type_uses_reflection_path_and_rejects_classes()
    {
        var reg = new ComponentRegistry();
        Assert.Throws<ArgumentException>(() => reg.GetOrRegister(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => reg.GetOrRegister(null!));

        var id = reg.GetOrRegister(typeof(UniqueUnregisteredStruct));
        Assert.Equal(reg.GetOrRegister<UniqueUnregisteredStruct>(), id);
    }

    [Fact]
    public void ArchetypeWorld_GetArchetypeIndicesMatchingAll_empty_returns_empty_list()
    {
        var ecs = new ArchetypeWorld();
        var list = ecs.GetArchetypeIndicesMatchingAll(Array.Empty<uint>());
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    /// <summary>
    /// Forces <see cref="ArchetypeWorld.GetArchetypeIndicesMatchingAll"/> to scan a smallest-index list where some
    /// candidates lack a required component (inner signature check).
    /// </summary>
    [Fact]
    public void ArchetypeWorld_GetArchetypeIndicesMatchingAll_skips_incomplete_smallest_candidates()
    {
        var w = new World();

        var ePs = w.CreateEntity();
        w.Components<Position>().GetOrAdd(ePs);
        w.Components<Sprite>().GetOrAdd(ePs);

        var ePt = w.CreateEntity();
        w.Components<Position>().GetOrAdd(ePt);
        w.Components<Transform>().GetOrAdd(ePt);

        var eFull = w.CreateEntity();
        w.Components<Position>().GetOrAdd(eFull);
        w.Components<Sprite>().GetOrAdd(eFull);
        w.Components<Transform>().GetOrAdd(eFull);

        var spec = SystemQuerySpec.All<Position, Sprite, Transform>();
        var n = 0;
        foreach (var chunk in w.QueryChunks(spec))
            n += chunk.Count;

        Assert.Equal(1, n);
    }

    [Fact]
    public void SystemQuerySpec_SortUnique_throws_on_duplicate_component_types()
    {
        var sortUnique = typeof(SystemQuerySpec).GetMethod("SortUnique", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(sortUnique);
        var ex = Assert.Throws<TargetInvocationException>(() =>
            sortUnique!.Invoke(null, new object[] { new[] { typeof(Position), typeof(Position) } }));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void SystemQuerySpec_Empty_resolve_returns_empty_sorted_ids()
    {
        var world = new World();
        var ecs = (ArchetypeWorld)typeof(World).GetField("_ecs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!;
        Assert.Empty(SystemQuerySpec.Empty.ResolveSortedComponentIds(ecs.Registry));
        foreach (var _ in world.QueryChunks(SystemQuerySpec.Empty))
        {
        }
    }

    [Fact]
    public void TextRenderSystem_OnStart_initializes_column_map()
    {
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = null };
        var world = new World();
        var sys = new TextRenderSystem(host);
        var spec = SystemQuerySpec.All<BitmapText, Position>();
        sys.OnStart(world, world.QueryChunks(spec));
        sys.OnLateUpdate(world, world.QueryChunks(spec), 0.016f);
    }
}
