using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.TestMod;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class ModdingTests
{
    private static string StageMod(
        string modsRoot,
        string folderName,
        string id,
        int loadOrder,
        bool includeDll = true,
        bool includeEntryAsmInManifest = true)
    {
        var modDir = Path.Combine(modsRoot, folderName);
        Directory.CreateDirectory(Path.Combine(modDir, "Content"));
        var srcDll = typeof(TestModEntry).Assembly.Location;
        var destDll = Path.Combine(modDir, "Cyberland.TestMod.dll");
        if (includeDll)
            File.Copy(srcDll, destDll, overwrite: true);

        var manifest = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["contentRoot"] = "Content",
            ["loadOrder"] = loadOrder,
        };
        if (includeEntryAsmInManifest)
            manifest["entryAssembly"] = "Cyberland.TestMod.dll";

        File.WriteAllText(
            Path.Combine(modDir, "manifest.json"),
            JsonSerializer.Serialize(manifest));

        File.WriteAllText(Path.Combine(modDir, "Content", "note.txt"), id);
        return modDir;
    }

    [Fact]
    public void ModLoadContext_mount_helpers_use_mod_directory()
    {
        var modRoot = Path.Combine(Path.GetTempPath(), "cyb mod ctx " + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(modRoot, "Content"));
        Directory.CreateDirectory(Path.Combine(modRoot, "Extra"));
        File.WriteAllText(Path.Combine(modRoot, "Extra", "x.bin"), "x");
        try
        {
            var vfs = new VirtualFileSystem();
            var m = new ModManifest { Id = "m", ContentRoot = "Content" };
            var loc = new LocalizationManager();
            var world = new World();
            var sched = new SystemScheduler(new ParallelismSettings());
            var ctx = new ModLoadContext(
                m,
                modRoot,
                vfs,
                loc,
                world,
                sched,
                new GameHostServices(new KeyBindingStore()));

            Assert.Same(loc, ctx.Localization);
            Assert.Same(world, ctx.World);
            Assert.Same(sched, ctx.Scheduler);

            ctx.MountDefaultContent();
            ctx.MountContentSubfolder("Extra");

            Assert.True(vfs.Exists("x.bin"));
        }
        finally
        {
            Directory.Delete(modRoot, true);
        }
    }

    [Fact]
    public void ModLoader_returns_when_mods_root_missing()
    {
        var loader = new ModLoader();
        var vfs = new VirtualFileSystem();
        loader.LoadAll(
            Path.Combine(Path.GetTempPath(), "absent_" + Guid.NewGuid()),
            vfs,
            new LocalizationManager(),
            new World(),
            new SystemScheduler(new ParallelismSettings()),
            new GameHostServices(new KeyBindingStore()));

        Assert.Empty(loader.LoadedManifests);
    }

    [Fact]
    public void ModLoader_skips_invalid_manifests_and_loads_mods_in_order()
    {
        TestModEntry.ResetCounters();
        var modsRoot = Path.Combine(Path.GetTempPath(), "cyb mods " + Guid.NewGuid());
        Directory.CreateDirectory(modsRoot);

        Directory.CreateDirectory(Path.Combine(modsRoot, "empty"));
        Directory.CreateDirectory(Path.Combine(modsRoot, "badmanifest"));
        /* Valid JSON but missing id — ModLoader should skip */
        File.WriteAllText(Path.Combine(modsRoot, "badmanifest", "manifest.json"), """{"loadOrder":0}""");

        StageMod(modsRoot, "z_second", "z.mod", loadOrder: 10);
        StageMod(modsRoot, "a_first", "a.mod", loadOrder: 0);

        /* stale: invalid id */
        var badDir = Path.Combine(modsRoot, "noid");
        Directory.CreateDirectory(Path.Combine(badDir, "Content"));
        File.WriteAllText(Path.Combine(badDir, "manifest.json"), """{"id":"","entryAssembly":"Cyberland.TestMod.dll"}""");

        try
        {
            var vfs = new VirtualFileSystem();
            var loader = new ModLoader();
            loader.LoadAll(
                modsRoot,
                vfs,
                new LocalizationManager(),
                new World(),
                new SystemScheduler(new ParallelismSettings()),
                new GameHostServices(new KeyBindingStore()));

            Assert.Equal(2, TestModEntry.OnLoadCount);
            Assert.Equal(2, loader.LoadedManifests.Count);
            Assert.Equal("a.mod", loader.LoadedManifests[0].Id);
            Assert.Equal("z.mod", loader.LoadedManifests[1].Id);

            Assert.True(vfs.TryOpenRead("note.txt", out var stream));
            stream!.Dispose();
            using var reader = new StreamReader(File.OpenRead(
                Path.Combine(vfs.Roots[^1], "note.txt")));
            Assert.Equal("z.mod", reader.ReadToEnd());

            loader.UnloadAll();
            Assert.Equal(2, TestModEntry.OnUnloadCount);
            Assert.Empty(loader.LoadedManifests);
        }
        finally
        {
            Directory.Delete(modsRoot, true);
        }
    }

    [Fact]
    public void ModLoader_skips_assembly_when_entry_dropped_or_missing_file()
    {
        TestModEntry.ResetCounters();
        var modsRoot = Path.Combine(Path.GetTempPath(), "cyb mods skip " + Guid.NewGuid());
        Directory.CreateDirectory(modsRoot);
        StageMod(modsRoot, "noasm", "n", 0, includeDll: true, includeEntryAsmInManifest: false);

        var miss = Path.Combine(modsRoot, "miss");
        Directory.CreateDirectory(miss);
        File.WriteAllText(Path.Combine(miss, "manifest.json"), """{"id":"m","entryAssembly":"ghost.dll","contentRoot":"Content","loadOrder":0}""");

        try
        {
            var loader = new ModLoader();
            loader.LoadAll(
                modsRoot,
                new VirtualFileSystem(),
                new LocalizationManager(),
                new World(),
                new SystemScheduler(new ParallelismSettings()),
                new GameHostServices(new KeyBindingStore()));
            Assert.Equal(0, TestModEntry.OnLoadCount);
        }
        finally
        {
            Directory.Delete(modsRoot, true);
        }
    }

    [Fact]
    public void ModLoader_OnLoad_receives_host_reference()
    {
        TestModEntry.ResetCounters();
        var modsRoot = Path.Combine(Path.GetTempPath(), "cyb host " + Guid.NewGuid());
        StageMod(modsRoot, "one", "one.mod", 0);
        try
        {
            var keys = new KeyBindingStore();
            var host = new GameHostServices(keys);
            var loader = new ModLoader();
            loader.LoadAll(
                modsRoot,
                new VirtualFileSystem(),
                new LocalizationManager(),
                new World(),
                new SystemScheduler(new ParallelismSettings()),
                host);
            Assert.Same(host, TestModEntry.LastContext!.Host);
        }
        finally
        {
            Directory.Delete(modsRoot, true);
        }
    }
}
