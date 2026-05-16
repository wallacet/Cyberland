using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.RuntimeScenes.Serialization;
using Cyberland.Engine.Scene;

namespace Cyberland.Engine.Tests;

public sealed class SceneRuntimeTests
{
    private static (GameHostServices Host, SceneRuntime Rt, World RootWorld, SystemScheduler RootSched) CreateRuntime(
        VirtualFileSystem vfs,
        ILocalizedContent? localized = null)
    {
        var host = new GameHostServices { LocalizedContent = localized };
        var rootWorld = new World();
        var rootSched = new SystemScheduler(new ParallelismSettings());
        host.InitializeRuntimeScenes(vfs, new ParallelismSettings(), () => localized, rootWorld, rootSched);
        return (host, host.RuntimeScenes!, rootWorld, rootSched);
    }

    [Fact]
    public void SceneRuntime_OrderAndPriority_LowerPriorityTicksFirstInEnumerationOrder()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        rt.MaxAdditiveScenes = 0;

        var a = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/a.json", Priority = 10 });
        var b = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/b.json", Priority = 0 });
        var list = rt.GetAdditiveScenesForTick();
        Assert.True(list.Count >= 2);
        Assert.Equal(b, list[0].Id);
        Assert.Equal(a, list[1].Id);
    }

    [Fact]
    public async Task SceneRuntime_LoadRollback_OnUnknownComponent_FailsAndLeavesNoEntities()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        var json = """
                   {"schemaVersion":1,"entities":[{"components":[{"type":"cyberland.unknown/missing","data":{}}]}]}
                   """;
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "bad.json"), json);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);

        var (_, rt, _, _) = CreateRuntime(vfs);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/bad.json" });
        var r = await rt.PumpAsync(id);
        Assert.True(r.Failed);
        Assert.False(rt.TryGetWorld(id, out _));
    }

    [Fact]
    public async Task SceneRuntime_StateMachine_ReachesReady()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_ok_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        var json = """
                   {"schemaVersion":1,"entities":[{"logicalId":"11111111-1111-1111-1111-111111111111","components":[{"type":"cyberland.engine/transform","data":{"localX":3,"localY":4}}]}]}
                   """;
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "ok.json"), json);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);

        var (_, rt, _, _) = CreateRuntime(vfs);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/ok.json" });
        SceneLoadResult last = new();
        for (var i = 0; i < 20; i++)
        {
            last = await rt.PumpAsync(id, new SceneLoadPumpOptions { MaxEntitiesToCommit = 4 });
            if (last.Completed)
                break;
        }

        Assert.True(last.Completed);
        Assert.True(rt.TryGetSceneStatus(id, out var st));
        Assert.Equal(SceneRuntimeState.Ready, st.State);
        Assert.True(rt.TryGetWorld(id, out var w));
        AssertSceneHasLogicalActorWithTransform(w);
    }

    private static void AssertSceneHasLogicalActorWithTransform(World w)
    {
        var found = false;
        foreach (var chunk in w.QueryChunks(SystemQuerySpec.All<Transform, LogicalActorId>()))
        {
            var ents = chunk.Entities;
            var transforms = chunk.Column<Transform>();
            var logicals = chunk.Column<LogicalActorId>();
            for (var i = 0; i < ents.Length; i++)
            {
                var t = transforms[i];
                Assert.Equal(3f, t.LocalPosition.X, 5);
                Assert.Equal(4f, t.LocalPosition.Y, 5);
                var la = logicals[i];
                Assert.Equal("11111111-1111-1111-1111-111111111111", la.Guid);
                found = true;
            }
        }

        Assert.True(found);
    }

    [Fact]
    public void SceneRuntime_AssetQueueBudget_RespectsMaxJobs()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/x.json", Priority = 0 });
        Assert.True(rt.TryGetSceneStatus(id, out var st));
        var entry = rt.GetAdditiveScenesForTick().First(e => e.Id == id);
        var q = entry.Queue;
        var runs = 0;
        for (var i = 0; i < 10; i++)
            q.Enqueue(0, () => runs++);
        var drained = q.Drain(maxJobs: 3, maxDecodeBytes: 99999);
        Assert.Equal(3, drained);
        Assert.Equal(3, runs);
    }

    [Fact]
    public void EntityLift_RemapAndHierarchy_PreservesLogicalActor()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var a = new World();
        var b = new World();
        var root = a.CreateEntity();
        ref var t = ref a.GetOrAdd<Transform>(root);
        t = Transform.Identity;
        var child = a.CreateEntity();
        ref var tc = ref a.GetOrAdd<Transform>(child);
        tc = Transform.Identity;
        tc.Parent = root;
        ref var la = ref a.GetOrAdd<LogicalActorId>(child);
        la.Guid = "22222222-2222-2222-2222-222222222222";

        Assert.True(rt.TryLiftSubtree(a, root, b, out var newRoot, out var map));
        Assert.True(map.TryGetValue(root, out var nr));
        Assert.True(map.TryGetValue(child, out var nc));
        ref readonly var tb = ref b.Get<Transform>(nc);
        Assert.Equal(nr, tb.Parent);
        Assert.Equal("22222222-2222-2222-2222-222222222222", b.Get<LogicalActorId>(nc).Guid);
        Assert.False(a.IsAlive(root));
        Assert.False(a.IsAlive(child));
        Assert.Equal(newRoot, nr);
    }

    [Fact]
    public void MultiWorld_GlobalClockSingleAuthority_Advances()
    {
        var host = new GameHostServices();
        host.SessionClock.Advance(0.1f);
        Assert.InRange(host.SessionClock.SessionSeconds, 0.09, 0.11);
        host.SessionClock.Paused = true;
        host.SessionClock.Advance(1f);
        Assert.InRange(host.SessionClock.SessionSeconds, 0.09, 0.11);
    }

    [Fact]
    public void InGameLoadProgress_TracksPhase()
    {
        var t = new InGameLoadProgressTracker();
        t.ReportPhaseProgress("a", 0.3f);
        t.ReportPhaseProgress("a", 0.5f);
        Assert.Equal(0.5f, t.GetPhaseProgress("a"));
    }

    [Fact]
    public async Task SceneRuntime_SpawnIntoWorldAsync_Validation()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, rootWorld, _) = CreateRuntime(vfs);
        await Assert.ThrowsAsync<ArgumentNullException>(() => rt.SpawnIntoWorldAsync(null!, "x.json").AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => rt.SpawnIntoWorldAsync(rootWorld, "  ").AsTask());
    }

    [Fact]
    public async Task SceneRuntime_SpawnIntoWorldAsync_ParentLogicalIdAndRotation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_spawn_root_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        var json = """
                   {"schemaVersion":1,"entities":[
                     {"logicalId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","components":[
                       {"type":"cyberland.engine/transform","data":{"localX":10,"localY":20}}
                     ]},
                     {"logicalId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","components":[
                       {"type":"cyberland.engine/transform","data":{"parentLogicalId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","localRotationRadians":1.25}}
                     ]}
                   ]}
                   """;
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "root.json"), json);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var (_, rt, rootWorld, _) = CreateRuntime(vfs);

        var result = await rt.SpawnIntoWorldAsync(rootWorld, "Content/Scenes/root.json");
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.EntitiesSpawned);
        AssertSpawnRootParentLink(rootWorld);
    }

    private static void AssertSpawnRootParentLink(World rootWorld)
    {
        EntityId child = default;
        var found = false;
        foreach (var chunk in rootWorld.QueryChunks(SystemQuerySpec.All<Transform, LogicalActorId>()))
        {
            var ents = chunk.Entities;
            var logicals = chunk.Column<LogicalActorId>();
            var transforms = chunk.Column<Transform>();
            for (var i = 0; i < ents.Length; i++)
            {
                if (logicals[i].Guid != "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                    continue;
                child = ents[i];
                Assert.Equal(1.25f, transforms[i].LocalRotationRadians, 4);
                found = true;
            }
        }

        Assert.True(found);
        Assert.True(rootWorld.TryGet(child, out Transform tc));
        Assert.True(rootWorld.TryGet(tc.Parent, out Transform tp));
        Assert.Equal(10f, tp.LocalPosition.X, 4);
        Assert.Equal(20f, tp.LocalPosition.Y, 4);
    }

    [Fact]
    public async Task SceneRuntime_SpawnIntoWorldAsync_FailsWhenSceneFileMissing()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, rootWorld, _) = CreateRuntime(vfs);
        var result = await rt.SpawnIntoWorldAsync(rootWorld, "Content/Scenes/missing.json");
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SceneRuntime_SpawnIntoWorldAsync_FailsOnNewerSchema()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_spawn_new_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "new.json"),
            $$"""{"schemaVersion":{{SceneDocument.CurrentSchemaVersion + 1}},"entities":[]}""");
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var (_, rt, rootWorld, _) = CreateRuntime(vfs);
        var result = await rt.SpawnIntoWorldAsync(
            rootWorld,
            "Content/Scenes/new.json",
            new SceneSpawnOptions { AllowUnknownComponentTypes = true });
        Assert.False(result.Succeeded);
        Assert.Contains("Unsupported newer schemaVersion", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task SceneRuntime_SpawnIntoWorldAsync_RollbackOnBadParentLogicalId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_spawn_parent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "badparent.json"),
            """
            {"schemaVersion":1,"entities":[{"components":[
              {"type":"cyberland.engine/transform","data":{"parentLogicalId":"cccccccc-cccc-cccc-cccc-cccccccccccc"}}
            ]}]}
            """);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var (_, rt, rootWorld, _) = CreateRuntime(vfs);
        var result = await rt.SpawnIntoWorldAsync(rootWorld, "Content/Scenes/badparent.json");
        Assert.False(result.Succeeded);
        Assert.Contains("Unknown parentLogicalId", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task SceneRuntime_SpawnIntoWorldAsync_RollbackOnUnknownComponent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_spawn_bad_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "bad.json"),
            """{"schemaVersion":1,"entities":[{"components":[{"type":"cyberland.unknown/x","data":{}}]}]}""");
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var (_, rt, rootWorld, _) = CreateRuntime(vfs);
        var result = await rt.SpawnIntoWorldAsync(rootWorld, "Content/Scenes/bad.json");
        Assert.False(result.Succeeded);
        var any = false;
        foreach (var _ in rootWorld.QueryChunks(SystemQuerySpec.All<Transform>()))
            any = true;
        Assert.False(any);
    }
}
