using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.RuntimeScenes.Serialization;
using Cyberland.Engine.Scene;

namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Default implementation of <see cref="ISceneRuntime"/>: additive worlds, load pump, lift, and asset queue.
/// </summary>
public sealed class SceneRuntime : ISceneRuntime
{
    private readonly GameHostServices _host;
    private readonly VirtualFileSystem _vfs;
    private readonly ParallelismSettings _rootParallelism;
    private readonly Func<ILocalizedContent?> _getLocalizedContent;
    private readonly Dictionary<string, SceneComponentDeserializer> _deserializers = new(StringComparer.Ordinal);
    private readonly Dictionary<(int From, int To), SceneSchemaMigrator> _migrators = new();
    private readonly object _gate = new();

    private World? _rootWorld;
    private SystemScheduler? _rootScheduler;
    private ulong _nextSceneId = 1;
    private readonly List<AdditiveSceneEntry> _additive = new();
    private bool _engineDeserializersRegistered;

    /// <summary>Constructs runtime bound to host services and VFS.</summary>
    public SceneRuntime(
        GameHostServices host,
        VirtualFileSystem vfs,
        ParallelismSettings rootParallelism,
        Func<ILocalizedContent?> getLocalizedContent)
    {
        _host = host;
        _vfs = vfs;
        _rootParallelism = rootParallelism;
        _getLocalizedContent = getLocalizedContent;
        RegisterBuiltinDeserializers();
        RegisterBuiltinSchemaMigrators();
    }

    /// <inheritdoc />
    public event EventHandler<SceneStateChangedEventArgs>? SceneStateChanged;

    /// <inheritdoc />
    public int MaxAdditiveScenes { get; set; }

    /// <summary>Registers the process root world pair (must be called once).</summary>
    public void InitializeRoot(World world, SystemScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        _rootWorld = world;
        _rootScheduler = scheduler;
    }

    /// <inheritdoc />
    public void RegisterComponentDeserializer(string typeId, SceneComponentDeserializer deserializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentNullException.ThrowIfNull(deserializer);
        lock (_gate)
            _deserializers[typeId] = deserializer;
    }

    /// <inheritdoc />
    public void RegisterSchemaMigrator(int fromVersion, int toVersion, SceneSchemaMigrator migrator)
    {
        ArgumentNullException.ThrowIfNull(migrator);
        lock (_gate)
            _migrators[(fromVersion, toVersion)] = migrator;
    }

