using System.Globalization;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Moq;
using Silk.NET.Input;

namespace Cyberland.Engine.Tests;

public sealed class CoverageFillTests
{
    private struct Cmp
    {
        public int V;
    }

    [Fact]
    public void World_Exposes_registry_and_DestroyEntity_with_no_components_stores()
    {
        var world = new World();
        var id = world.CreateEntity();
        _ = world.Entities;
        world.DestroyEntity(id);
        Assert.False(world.IsAlive(id));
    }

    [Fact]
    public void ComponentStore_TryGet_false_and_Remove_noops_when_far_or_empty_slot()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        Assert.False(store.TryGet(EntityId.FromParts(9_999, 1), out _));
        store.Remove(EntityId.FromParts(9_999, 1));
        store.Remove(EntityId.FromParts(1, 1));
    }

    [Fact]
    public void ComponentStore_Remove_first_of_three_uses_swap_with_tail()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        var c = world.CreateEntity();
        store.GetOrAdd(a).V = 1;
        store.GetOrAdd(b).V = 2;
        store.GetOrAdd(c).V = 3;

        store.Remove(a);

        Assert.False(store.Contains(a));
        Assert.True(store.Contains(c));
        Assert.True(store.Contains(b));
    }

    [Fact]
    public void ComponentStore_Remove_middle_element()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        var c = world.CreateEntity();
        store.GetOrAdd(a).V = 1;
        store.GetOrAdd(b).V = 2;
        store.GetOrAdd(c).V = 3;

        store.Remove(b);

        Assert.False(store.Contains(b));
        Assert.True(store.Contains(a));
        Assert.True(store.Contains(c));
    }

    [Fact]
    public void ComponentStore_Get_throws_when_slot_empty()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        var ex = Assert.Throws<InvalidOperationException>(() => _ = store.Get(EntityId.FromParts(0, 1)));
        Assert.Contains("missing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComponentStore_QueryChunks_visible()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        store.GetOrAdd(a).V = 11;
        store.GetOrAdd(b).V = 22;

        var total = 0;
        foreach (var chunk in world.QueryChunks<Cmp>())
        {
            total += chunk.Count;
            Assert.Equal(chunk.Entities.Length, chunk.Count);
            Assert.Equal(chunk.Components.Length, chunk.Count);
        }

        Assert.Equal(2, total);
    }

    [Fact]
    public void ComponentStore_GetOrAdd_second_call_returns_existing_component()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        var e = world.CreateEntity();
        store.GetOrAdd(e).V = 7;
        ref var second = ref store.GetOrAdd(e, new Cmp { V = 99 });
        Assert.Equal(7, second.V);
    }

    [Fact]
    public void ComponentStore_Remove_only_element_skips_swap_block()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        var e = world.CreateEntity();
        store.GetOrAdd(e).V = 1;
        store.Remove(e);
        Assert.False(store.Contains(e));
    }

    [Fact]
    public void ComponentStore_Remove_when_not_present_returns_early()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        var e = world.CreateEntity();
        store.GetOrAdd(e).V = 1;
        store.Remove(EntityId.FromParts(4, 1));
        Assert.True(store.Contains(e));
        Assert.True(store.TryGet(e, out var got) && got.V == 1);
    }

    [Fact]
    public void ComponentStore_Get_returns_ref_when_present()
    {
        var world = new World();
        var store = world.Components<Cmp>();
        var e = world.CreateEntity();
        store.GetOrAdd(e).V = 10;
        ref var v = ref store.Get(e);
        v.V = 12;
        Assert.True(store.TryGet(e, out var got));
        Assert.Equal(12, got.V);
    }

    [Fact]
    public void VirtualFileSystem_Exists_and_TryOpenRead_false_when_no_mounts()
    {
        var vfs = new VirtualFileSystem();
        Assert.False(vfs.Exists("nope.txt"));
        Assert.False(vfs.TryOpenRead("nope.txt", out _));
    }

    [Fact]
    public void VirtualFileSystem_TryOpenRead_continues_when_early_root_has_no_file()
    {
        var empty = Path.Combine(Path.GetTempPath(), "cyb-vfs-empty-" + Guid.NewGuid());
        var filled = Path.Combine(Path.GetTempPath(), "cyb-vfs-filled-" + Guid.NewGuid());
        Directory.CreateDirectory(empty);
        Directory.CreateDirectory(filled);
        File.WriteAllText(Path.Combine(filled, "x.txt"), "ok");
        try
        {
            var vfs = new VirtualFileSystem();
            // Iterate from last mount to first; put empty last so the first probe misses.
            vfs.Mount(filled);
            vfs.Mount(empty);
            Assert.True(vfs.TryOpenRead("x.txt", out var stream));
            stream!.Dispose();
        }
        finally
        {
            Directory.Delete(empty, true);
            Directory.Delete(filled, true);
        }
    }

    [Fact]
    public void VirtualFileSystem_Exists_continues_past_missing_candidates()
    {
        var empty = Path.Combine(Path.GetTempPath(), "cyb-vfs-empty2-" + Guid.NewGuid());
        var filled = Path.Combine(Path.GetTempPath(), "cyb-vfs-filled2-" + Guid.NewGuid());
        Directory.CreateDirectory(empty);
        Directory.CreateDirectory(filled);
        File.WriteAllText(Path.Combine(filled, "x.txt"), "ok");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(filled);
            vfs.Mount(empty);
            Assert.True(vfs.Exists("x.txt"));
        }
        finally
        {
            Directory.Delete(empty, true);
            Directory.Delete(filled, true);
        }
    }

    [Fact]
    public void VirtualFileSystem_Normalize_trims_and_strips_leading_slash()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-vfs-norm-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "x.txt"), "ok");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            Assert.True(vfs.Exists(" /x.txt "));
            Assert.True(vfs.TryOpenRead(@" \x.txt ", out var s));
            s!.Dispose();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AssetManager_OpenReadOrThrow_throws_when_missing()
    {
        var assets = new AssetManager(new VirtualFileSystem());
        Assert.Throws<FileNotFoundException>(() => assets.OpenReadOrThrow("nope.bin"));
    }

    [Fact]
    public async Task AssetManager_LoadJsonAsync_allows_null_reference_result()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-json-null-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "x.json"), "null");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var result = await assets.LoadJsonAsync<Dictionary<string, string>?>("x.json");
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void KeyBindingStore_IsDown_short_circuits_when_action_unknown()
    {
        var store = new KeyBindingStore();
        var kb = new Mock<IKeyboard>(MockBehavior.Strict);
        Assert.False(store.IsDown(kb.Object, "missing"));
        kb.VerifyNoOtherCalls();
    }

    [Fact]
    public void FrameEdgeLatch_Arm_TryConsume_and_IsArmed()
    {
        var latch = new FrameEdgeLatch();
        Assert.False(latch.IsArmed);
        Assert.False(latch.TryConsume());

        latch.Arm();
        Assert.True(latch.IsArmed);
        Assert.True(latch.TryConsume());
        Assert.False(latch.IsArmed);
        Assert.False(latch.TryConsume());
    }

    [Fact]
    public void GraphicsInitializationException_without_inner_message()
    {
        var ex = new GraphicsInitializationException("gpu detail");
        Assert.Contains("gpu detail", ex.UserMessage);
        Assert.DoesNotContain("System.", ex.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityId_Equals_object_rejects_non_entity()
    {
        var id = EntityId.FromParts(1, 1);
        Assert.False(id.Equals(new object()));
    }

    [Fact]
    public void LocalizationManager_TryGet_true_and_Culture_roundtrip()
    {
        var loc = new LocalizationManager();
        var culture = CultureInfo.GetCultureInfo("en-US");
        loc.SetCulture(culture);
        loc.MergeJson("""{"k":"v"}"""u8.ToArray());
        Assert.True(loc.TryGet("k", out var value));
        Assert.Equal("v", value);
        Assert.Equal(culture, loc.Culture);
    }

    [Fact]
    public void ModLoader_skips_assembly_when_no_concrete_IMod()
    {
        var modsRoot = Path.Combine(Path.GetTempPath(), "cyb-mod-no-imod-" + Guid.NewGuid());
        Directory.CreateDirectory(modsRoot);
        var modDir = Path.Combine(modsRoot, "m");
        Directory.CreateDirectory(Path.Combine(modDir, "Content"));
        var helperDll = typeof(Cyberland.ModPluginHelper.PluginHelper).Assembly.Location;
        File.Copy(helperDll, Path.Combine(modDir, "Cyberland.ModPluginHelper.dll"), true);
        File.WriteAllText(
            Path.Combine(modDir, "manifest.json"),
            """{"id":"m","entryAssembly":"Cyberland.ModPluginHelper.dll","contentRoot":"Content","loadOrder":0}""");
        ModLoader? loader = null;
        try
        {
            loader = new ModLoader();
            loader.LoadAll(
                modsRoot,
                new VirtualFileSystem(),
                new LocalizationManager(),
                new World(),
                new SystemScheduler(new ParallelismSettings()),
                new GameHostServices(new KeyBindingStore()));
            Assert.Single(loader.LoadedManifests);
            loader.UnloadAll();
        }
        finally
        {
            loader?.UnloadAll();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Directory.Delete(modsRoot, true);
        }
    }

    [Fact]
    public void ModLoadContext_MountDefaultContent_when_content_folder_missing_mount_is_noop()
    {
        var modRoot = Path.Combine(Path.GetTempPath(), "cyb-modctx-empty-" + Guid.NewGuid());
        Directory.CreateDirectory(modRoot);
        try
        {
            var vfs = new VirtualFileSystem();
            var ctx = new ModLoadContext(
                new ModManifest { Id = "x", ContentRoot = "Content" },
                modRoot,
                vfs,
                new LocalizationManager(),
                new World(),
                new SystemScheduler(new ParallelismSettings()),
                new GameHostServices(new KeyBindingStore()));

            ctx.MountDefaultContent();
            Assert.Empty(vfs.Roots);
        }
        finally
        {
            Directory.Delete(modRoot, true);
        }
    }
}
