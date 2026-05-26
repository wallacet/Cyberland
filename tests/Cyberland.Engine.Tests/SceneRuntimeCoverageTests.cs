using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.RuntimeScenes.Serialization;
using Cyberland.Engine.Scene;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Extra coverage for <see cref="SceneRuntime"/> edge paths (reflection only where the API is intentionally internal).
/// </summary>
public sealed class SceneRuntimeCoverageTests
{
    private static (GameHostServices Host, SceneRuntime Rt, World RootWorld, SystemScheduler RootSched) CreateRuntime(
        VirtualFileSystem vfs,
        ILocalizedContent? localized = null,
        ParallelismSettings? parallelism = null)
    {
        var host = new GameHostServices { LocalizedContent = localized };
        var rootWorld = new World();
        var rootSched = new SystemScheduler(parallelism ?? new ParallelismSettings());
        host.InitializeRuntimeScenes(vfs, parallelism ?? new ParallelismSettings(), () => localized, rootWorld, rootSched);
        return (host, host.RuntimeScenes!, rootWorld, rootSched);
    }

    [Fact]
    public void SceneInstanceId_ObjectEquals_HashCode_Operators()
    {
        var a = new SceneInstanceId(5);
        var b = new SceneInstanceId(5);
        var c = new SceneInstanceId(6);
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals((object)c));
        Assert.False(a.Equals(new object()));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a == b);
        Assert.True(a != c);
    }

    [Fact]
    public void SceneStateChangedEventArgs_ExposesFields()
    {
        var a = new SceneStateChangedEventArgs(new SceneInstanceId(2), SceneRuntimeState.Loading, SceneRuntimeState.Ready);
        Assert.Equal(new SceneInstanceId(2), a.Id);
        Assert.Equal(SceneRuntimeState.Loading, a.Previous);
        Assert.Equal(SceneRuntimeState.Ready, a.Current);
    }

    [Fact]
    public void SceneStatus_Deconstructs()
    {
        var s = new SceneStatus(new SceneInstanceId(1), SceneRuntimeState.Ready, 3, "p", "e");
        Assert.Equal("p", s.ScenePath);
        Assert.Equal("e", s.LastError);
    }

    [Fact]
    public void LocalizationManagerStringTable_DelegatesToManager()
    {
        var loc = new LocalizationManager();
        loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));
        loc.MergeJson("""{"k":"v"}"""u8.ToArray());
        var table = new LocalizationManagerStringTable(loc);
        Assert.True(table.TryGetString("k", out var v));
        Assert.Equal("v", v);
    }

    [Fact]
    public void GlobalSessionClock_TimeScaleClamps_AndAdvanceSkipsNonPositiveDelta()
    {
        var c = new GlobalSessionClock();
        c.TimeScale = -5f;
        Assert.Equal(0f, c.TimeScale);
        c.TimeScale = 2f;
        c.Advance(0f);
        c.Advance(-1f);
        Assert.Equal(0, c.SessionSeconds);
        c.Advance(1f);
        Assert.Equal(2, c.SessionSeconds, 5);
    }

    [Fact]
    public void InGameLoadProgress_Reset_AndNullKeyThrows()
    {
        var t = new InGameLoadProgressTracker();
        t.ReportPhaseProgress("x", 1f);
        t.Reset();
        Assert.Equal(0f, t.GetPhaseProgress("x"));
        Assert.Throws<ArgumentException>(() => t.ReportPhaseProgress("", 0.5f));
    }

    [Fact]
    public void SceneRuntime_BeginLoad_ValidationAndMaxScenes()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        Assert.Throws<ArgumentNullException>(() => rt.BeginLoad(null!));
        Assert.Throws<ArgumentException>(() => rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "  " }));
        rt.MaxAdditiveScenes = 1;
        _ = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/a.json" });
        Assert.Throws<InvalidOperationException>(() => rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/b.json" }));
    }

    [Fact]
    public async Task SceneRuntime_PumpAsync_RootUnknownAndTerminalPaths()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var bad = await rt.PumpAsync(new SceneInstanceId(999));
        Assert.True(bad.Failed);
        var rootPump = await rt.PumpAsync(SceneInstanceId.Root);
        Assert.True(rootPump.Failed);
        Assert.Equal("Cannot pump root scene.", rootPump.ErrorMessage);

        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_term_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "t.json"),
            """{"schemaVersion":1,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/t.json" });
        while (true)
        {
            var pr = await rt.PumpAsync(id);
            if (pr.Completed || pr.Failed)
                break;
        }

        var afterReady = await rt.PumpAsync(id);
        Assert.True(afterReady.Completed);
        Assert.False(afterReady.Failed);
        rt.RequestUnload(id);
        var afterUnload = await rt.PumpAsync(id);
        Assert.False(afterUnload.Completed);
    }

    [Fact]
    public void SceneRuntime_TryGetRootWorldAndScheduler()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        Assert.True(rt.TryGetWorld(SceneInstanceId.Root, out var w));
        Assert.True(rt.TryGetScheduler(SceneInstanceId.Root, out var s));
        Assert.NotNull(w);
        Assert.NotNull(s);
    }

    [Fact]
    public void SceneRuntime_TryGetSceneStatus_Enumerate_Missing()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        Assert.False(rt.TryGetSceneStatus(new SceneInstanceId(404), out _));
        var list = rt.EnumerateScenes();
        Assert.Contains(list, x => x.Id == SceneInstanceId.Root);
    }

    [Fact]
    public void SceneRuntime_RequestUnload_RootAndUnknown()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        Assert.False(rt.RequestUnload(SceneInstanceId.Root));
        Assert.False(rt.RequestUnload(new SceneInstanceId(999)));
    }

    [Fact]
    public async Task SceneRuntime_SceneStateChanged_Fires()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var transitions = new List<string>();
        rt.SceneStateChanged += (_, a) => transitions.Add($"{a.Previous}->{a.Current}");
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_evt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "e.json"),
            """{"schemaVersion":1,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/e.json" });
        while (true)
        {
            var pr = await rt.PumpAsync(id);
            if (pr.Completed || pr.Failed)
                break;
        }

        Assert.Contains(transitions, x => x.Contains(nameof(SceneRuntimeState.Ready)));
    }

    [Fact]
    public async Task SceneRuntime_RequestUnloadWhileLoading_Cancels()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_cancel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "c.json"),
            """{"schemaVersion":1,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/c.json" });
        Assert.Contains(rt.EnumerateScenes(), s => s.Id == id);
        Assert.True(rt.TryGetSceneStatus(id, out var st));
        Assert.Equal(SceneRuntimeState.Loading, st.State);
        Assert.True(rt.RequestUnload(id));
        var r = await rt.PumpAsync(id);
        Assert.False(r.Completed);
        Assert.False(r.Failed);
    }

    [Fact]
    public async Task SceneRuntime_RequestUnloadReady_ThenIdempotent()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_unl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "u.json"),
            """{"schemaVersion":1,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/u.json" });
        while (true)
        {
            var pr = await rt.PumpAsync(id);
            if (pr.Completed || pr.Failed)
                break;
        }

        Assert.True(rt.RequestUnload(id));
        Assert.False(rt.RequestUnload(id));
        Assert.False(rt.TryGetScheduler(id, out _));
    }

    [Fact]
    public async Task SceneRuntime_PumpAllAdditiveScenes_CompletesLoad()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_pall_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "p.json"),
            """{"schemaVersion":1,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/p.json" });
        rt.PumpAllAdditiveScenes();
        Assert.True(rt.TryGetSceneStatus(id, out var st));
        Assert.Equal(SceneRuntimeState.Ready, st.State);
    }

    [Fact]
    public async Task SceneRuntime_DeserializeNullDocument_Fails()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_null_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "n.json"), "null");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/n.json" });
        var r = await rt.PumpAsync(id);
        Assert.True(r.Failed);
    }

    [Fact]
    public async Task SceneRuntime_NewerSchemaWithAllowUnknown_FailsAtParseGate()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_new_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "new.json"),
            $$"""{"schemaVersion":{{SceneDocument.CurrentSchemaVersion + 1}},"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor
        {
            ScenePath = "Content/Scenes/new.json",
            AllowUnknownComponentTypes = true
        });
        var r = await rt.PumpAsync(id);
        Assert.True(r.Failed);
    }

    [Fact]
    public async Task SceneRuntime_AllowUnknownComponent_IgnoresMissingDeserializer()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_ign_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "i.json"),
            """
            {"schemaVersion":1,"entities":[{"components":[
              {"type":"cyberland.engine/transform","data":{"localX":1,"localY":2}},
              {"type":"cyberland.unknown/x","data":{}}
            ]}]}
            """);
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor
        {
            ScenePath = "Content/Scenes/i.json",
            AllowUnknownComponentTypes = true
        });
        while (true)
        {
            var pr = await rt.PumpAsync(id);
            if (pr.Completed || pr.Failed)
                break;
        }

        Assert.True(rt.TryGetWorld(id, out var w));
        AssertWorldHasTransformAt(w, 1f, 2f);
    }

    [Fact]
    public async Task SceneRuntime_LogicalActorComponentDeserializer_AppliesGuid()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_la_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "la.json"),
            """
            {"schemaVersion":1,"entities":[{"components":[
              {"type":"cyberland.engine/logical-actor","data":{"guid":"33333333-3333-3333-3333-333333333333"}},
              {"type":"cyberland.engine/transform","data":{"localX":0,"localY":0}}
            ]}]}
            """);
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/la.json" });
        while (true)
        {
            var pr = await rt.PumpAsync(id);
            if (pr.Completed || pr.Failed)
                break;
        }

        Assert.True(rt.TryGetWorld(id, out var w));
        AssertWorldHasLogicalActorGuid(w, "33333333-3333-3333-3333-333333333333");
    }

    [Fact]
    public async Task SceneRuntime_InvalidLogicalId_RollsBack()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_badla_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "badla.json"),
            """{"schemaVersion":1,"entities":[{"logicalId":"not-a-guid","components":[]}]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/badla.json" });
        var r = await rt.PumpAsync(id);
        Assert.True(r.Failed);
    }

    [Fact]
    public void SceneRuntime_RegisterComponentDeserializer_Validation()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        Assert.Throws<ArgumentException>(() => rt.RegisterComponentDeserializer(" ", static (in SceneComponentDeserializeContext _) => { }));
        Assert.Throws<ArgumentNullException>(() => rt.RegisterComponentDeserializer("a", null!));
    }

    [Fact]
    public void SceneRuntime_RegisterSchemaMigrator_NullThrows()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        Assert.Throws<ArgumentNullException>(() => rt.RegisterSchemaMigrator(1, 2, null!));
    }

    [Fact]
    public async Task SceneRuntime_MissingMigrator_ThrowsWrappedAsFailedLoad()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        ClearMigrators(rt);
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        rt.RegisterSchemaMigrator(0, 1, root =>
        {
            var doc = JsonSerializer.Deserialize<SceneDocument>(root.GetRawText(), jsonOpts) ?? new SceneDocument();
            doc.SchemaVersion = 1;
            return JsonSerializer.SerializeToElement(doc, jsonOpts);
        });

        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_mig_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "m.json"),
            """{"schemaVersion":0,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/m.json" });
        var r = await rt.PumpAsync(id);
        Assert.True(r.Failed);
        Assert.Contains("Missing migrator", r.ErrorMessage ?? "");
    }

    [Fact]
    public async Task SceneRuntime_PumpWhileUnloading_ReturnsEmptySlice()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_unldp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "w.json"),
            """{"schemaVersion":1,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/w.json" });
        while (true)
        {
            var pr = await rt.PumpAsync(id);
            if (pr.Completed || pr.Failed)
                break;
        }

        var entry = GetFirstAdditiveEntry(rt);
        entry.State = SceneRuntimeState.Unloading;
        var r = await rt.PumpAsync(id);
        Assert.False(r.Completed);
        Assert.False(r.Failed);
    }

    [Fact]
    public void EntityWorldTransfer_OrderParentsBeforeChildren_sorts_by_depth()
    {
        var w = new World();
        var root = w.CreateEntity();
        ref var tr = ref w.GetOrAdd<Transform>(root);
        tr = Transform.Identity;
        var c1 = w.CreateEntity();
        ref var t1 = ref w.GetOrAdd<Transform>(c1);
        t1 = Transform.Identity;
        t1.Parent = root;
        var c2 = w.CreateEntity();
        ref var t2 = ref w.GetOrAdd<Transform>(c2);
        t2 = Transform.Identity;
        t2.Parent = c1;
        var list = new List<EntityId> { c2, root, c1 };
        var ordered = EntityWorldTransfer.OrderParentsBeforeChildren(w, list);
        Assert.Equal(root, ordered[0]);
        Assert.Equal(c2, ordered[^1]);
    }

    [Fact]
    public void EntityWorldTransfer_CollectSubtree_NotAlive_IsEmpty()
    {
        var w = new World();
        var dead = new EntityId(999999);
        Assert.Empty(EntityWorldTransfer.CollectSubtree(w, dead));
    }

    [Fact]
    public void SceneRuntime_TryLiftSubtree_NoTransformOnRoot_Fails()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var a = new World();
        var b = new World();
        var root = a.CreateEntity();
        Assert.False(rt.TryLiftSubtree(a, root, b, out _, out _));
    }

    [Fact]
    public void SceneRuntime_TryLiftEntity_DeadSource_Fails()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var a = new World();
        var b = new World();
        var dead = new EntityId(999999);
        Assert.False(rt.TryLiftEntity(a, dead, b, out _, out _));
    }

    [Fact]
    public void SceneAssetRequestQueue_EnqueueNullThrows_Clear_DrainsByteBudget()
    {
        var q = new SceneAssetRequestQueue();
        Assert.Throws<ArgumentNullException>(() => q.Enqueue(0, null!));
        q.Enqueue(0, () => { }, byteBudgetHint: 5);
        q.Enqueue(0, () => { }, byteBudgetHint: 5);
        Assert.Equal(1, q.Drain(maxJobs: 10, maxDecodeBytes: 5));
        q.Enqueue(0, () => { });
        q.Clear();
        Assert.Equal(0, q.Drain(5, 999));
    }

    [Fact]
    public void SceneAssetRequestQueue_JobCompareToNull_IsGreater()
    {
        var job = new SceneAssetRequestQueue.Job { Priority = 0, Action = () => { }, ByteBudgetHint = 0 };
        Assert.True(job.CompareTo(null) > 0);
    }

    [Fact]
    public async Task SceneRuntime_MultiSliceSpawn_MaxElapsed()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_slice_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        var ents = string.Join(',', Enumerable.Range(0, 8).Select(_ => """{"components":[{"type":"cyberland.engine/transform","data":{}}]}"""));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "big.json"),
            $$"""{"schemaVersion":1,"entities":[{{ents}}]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/big.json" });
        var opts = new SceneLoadPumpOptions { MaxEntitiesToCommit = 1 };
        var completed = false;
        for (var i = 0; i < 50; i++)
        {
            var r = await rt.PumpAsync(id, opts);
            if (r.Failed)
                Assert.Fail(r.ErrorMessage ?? "pump failed");
            if (r.Completed)
            {
                completed = true;
                break;
            }
        }

        Assert.True(completed);
    }

    [Fact]
    public void SceneComponentDeserializeContext_PropertiesRoundtrip()
    {
        var w = new World();
        var e = w.CreateEntity();
        using var doc = JsonDocument.Parse("""{"x":1}""");
        var ctx = new SceneComponentDeserializeContext(w, e, new SceneInstanceId(1), doc.RootElement.Clone(), null);
        Assert.Equal(w, ctx.World);
        Assert.Equal(e, ctx.EntityId);
        Assert.Equal(new SceneInstanceId(1), ctx.SceneId);
        Assert.Null(ctx.Strings);
    }

    [Fact]
    public void SceneRuntime_CreateRuntime_ZeroMaxParallelism_UsesProcessorCount()
    {
        var vfs = new VirtualFileSystem();
        var par = new ParallelismSettings { MaxConcurrency = 0 };
        var (_, rt, _, _) = CreateRuntime(vfs, null, par);
        rt.MaxAdditiveScenes = 0;
        _ = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/x.json" });
        Assert.True(rt.TryGetScheduler(rt.EnumerateScenes().First(x => x.Id != SceneInstanceId.Root).Id, out var sched));
        var f = typeof(SystemScheduler).GetField("_parallelism", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var inner = (ParallelismSettings)f.GetValue(sched)!;
        Assert.True(inner.MaxConcurrency > 0);
    }

    [Fact]
    public void LogicalActorId_HasValue_tracks_nonempty_guid()
    {
        var a = new LogicalActorId();
        Assert.False(a.HasValue);
        a.Guid = "";
        Assert.False(a.HasValue);
        a.Guid = "00000000-0000-0000-0000-000000000001";
        Assert.True(a.HasValue);
    }

    [Fact]
    public void ModLoadContext_Scenes_reflects_host_runtime_after_init()
    {
        var host = new GameHostServices();
        var ctx = new ModLoadContext(
            new ModManifest { Id = "t", ContentRoot = "Content" },
            Path.GetTempPath(),
            new VirtualFileSystem(),
            new LocalizedContent(new LocalizationManager(), new VirtualFileSystem(), "en"),
            new World(),
            new SystemScheduler(new ParallelismSettings()),
            host);
        Assert.Null(ctx.Scenes);
        var root = new World();
        var sched = new SystemScheduler(new ParallelismSettings());
        host.InitializeRuntimeScenes(new VirtualFileSystem(), new ParallelismSettings(), () => null, root, sched);
        Assert.NotNull(ctx.Scenes);
        Assert.Same(host.RuntimeScenes, ctx.Scenes);
    }

    [Fact]
    public void SceneLoadDtos_expose_all_surface_fields()
    {
        var d = new SceneLoadDescriptor
        {
            ScenePath = "a.json",
            Priority = 2,
            AllowUnknownComponentTypes = true,
            MaxEntitiesPerPump = 3
        };
        Assert.Equal("a.json", d.ScenePath);
        Assert.Equal(2, d.Priority);
        Assert.True(d.AllowUnknownComponentTypes);
        Assert.Equal(3, d.MaxEntitiesPerPump);

        var o = new SceneLoadPumpOptions
        {
            MaxElapsed = TimeSpan.FromSeconds(1),
            MaxReadBytes = 9,
            MaxEntitiesToCommit = 7,
            MaxAssetJobs = 2,
            MaxAssetDecodeBytes = 11
        };
        Assert.Equal(TimeSpan.FromSeconds(1), o.MaxElapsed);
        Assert.Equal(9, o.MaxReadBytes);
        Assert.Equal(7, o.MaxEntitiesToCommit);
        Assert.Equal(2, o.MaxAssetJobs);
        Assert.Equal(11, o.MaxAssetDecodeBytes);

        var r = new SceneLoadResult
        {
            JobsStarted = 1,
            JobsCompleted = 2,
            BytesDecoded = 3,
            EntitiesCommitted = 4,
            Warnings = 5,
            Completed = true,
            Failed = false,
            ErrorMessage = "ok"
        };
        Assert.Equal(1, r.JobsStarted);
        Assert.Equal(2, r.JobsCompleted);
        Assert.Equal(3, r.BytesDecoded);
        Assert.Equal(4, r.EntitiesCommitted);
        Assert.Equal(5, r.Warnings);
        Assert.True(r.Completed);
        Assert.False(r.Failed);
        Assert.Equal("ok", r.ErrorMessage);

        var st = new SceneStatus(new SceneInstanceId(3), SceneRuntimeState.Ready, 1, "p", "err");
        Assert.Equal(new SceneInstanceId(3), st.Id);
        Assert.Equal(SceneRuntimeState.Ready, st.State);
        Assert.Equal(1, st.Priority);
        Assert.Equal("p", st.ScenePath);
        Assert.Equal("err", st.LastError);
    }

    [Fact]
    public async Task SceneRuntime_NewerSchema_without_allow_unknown_fails()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_future_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "f.json"),
            $$"""{"schemaVersion":{{SceneDocument.CurrentSchemaVersion + 5}},"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/f.json", AllowUnknownComponentTypes = false });
        var r = await rt.PumpAsync(id);
        Assert.True(r.Failed);
        Assert.Contains("Unsupported newer schemaVersion", r.ErrorMessage ?? "");
    }

    [Fact]
    public void GlobalSessionClock_Paused_getter_reads_backing_field()
    {
        var c = new GlobalSessionClock();
        Assert.False(c.Paused);
        c.Paused = true;
        Assert.True(c.Paused);
    }

    [Fact]
    public void SceneRuntime_without_initialize_root_TryGet_fails()
    {
        var host = new GameHostServices();
        var rt = new SceneRuntime(host, new VirtualFileSystem(), new ParallelismSettings(), () => null);
        Assert.False(rt.TryGetWorld(SceneInstanceId.Root, out _));
        Assert.False(rt.TryGetScheduler(SceneInstanceId.Root, out _));
        Assert.False(rt.TryGetSceneStatus(SceneInstanceId.Root, out _));
    }

    [Fact]
    public async Task SceneRuntime_PumpAsync_non_positive_MaxElapsed_has_no_wall_clock_cap()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_slice2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "s.json"),
            """{"schemaVersion":1,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/s.json" });
        var opts = new SceneLoadPumpOptions { MaxElapsed = TimeSpan.FromMilliseconds(-1) };
        var n = 0;
        SceneLoadResult r;
        do
        {
            r = await rt.PumpAsync(id, opts);
            n++;
        } while (!r.Completed && !r.Failed && n < 200);

        Assert.True(r.Completed || r.Failed);
    }

    [Fact]
    public async Task SceneRuntime_PumpAsync_positive_wall_clock_can_slice_before_parsing()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_slice3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "s3.json"),
            """{"schemaVersion":1,"entities":[]}""");
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/s3.json" });
        var opts = new SceneLoadPumpOptions { MaxElapsed = TimeSpan.FromTicks(1) };
        var r1 = await rt.PumpAsync(id, opts);
        Assert.False(r1.Completed);
        Assert.False(r1.Failed);
        for (var j = 0; j < 20; j++)
        {
            var r2 = await rt.PumpAsync(id);
            if (r2.Completed || r2.Failed)
            {
                Assert.True(r2.Completed);
                return;
            }
        }

        Assert.Fail("Expected scene load to complete.");
    }

    [Fact]
    public async Task SceneRuntime_PumpAsync_wall_clock_slices_after_parse_before_spawn()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_slice4_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        // Large JSON DOM so JsonDocument.Parse exceeds a 1 ms wall budget on fast CI runners (parsing is not incremental).
        var pad = new string('z', 8_000_000);
        var json = "{\"schemaVersion\":1,\"padding\":\"" + pad + "\",\"entities\":[{\"components\":[{\"type\":\"cyberland.engine/transform\",\"data\":{}}]}]}";
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "s4.json"), json);
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/s4.json" });
        var r1 = await rt.PumpAsync(id, new SceneLoadPumpOptions { MaxElapsed = TimeSpan.FromTicks(1) });
        Assert.False(r1.Completed);
        var r2 = await rt.PumpAsync(id, new SceneLoadPumpOptions { MaxElapsed = TimeSpan.FromMilliseconds(1) });
        Assert.False(r2.Completed);
        Assert.Equal(0, r2.EntitiesCommitted);
        var r3 = await rt.PumpAsync(id);
        Assert.True(r3.Completed);
    }

    [Fact]
    public async Task SceneRuntime_PumpAsync_wall_clock_breaks_inside_spawn_loop()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        rt.RegisterComponentDeserializer("cyberland.engine.tests/slow-comp", static (in SceneComponentDeserializeContext _) =>
        {
            Thread.Sleep(10);
        });
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_slice5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "s5.json"),
            """
            {"schemaVersion":1,"entities":[
              {"components":[{"type":"cyberland.engine/transform","data":{}}]},
              {"components":[
                {"type":"cyberland.engine.tests/slow-comp","data":{}},
                {"type":"cyberland.engine/transform","data":{}}
              ]},
              {"components":[{"type":"cyberland.engine/transform","data":{}}]}
            ]}
            """);
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/s5.json" });
        var p1 = await rt.PumpAsync(id, new SceneLoadPumpOptions { MaxEntitiesToCommit = 1 });
        Assert.False(p1.Completed);
        Assert.False(p1.Failed);
        var slice = await rt.PumpAsync(id, new SceneLoadPumpOptions
        {
            MaxElapsed = TimeSpan.FromMilliseconds(5),
            MaxEntitiesToCommit = 4
        });
        Assert.False(slice.Completed);
        Assert.False(slice.Failed);
        Assert.True(slice.EntitiesCommitted >= 1);
        var done = await rt.PumpAsync(id);
        Assert.True(done.Completed);
    }

    [Fact]
    public void SceneRuntime_TryLiftEntity_single_entity_succeeds()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var w = new World();
        var d = new World();
        var e = w.CreateEntity();
        w.GetOrAdd<Transform>(e) = Transform.Identity;
        Assert.True(rt.TryLiftEntity(w, e, d, out var dst, out var map));
        Assert.True(map.ContainsKey(e));
        Assert.Equal(dst, map[e]);
        Assert.True(d.IsAlive(dst));
    }

    [Fact]
    public async Task SceneRuntime_spawn_loop_sees_cancellation_token_without_request_unload()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_cx_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "cx.json"),
            """
            {"schemaVersion":1,"entities":[
              {"components":[{"type":"cyberland.engine/transform","data":{}}]},
              {"components":[{"type":"cyberland.engine/transform","data":{}}]}
            ]}
            """);
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/cx.json" });
        _ = await rt.PumpAsync(id, new SceneLoadPumpOptions { MaxEntitiesToCommit = 1 });
        GetFirstAdditiveEntry(rt).Cancellation.Cancel();
        var r2 = await rt.PumpAsync(id);
        Assert.True(r2.Failed);
        Assert.Equal("Cancelled.", r2.ErrorMessage);
    }

    [Fact]
    public void SceneRuntime_TryLiftEntities_two_disjoint_roots()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var w = new World();
        var d = new World();
        var r1 = w.CreateEntity();
        ref var t1 = ref w.GetOrAdd<Transform>(r1);
        t1 = Transform.Identity;
        var r2 = w.CreateEntity();
        ref var t2 = ref w.GetOrAdd<Transform>(r2);
        t2 = Transform.Identity;
        Span<EntityId> roots = stackalloc EntityId[2];
        roots[0] = r1;
        roots[1] = r2;
        Assert.True(rt.TryLiftEntities(w, roots, d, out var map));
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void EntityWorldTransfer_OrderParentsBeforeChildren_depth_breaks_outside_member_set()
    {
        var w = new World();
        var root = w.CreateEntity();
        ref var tr = ref w.GetOrAdd<Transform>(root);
        tr = Transform.Identity;
        var mid = w.CreateEntity();
        ref var tm = ref w.GetOrAdd<Transform>(mid);
        tm = Transform.Identity;
        tm.Parent = root;
        var leaf = w.CreateEntity();
        ref var tl = ref w.GetOrAdd<Transform>(leaf);
        tl = Transform.Identity;
        tl.Parent = mid;
        var ordered = EntityWorldTransfer.OrderParentsBeforeChildren(w, new List<EntityId> { leaf });
        Assert.Single(ordered);
        Assert.Equal(leaf, ordered[0]);
    }

    [Fact]
    public void EntityWorldTransfer_IsUnderRoot_returns_false_for_unrelated_root()
    {
        var w = new World();
        var root = w.CreateEntity();
        ref var tr = ref w.GetOrAdd<Transform>(root);
        tr = Transform.Identity;
        var leaf = w.CreateEntity();
        ref var tl = ref w.GetOrAdd<Transform>(leaf);
        tl = Transform.Identity;
        tl.Parent = root;
        var other = w.CreateEntity();
        ref var to = ref w.GetOrAdd<Transform>(other);
        to = Transform.Identity;
        var m = typeof(EntityWorldTransfer).GetMethod("IsUnderRoot", BindingFlags.Static | BindingFlags.NonPublic)
                  ?? throw new InvalidOperationException("IsUnderRoot");
        Assert.True((bool)m.Invoke(null, new object[] { w, leaf, root })!);
        Assert.False((bool)m.Invoke(null, new object[] { w, leaf, other })!);
    }

    [Fact]
    public void EntityWorldTransfer_IsUnderRoot_false_when_transform_parent_destroyed()
    {
        var w = new World();
        var root = w.CreateEntity();
        ref var tr = ref w.GetOrAdd<Transform>(root);
        tr = Transform.Identity;
        var parent = w.CreateEntity();
        ref var tp = ref w.GetOrAdd<Transform>(parent);
        tp = Transform.Identity;
        tp.Parent = root;
        var child = w.CreateEntity();
        ref var tc = ref w.GetOrAdd<Transform>(child);
        tc = Transform.Identity;
        tc.Parent = parent;
        w.DestroyEntity(parent);
        var m = typeof(EntityWorldTransfer).GetMethod("IsUnderRoot", BindingFlags.Static | BindingFlags.NonPublic)
                  ?? throw new InvalidOperationException("IsUnderRoot");
        Assert.False((bool)m.Invoke(null, new object[] { w, child, root })!);
    }

    [Fact]
    public void EntityWorldTransfer_TryCopyEngineComponents_copies_light_components()
    {
        var src = new World();
        var dst = new World();
        var e = src.CreateEntity();
        src.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var pt = ref src.GetOrAdd<PointLightSource>(e);
        pt.Intensity = 5f;
        pt.Active = true;
        ref var dir = ref src.GetOrAdd<DirectionalLightSource>(e);
        dir.Intensity = 2f;
        dir.Active = true;
        ref var spot = ref src.GetOrAdd<SpotLightSource>(e);
        spot.Radius = 10f;
        spot.Active = true;
        ref var amb = ref src.GetOrAdd<AmbientLightSource>(e);
        amb.Intensity = 1f;
        amb.Active = true;
        var d = dst.CreateEntity();
        dst.GetOrAdd<Transform>(d) = Transform.Identity;
        EntityWorldTransfer.TryCopyEngineComponents(src, e, dst, d);
        Assert.True(dst.Has<PointLightSource>(d));
        Assert.Equal(5f, dst.Get<PointLightSource>(d).Intensity);
        Assert.True(dst.Has<DirectionalLightSource>(d));
        Assert.Equal(2f, dst.Get<DirectionalLightSource>(d).Intensity);
        Assert.True(dst.Has<SpotLightSource>(d));
        Assert.Equal(10f, dst.Get<SpotLightSource>(d).Radius);
        Assert.True(dst.Has<AmbientLightSource>(d));
        Assert.Equal(1f, dst.Get<AmbientLightSource>(d).Intensity);
    }

    [Fact]
    public void EntityWorldTransfer_IsUnderRoot_false_when_parent_chain_cycles()
    {
        var w = new World();
        var unrelated = w.CreateEntity();
        _ = w.GetOrAdd<Transform>(unrelated);
        var a = w.CreateEntity();
        ref var ta = ref w.GetOrAdd<Transform>(a);
        ta = Transform.Identity;
        var b = w.CreateEntity();
        ref var tb = ref w.GetOrAdd<Transform>(b);
        tb = Transform.Identity;
        ta.Parent = b;
        tb.Parent = a;
        var m = typeof(EntityWorldTransfer).GetMethod("IsUnderRoot", BindingFlags.Static | BindingFlags.NonPublic)
                  ?? throw new InvalidOperationException("IsUnderRoot");
        Assert.False((bool)m.Invoke(null, new object[] { w, a, unrelated })!);
    }

    [Fact]
    public async Task SceneRuntime_logical_actor_deserializer_skips_invalid_guid()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_scene_labad_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "badla2.json"),
            """
            {"schemaVersion":1,"entities":[{"components":[
              {"type":"cyberland.engine/logical-actor","data":{"guid":""}},
              {"type":"cyberland.engine/transform","data":{}}
            ]}]}
            """);
        vfs.Mount(dir);
        var id = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/badla2.json" });
        while (true)
        {
            var pr = await rt.PumpAsync(id);
            if (pr.Completed || pr.Failed)
                break;
        }

        Assert.True(rt.TryGetWorld(id, out var world));
        var anyLogical = false;
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<LogicalActorId>()))
        {
            if (chunk.Count > 0)
                anyLogical = true;
        }

        Assert.False(anyLogical);
    }

    [Fact]
    public void SceneRuntime_DestroyAllEntities_reflection_hits_alive_branch()
    {
        var vfs = new VirtualFileSystem();
        var (_, rt, _, _) = CreateRuntime(vfs);
        var w = new World();
        var e = w.CreateEntity();
        w.GetOrAdd<Transform>(e) = Transform.Identity;
        var m = typeof(SceneRuntime).GetMethod("DestroyAllEntities", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("DestroyAllEntities");
        m.Invoke(rt, new object[] { w });
        Assert.False(w.IsAlive(e));
    }

    private static void ClearMigrators(SceneRuntime rt)
    {
        var f = typeof(SceneRuntime).GetField("_migrators", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_migrators");
        var dict = (System.Collections.IDictionary)f.GetValue(rt)!;
        dict.Clear();
    }

    [Fact]
    public void SceneStreamingPolicy_Anchor_IsStable()
    {
        Assert.Equal(nameof(SceneStreamingPolicy), SceneStreamingPolicy.Anchor);
    }

    private static void AssertWorldHasLogicalActorGuid(World w, string guid)
    {
        var found = false;
        foreach (var chunk in w.QueryChunks(SystemQuerySpec.All<LogicalActorId>()))
        {
            var col = chunk.Column<LogicalActorId>();
            for (var i = 0; i < col.Length; i++)
            {
                Assert.Equal(guid, col[i].Guid);
                found = true;
            }
        }

        Assert.True(found);
    }

    private static void AssertWorldHasTransformAt(World w, float expectX, float expectY)
    {
        var found = false;
        foreach (var chunk in w.QueryChunks(SystemQuerySpec.All<Transform>()))
        {
            var col = chunk.Column<Transform>();
            for (var i = 0; i < col.Length; i++)
            {
                var t = col[i];
                Assert.Equal(expectX, t.LocalPosition.X, 5);
                Assert.Equal(expectY, t.LocalPosition.Y, 5);
                found = true;
            }
        }

        Assert.True(found);
    }

    private static SceneRuntime.AdditiveSceneEntry GetFirstAdditiveEntry(SceneRuntime rt)
    {
        var f = typeof(SceneRuntime).GetField("_additive", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_additive");
        var list = (List<SceneRuntime.AdditiveSceneEntry>)f.GetValue(rt)!;
        return list[0];
    }
}