    /// <inheritdoc />
    public SceneInstanceId BeginLoad(SceneLoadDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.ScenePath))
            throw new ArgumentException("ScenePath is required.", nameof(descriptor));

        lock (_gate)
        {
            if (MaxAdditiveScenes > 0 && _additive.Count >= MaxAdditiveScenes)
                throw new InvalidOperationException($"Max additive scenes ({MaxAdditiveScenes}) reached.");

            var id = new SceneInstanceId(_nextSceneId++);
            var parallelism = CreatePartitionedParallelism(1 + _additive.Count);
            var world = new World();
            var scheduler = new SystemScheduler(parallelism);
            EngineDefaultSchedulerSystems.RegisterStockEarlySystems(scheduler);
            EngineDefaultSchedulerSystems.RegisterStockLateSystems(scheduler, _host);

            var entry = new AdditiveSceneEntry
            {
                Id = id,
                Priority = descriptor.Priority,
                InsertionSeq = _additive.Count,
                State = SceneRuntimeState.Allocated,
                World = world,
                Scheduler = scheduler,
                Descriptor = descriptor,
                Queue = new SceneAssetRequestQueue(),
                Cancellation = new CancellationTokenSource(),
                SpawnedDuringLoad = new List<EntityId>(),
                LoadPhase = SceneLoadPhase.PendingRead
            };
            _additive.Add(entry);
            Transition(entry, SceneRuntimeState.Loading);
            return id;
        }
    }

    /// <inheritdoc />
    public async ValueTask<SceneLoadResult> PumpAsync(SceneInstanceId id, SceneLoadPumpOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SceneLoadPumpOptions();
        EnsureEngineComponentDeserializers();
        if (id == SceneInstanceId.Root)
            return new SceneLoadResult { Failed = true, ErrorMessage = "Cannot pump root scene." };

        AdditiveSceneEntry? entry;
        lock (_gate)
        {
            entry = _additive.FirstOrDefault(e => e.Id == id);
        }

        if (entry is null)
            return new SceneLoadResult { Failed = true, ErrorMessage = "Unknown scene id." };

        if (entry.State is SceneRuntimeState.Ready or SceneRuntimeState.Unloaded or SceneRuntimeState.Failed or SceneRuntimeState.Cancelled)
            return new SceneLoadResult { Completed = entry.State == SceneRuntimeState.Ready };

        var sw = Stopwatch.StartNew();
        var maxMs = options.MaxElapsed?.TotalMilliseconds;
        var hasWallTimeBudget = maxMs is > 0;
        var maxEntities = options.MaxEntitiesToCommit ?? 64;
        var maxAssetJobs = options.MaxAssetJobs ?? 8;
        var maxAssetBytes = options.MaxAssetDecodeBytes ?? 1024 * 1024;
        var jobs = 0;
        var bytes = 0;
        var committed = 0;

        try
        {
            if (entry.State == SceneRuntimeState.Unloading)
                return new SceneLoadResult();

            if (entry.LoadPhase == SceneLoadPhase.PendingRead)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var assets = new AssetManager(_vfs);
                var text = await assets.LoadTextAsync(entry.Descriptor.ScenePath, cancellationToken).ConfigureAwait(false);
                bytes += text.Length * 2;
                entry.RawJson = text;
                entry.LoadPhase = SceneLoadPhase.Parsing;
            }

            if (hasWallTimeBudget && sw.Elapsed.TotalMilliseconds > maxMs)
                goto DoneSlice;

            if (entry.LoadPhase == SceneLoadPhase.Parsing)
            {
                using var doc = JsonDocument.Parse(entry.RawJson ?? "{}");
                var root = doc.RootElement.Clone();
                var migrated = ApplyMigrators(root, entry.Descriptor.AllowUnknownComponentTypes);
                var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                entry.Parsed = JsonSerializer.Deserialize<SceneDocument>(migrated.GetRawText(), jsonOpts)
                    ?? throw new InvalidOperationException("Scene document deserialized to null.");
                if (entry.Parsed.SchemaVersion > SceneDocument.CurrentSchemaVersion)
                    throw new InvalidOperationException($"Unsupported newer schemaVersion={entry.Parsed.SchemaVersion}.");
                entry.LoadPhase = SceneLoadPhase.Spawning;
                bytes += entry.RawJson?.Length ?? 0;
            }

            if (hasWallTimeBudget && sw.Elapsed.TotalMilliseconds > maxMs)
                goto DoneSlice;

            if (entry.LoadPhase == SceneLoadPhase.Spawning && entry.Parsed is not null)
            {
                var strings = BuildStringsTable();
                var max = maxEntities;
                while (entry.NextEntityIndex < entry.Parsed.Entities.Count && max-- > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.Cancellation.IsCancellationRequested)
                    {
                        RollbackSpawn(entry);
                        Transition(entry, SceneRuntimeState.Cancelled);
                        Transition(entry, SceneRuntimeState.Unloaded);
                        return new SceneLoadResult { Failed = true, ErrorMessage = "Cancelled." };
                    }

                    if (hasWallTimeBudget && sw.Elapsed.TotalMilliseconds > maxMs)
                        break;

                    var dto = entry.Parsed.Entities[entry.NextEntityIndex++];
                    var eid = entry.World.CreateEntity();
                    entry.SpawnedDuringLoad.Add(eid);

                    try
                    {
                        CommitEntityComponents(
                            entry.World,
                            eid,
                            dto,
                            entry.Descriptor.AllowUnknownComponentTypes,
                            strings,
                            spawnSession: null,
                            logicalIdMap: null);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                    {
                        FailAndRollback(entry, ex.Message);
                        return new SceneLoadResult { Failed = true, ErrorMessage = ex.Message };
                    }

                    committed++;
                }

                if (entry.NextEntityIndex >= entry.Parsed.Entities.Count)
                {
                    Transition(entry, SceneRuntimeState.Ready);
                    _host.InGameLoadProgress.ReportPhaseProgress($"scene:{entry.Id.Value}", 1f);
                    return new SceneLoadResult { Completed = true, EntitiesCommitted = committed, BytesDecoded = bytes, JobsCompleted = jobs };
                }
            }

        DoneSlice:
            jobs += entry.Queue.Drain(maxAssetJobs, maxAssetBytes);
            return new SceneLoadResult
            {
                EntitiesCommitted = committed,
                BytesDecoded = bytes,
                JobsCompleted = jobs
            };
        }
        catch (Exception ex)
        {
            FailAndRollback(entry, ex.Message);
            return new SceneLoadResult { Failed = true, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public async ValueTask<SceneSpawnResult> SpawnIntoWorldAsync(
        World world,
        string scenePath,
        SceneSpawnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        options ??= new SceneSpawnOptions();
        EnsureEngineComponentDeserializers();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assets = new AssetManager(_vfs);
            var text = await assets.LoadTextAsync(scenePath, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement.Clone();
            var migrated = ApplyMigrators(root, options.AllowUnknownComponentTypes);
            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<SceneDocument>(migrated.GetRawText(), jsonOpts)
                ?? throw new InvalidOperationException("Scene document deserialized to null.");
            if (parsed.SchemaVersion > SceneDocument.CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported newer schemaVersion={parsed.SchemaVersion}.");

            var session = new SceneSpawnSession();
            var strings = BuildStringsTable();
            var spawned = new List<EntityId>();
            try
            {
                var count = SpawnEntities(world, parsed, options.AllowUnknownComponentTypes, strings, session, spawned);
                ResolvePendingParentLinks(world, session);
                ResolvePendingCameraFollowTargets(world, session);
                return new SceneSpawnResult { Succeeded = true, EntitiesSpawned = count };
            }
            catch (Exception ex)
            {
                foreach (var id in spawned)
                {
                    if (world.IsAlive(id))
                        world.DestroyEntity(id);
                }

                return new SceneSpawnResult { Succeeded = false, ErrorMessage = ex.Message };
            }
        }
        catch (Exception ex)
        {
            return new SceneSpawnResult { Succeeded = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public bool RequestUnload(SceneInstanceId id)
    {
        if (id == SceneInstanceId.Root)
            return false;

        AdditiveSceneEntry? entry;
        lock (_gate)
            entry = _additive.FirstOrDefault(e => e.Id == id);

        if (entry is null)
            return false;

        if (entry.State is SceneRuntimeState.Unloaded or SceneRuntimeState.Cancelled)
            return false;

        var prev = entry.State;
        entry.Cancellation.Cancel();
        if (entry.State == SceneRuntimeState.Loading)
        {
            RollbackSpawn(entry);
            Transition(entry, SceneRuntimeState.Cancelled);
            Transition(entry, SceneRuntimeState.Unloaded);
        }
        else
        {
            Transition(entry, SceneRuntimeState.Unloading);
            DestroyAllEntities(entry.World);
            Transition(entry, SceneRuntimeState.Unloaded);
        }

        entry.Queue.Clear();
        return true;
    }

    /// <inheritdoc />
    public bool TryGetWorld(SceneInstanceId id, out World world)
    {
        if (id == SceneInstanceId.Root)
        {
            if (_rootWorld is not null)
            {
                world = _rootWorld;
                return true;
            }
        }
        else
        {
            lock (_gate)
            {
                var e = _additive.FirstOrDefault(x => x.Id == id);
                if (e is not null && e.State is not SceneRuntimeState.Unloaded and not SceneRuntimeState.Cancelled and not SceneRuntimeState.Failed)
                {
                    world = e.World;
                    return true;
                }
            }
        }

        world = null!;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetScheduler(SceneInstanceId id, out SystemScheduler scheduler)
    {
        if (id == SceneInstanceId.Root)
        {
            if (_rootScheduler is not null)
            {
                scheduler = _rootScheduler;
                return true;
            }
        }
        else
        {
            lock (_gate)
            {
                var e = _additive.FirstOrDefault(x => x.Id == id);
                if (e is not null && e.State is not SceneRuntimeState.Unloaded and not SceneRuntimeState.Cancelled and not SceneRuntimeState.Failed)
                {
                    scheduler = e.Scheduler;
                    return true;
                }
            }
        }

        scheduler = null!;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetSceneStatus(SceneInstanceId id, out SceneStatus status)
    {
        if (id == SceneInstanceId.Root)
        {
            status = new SceneStatus(SceneInstanceId.Root, SceneRuntimeState.Ready, int.MinValue, null, null);
            return _rootWorld is not null;
        }

        lock (_gate)
        {
            var e = _additive.FirstOrDefault(x => x.Id == id);
            if (e is null)
            {
                status = default;
                return false;
            }

            status = new SceneStatus(e.Id, e.State, e.Priority, e.Descriptor.ScenePath, e.LastError);
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SceneStatus> EnumerateScenes()
    {
        lock (_gate)
        {
            var list = new List<SceneStatus> { new(SceneInstanceId.Root, SceneRuntimeState.Ready, int.MinValue, null, null) };
            foreach (var e in _additive.OrderBy(x => x.Priority).ThenBy(x => x.InsertionSeq))
                list.Add(new SceneStatus(e.Id, e.State, e.Priority, e.Descriptor.ScenePath, e.LastError));
            return new ReadOnlyCollection<SceneStatus>(list);
        }
    }

    /// <inheritdoc />
    public bool TryLiftEntity(World sourceWorld, EntityId sourceEntity, World destinationWorld, out EntityId destinationEntity,
        out IReadOnlyDictionary<EntityId, EntityId> remap)
    {
        destinationEntity = default;
        Span<EntityId> one = stackalloc EntityId[1];
        one[0] = sourceEntity;
        if (!TryLiftEntities(sourceWorld, one, destinationWorld, out remap))
            return false;
        destinationEntity = ((Dictionary<EntityId, EntityId>)remap)[sourceEntity];
        return true;
    }

    /// <inheritdoc />
    public bool TryLiftEntities(World sourceWorld, ReadOnlySpan<EntityId> sourceRoots, World destinationWorld,
        out IReadOnlyDictionary<EntityId, EntityId> remap)
    {
        remap = new Dictionary<EntityId, EntityId>();
        var map = (Dictionary<EntityId, EntityId>)remap;
        foreach (var root in sourceRoots)
        {
            if (!TryLiftSubtree(sourceWorld, root, destinationWorld, out _, out var submap))
                return false;
            foreach (var kv in submap)
                map[kv.Key] = kv.Value;
        }

        return true;
    }

    /// <inheritdoc />
    public bool TryLiftSubtree(World sourceWorld, EntityId root, World destinationWorld, out EntityId destinationRoot,
        out IReadOnlyDictionary<EntityId, EntityId> remap)
    {
        remap = new ReadOnlyDictionary<EntityId, EntityId>(new Dictionary<EntityId, EntityId>());
        destinationRoot = default;
        if (!sourceWorld.IsAlive(root))
            return false;

        var members = EntityWorldTransfer.CollectSubtree(sourceWorld, root);
        if (members.Count == 0)
            return false;

        var ordered = EntityWorldTransfer.OrderParentsBeforeChildren(sourceWorld, members);
        var map = new Dictionary<EntityId, EntityId>();

        foreach (var src in ordered)
        {
            var dst = destinationWorld.CreateEntity();
            map[src] = dst;
        }

        foreach (var src in ordered)
        {
            var dst = map[src];
            EntityWorldTransfer.TryCopyEngineComponents(sourceWorld, src, destinationWorld, dst);
            if (sourceWorld.TryGet(src, out Transform t))
            {
                ref var td = ref destinationWorld.Get<Transform>(dst);
                var p = t.Parent;
                if (p.Raw != 0 && map.TryGetValue(p, out var newP))
                    td.Parent = newP;
                else
                    td.Parent = default;
            }
        }

        var destroyOrder = ordered.ToList();
        destroyOrder.Reverse();
        foreach (var src in destroyOrder)
            sourceWorld.DestroyEntity(src);

        remap = new ReadOnlyDictionary<EntityId, EntityId>(map);
        destinationRoot = map[root];
        return true;
    }

    /// <summary>Ordered additive entries for host tick (excludes unloading terminal).</summary>
    internal IReadOnlyList<AdditiveSceneEntry> GetAdditiveScenesForTick()
    {
        lock (_gate)
        {
            return _additive
                .Where(e => e.State == SceneRuntimeState.Ready || e.State == SceneRuntimeState.Loading)
                .OrderBy(e => e.Priority).ThenBy(e => e.InsertionSeq)
                .ToList();
        }
    }

    internal void PumpAllAdditiveScenes(SceneLoadPumpOptions? options = null)
    {
        List<AdditiveSceneEntry> copy;
        lock (_gate)
            copy = _additive.Where(e => e.State == SceneRuntimeState.Loading).ToList();

        foreach (var e in copy)
            _ = PumpAsync(e.Id, options ?? new SceneLoadPumpOptions(), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    private void DestroyAllEntities(World world)
    {
        var spec = SystemQuerySpec.All<Transform>();
        var ids = new List<EntityId>();
        foreach (var chunk in world.QueryChunks(spec))
        {
            foreach (var id in chunk.Entities)
                ids.Add(id);
        }

        foreach (var id in ids)
        {
            if (world.IsAlive(id))
                world.DestroyEntity(id);
        }
    }

    private static void RollbackSpawn(AdditiveSceneEntry entry)
    {
        foreach (var id in entry.SpawnedDuringLoad)
        {
            if (entry.World.IsAlive(id))
                entry.World.DestroyEntity(id);
        }

        entry.SpawnedDuringLoad.Clear();
    }

    private void FailAndRollback(AdditiveSceneEntry entry, string error)
    {
        entry.LastError = error;
        RollbackSpawn(entry);
        Transition(entry, SceneRuntimeState.Failed);
    }

    private static void ApplyLogicalActor(World world, EntityId entityId, string guid)
    {
        ref var la = ref world.GetOrAdd<LogicalActorId>(entityId);
        la.Guid = guid;
    }

    private void Transition(AdditiveSceneEntry entry, SceneRuntimeState next)
    {
        var prev = entry.State;
        entry.State = next;
        RaiseState(entry.Id, prev, next);
    }

    private void RaiseState(SceneInstanceId id, SceneRuntimeState prev, SceneRuntimeState next) =>
        SceneStateChanged?.Invoke(this, new SceneStateChangedEventArgs(id, prev, next));

    private ILocalizedContentStrings? BuildStringsTable()
    {
        var loc = _getLocalizedContent();
        return loc is null ? null : new LocalizationManagerStringTable(loc.Strings);
    }

    private JsonElement ApplyMigrators(JsonElement root, bool allowUnknownFuture)
    {
        using var doc = JsonDocument.Parse(root.GetRawText());
        var version = doc.RootElement.TryGetProperty("schemaVersion", out var v) && v.TryGetInt32(out var iv) ? iv : 1;
        if (version > SceneDocument.CurrentSchemaVersion)
        {
            if (!allowUnknownFuture)
                throw new InvalidOperationException($"Unsupported newer schemaVersion={version}.");
            return root;
        }

        var el = root;
        while (version < SceneDocument.CurrentSchemaVersion)
        {
            SceneSchemaMigrator? mig;
            lock (_gate)
                _migrators.TryGetValue((version, version + 1), out mig);
            if (mig is null)
                throw new InvalidOperationException($"Missing migrator {version}->{version + 1}.");
            el = mig(el);
            version++;
        }

        return el;
    }

    private int SpawnEntities(
        World world,
        SceneDocument parsed,
        bool allowUnknownComponentTypes,
        ILocalizedContentStrings? strings,
        SceneSpawnSession session,
        List<EntityId> spawned)
    {
        var count = 0;
        foreach (var dto in parsed.Entities)
        {
            var eid = world.CreateEntity();
            spawned.Add(eid);
            CommitEntityComponents(
                world,
                eid,
                dto,
                allowUnknownComponentTypes,
                strings,
                session,
                session.LogicalIdToEntity);
            count++;
        }

        return count;
    }

    private void CommitEntityComponents(
        World world,
        EntityId eid,
        SceneEntityDto dto,
        bool allowUnknownComponentTypes,
        ILocalizedContentStrings? strings,
        SceneSpawnSession? spawnSession,
        Dictionary<string, EntityId>? logicalIdMap)
    {
        if (!string.IsNullOrWhiteSpace(dto.LogicalId))
        {
            if (!Guid.TryParse(dto.LogicalId, out _))
                throw new InvalidOperationException("Invalid logicalId GUID.");
            ApplyLogicalActor(world, eid, dto.LogicalId!);
            logicalIdMap?.Add(dto.LogicalId!, eid);
        }

        foreach (var c in dto.Components)
        {
            if (!TryApplyComponent(world, eid, SceneInstanceId.Root, c, strings, spawnSession))
            {
                if (!allowUnknownComponentTypes)
                    throw new InvalidOperationException($"Unknown component type '{c.Type}'.");
            }
        }
    }

    private static void ResolvePendingParentLinks(World world, SceneSpawnSession session)
    {
        foreach (var (child, parentLogicalId) in session.PendingParentLinks)
        {
            if (!session.LogicalIdToEntity.TryGetValue(parentLogicalId, out var parent))
                throw new InvalidOperationException($"Unknown parentLogicalId '{parentLogicalId}'.");
            ref var t = ref world.Get<Transform>(child);
            t.Parent = parent;
        }
    }

    private static void ResolvePendingCameraFollowTargets(World world, SceneSpawnSession session)
    {
        foreach (var (camera, targetLogicalId) in session.PendingCameraFollowTargets)
        {
            if (!session.LogicalIdToEntity.TryGetValue(targetLogicalId, out var target))
                throw new InvalidOperationException($"Unknown targetLogicalId '{targetLogicalId}'.");
            ref var follow = ref world.Get<CameraFollow2D>(camera);
            follow.Target = target;
        }
    }

    private bool TryApplyComponent(
        World world,
        EntityId eid,
        SceneInstanceId sceneId,
        SceneComponentDto dto,
        ILocalizedContentStrings? strings,
        SceneSpawnSession? spawnSession)
    {
        SceneComponentDeserializer? del;
        lock (_gate)
            _deserializers.TryGetValue(dto.Type, out del);
        if (del is null)
            return false;

        var ctx = spawnSession is null
            ? new SceneComponentDeserializeContext(world, eid, sceneId, dto.Data, strings)
            : new SceneComponentDeserializeContext(world, eid, sceneId, dto.Data, strings, spawnSession);
        del(in ctx);
        return true;
    }

    private ParallelismSettings CreatePartitionedParallelism(int activeSchedulers)
    {
        var n = Math.Max(1, activeSchedulers);
        var max = _rootParallelism.MaxConcurrency <= 0 ? Environment.ProcessorCount : _rootParallelism.MaxConcurrency;
        var per = Math.Max(1, max / n);
        var p = new ParallelismSettings { MaxConcurrency = per };
        return p;
    }

    private void RegisterBuiltinSchemaMigrators()
    {
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        RegisterSchemaMigrator(0, 1, root => BumpSceneSchema(root, 1, jsonOpts));
        RegisterSchemaMigrator(1, 2, root => BumpSceneSchema(root, 2, jsonOpts));
        RegisterSchemaMigrator(2, 3, root => BumpSceneSchema(root, 3, jsonOpts));
    }

    private static JsonElement BumpSceneSchema(JsonElement root, int toVersion, JsonSerializerOptions jsonOpts)
    {
        var doc = JsonSerializer.Deserialize<SceneDocument>(root.GetRawText(), jsonOpts) ?? new SceneDocument();
        doc.SchemaVersion = toVersion;
        return JsonSerializer.SerializeToElement(doc, jsonOpts);
    }

    private void EnsureEngineComponentDeserializers()
    {
        lock (_gate)
        {
            if (_engineDeserializersRegistered)
                return;
            var renderer = _host.Renderer;
            if (renderer is null)
                return;
            EngineSceneComponentDeserializers.Register(this, renderer);
            _engineDeserializersRegistered = true;
        }
    }

    private void RegisterBuiltinDeserializers()
    {
        RegisterComponentDeserializer("cyberland.engine/transform", static (in SceneComponentDeserializeContext ctx) =>
        {
            var lx = 0f;
            var ly = 0f;
            var rot = 0f;
            if (ctx.Data.TryGetProperty("localX", out var jx) && jx.TryGetSingle(out var vx))
                lx = vx;
            if (ctx.Data.TryGetProperty("localY", out var jy) && jy.TryGetSingle(out var vy))
                ly = vy;
            if (ctx.Data.TryGetProperty("localRotationRadians", out var jr) && jr.TryGetSingle(out var vr))
                rot = vr;

            ref var t = ref ctx.World.GetOrAdd<Transform>(ctx.EntityId);
            t = Transform.Identity;
            t.LocalPosition = new Silk.NET.Maths.Vector2D<float>(lx, ly);
            t.LocalRotationRadians = rot;

            if (ctx.SpawnSession is not null
                && ctx.Data.TryGetProperty("parentLogicalId", out var jp)
                && jp.ValueKind == JsonValueKind.String)
            {
                var parentId = jp.GetString();
                if (!string.IsNullOrWhiteSpace(parentId))
                    ctx.SpawnSession.PendingParentLinks.Add((ctx.EntityId, parentId));
            }
        });

        RegisterComponentDeserializer("cyberland.engine/logical-actor", static (in SceneComponentDeserializeContext ctx) =>
        {
            string? g = null;
            if (ctx.Data.TryGetProperty("guid", out var gid) && gid.ValueKind == JsonValueKind.String)
                g = gid.GetString();
            if (string.IsNullOrWhiteSpace(g) || !Guid.TryParse(g, out _))
                return;
            ref var la = ref ctx.World.GetOrAdd<LogicalActorId>(ctx.EntityId);
            la.Guid = g!;
        });
    }

    internal sealed class AdditiveSceneEntry
    {
        public required SceneInstanceId Id { get; init; }
        public required int Priority { get; init; }
        public required int InsertionSeq { get; init; }
        public SceneRuntimeState State;
        public required World World { get; init; }
        public required SystemScheduler Scheduler { get; init; }
        public required SceneLoadDescriptor Descriptor { get; init; }
        public required SceneAssetRequestQueue Queue { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public required List<EntityId> SpawnedDuringLoad { get; init; } = null!;
        public SceneLoadPhase LoadPhase;
        public string? RawJson;
        public SceneDocument? Parsed;
        public int NextEntityIndex;
        public string? LastError;
    }
}

internal enum SceneLoadPhase
{
    PendingRead,
    Parsing,
    Spawning
}
