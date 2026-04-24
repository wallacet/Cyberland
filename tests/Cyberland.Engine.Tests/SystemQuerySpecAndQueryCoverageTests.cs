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
        world.Components<Transform>().GetOrAdd(e);
        world.Components<Sprite>().GetOrAdd(e);
        world.Components<BitmapText>().GetOrAdd(e);

        var spec = SystemQuerySpec.All<BitmapText, Sprite, Transform>();
        var bitmapCol = spec.GetColumnIndex<BitmapText>(world);
        var spriteCol = spec.GetColumnIndex<Sprite>(world);
        var transformCol = spec.GetColumnIndex<Transform>(world);
        var n = 0;
        foreach (var chunk in world.QueryChunks(spec))
        {
            n += chunk.Count;
            _ = chunk.Column<BitmapText>(bitmapCol);
            _ = chunk.Column<Sprite>(spriteCol);
            _ = chunk.Column<Transform>(transformCol);
        }

        Assert.Equal(1, n);
        Assert.InRange(spec.GetColumnIndex<BitmapText>(world), 0, 2);
        Assert.InRange(spec.GetColumnIndex<Sprite>(world), 0, 2);
        Assert.InRange(spec.GetColumnIndex<Transform>(world), 0, 2);
    }

    [Fact]
    public void SystemQuerySpec_duplicate_types_dedupes_sorted_ids_and_queries()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e);

        var spec = new SystemQuerySpec(new[] { typeof(Transform), typeof(Transform) });
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
        var a = SystemQuerySpec.All<Transform>();
        var b = SystemQuerySpec.All<Transform>();
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
        var ex = Assert.Throws<ArgumentException>(() => w.GetQueryColumnIndex<Sprite>(SystemQuerySpec.All<Transform>()));
        Assert.Contains("Sprite", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ChunkColumn_generic_throws_when_component_not_in_query()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e);
        var spec = SystemQuerySpec.All<Transform>();

        var ex = Assert.Throws<ArgumentException>(() =>
        {
            foreach (var chunk in world.QueryChunks(spec))
                _ = chunk.Column<Sprite>();
        });
        Assert.Contains("Sprite", ex.Message, StringComparison.Ordinal);
    }

    private struct UniqueUnregisteredStruct : IComponent
    {
    }

    [Fact]
    public void ComponentRegistry_GetOrRegister_Type_uses_reflection_path_and_rejects_classes()
    {
        var reg = new ComponentRegistry();
        Assert.Throws<ArgumentException>(() => reg.GetOrRegister(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => reg.GetOrRegister(null!));

        var id = reg.GetOrRegister(typeof(UniqueUnregisteredStruct));
        Assert.Equal(reg.GetOrRegister<UniqueUnregisteredStruct>(), id);
    }

    private struct StructWithoutIComponentMarker
    {
    }

    [Fact]
    public void ComponentRegistry_GetOrRegister_Type_rejects_structs_not_marked_IComponent()
    {
        var reg = new ComponentRegistry();
        var ex = Assert.Throws<ArgumentException>(() => reg.GetOrRegister(typeof(StructWithoutIComponentMarker)));
        Assert.Contains(nameof(IComponent), ex.Message, StringComparison.Ordinal);
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
        w.Components<Transform>().GetOrAdd(ePs);
        w.Components<Sprite>().GetOrAdd(ePs);

        var ePt = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(ePt);
        w.Components<BitmapText>().GetOrAdd(ePt);

        var eFull = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(eFull);
        w.Components<Sprite>().GetOrAdd(eFull);
        w.Components<BitmapText>().GetOrAdd(eFull);

        var spec = SystemQuerySpec.All<BitmapText, Sprite, Transform>();
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
            sortUnique!.Invoke(null, new object[] { new[] { typeof(Transform), typeof(Transform) } }));
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
    public void SystemQuerySpec_All_quadruple_sorts_and_queries()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.Components<BitmapText>().GetOrAdd(e);

        var spec = SystemQuerySpec.All<BitmapText, Transform, TextBuildFingerprint, TextSpriteCache>();
        var n = 0;
        foreach (var chunk in world.QueryChunks(spec))
        {
            n += chunk.Count;
            _ = chunk.Column<BitmapText>();
            _ = chunk.Column<Transform>();
            _ = chunk.Column<TextBuildFingerprint>();
            _ = chunk.Column<TextSpriteCache>();
        }

        Assert.Equal(1, n);
    }

    [Fact]
    public void TextRenderSystem_OnStart_initializes_column_map()
    {
        var host = new GameHostServices(new KeyBindingStore()) { Renderer = new RecordingRenderer() };
        var world = new World();
        var sys = new TextRenderSystem(host);
        var spec = SystemQuerySpec.All<BitmapText, Transform, TextBuildFingerprint, TextSpriteCache>();
        sys.OnStart(world, world.QueryChunks(spec));
        sys.OnLateUpdate(world.QueryChunks(spec), 0.016f);
    }
}
