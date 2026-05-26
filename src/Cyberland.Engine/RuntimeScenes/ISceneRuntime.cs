using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using System.Text.Json;
using System.Threading;

namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Host-facing runtime scene stack: additive worlds, load pump, entity lift, and asset queue.
/// </summary>
/// <remarks>
/// Additive worlds share the single <see cref="Rendering.IRenderer"/> submission queue. Sprite, light, camera, and
/// post-volume submissions from all active scenes are combined into one frame plan by the renderer.
/// </remarks>
public interface ISceneRuntime
{
    /// <summary>Raised after a successful state transition.</summary>
    event EventHandler<SceneStateChangedEventArgs>? SceneStateChanged;

    /// <summary>Begins loading a new additive scene; returns immediately after allocating world/scheduler.</summary>
    SceneInstanceId BeginLoad(SceneLoadDescriptor descriptor);

    /// <summary>Advances load work for the given scene under <paramref name="options"/> budgets.</summary>
    ValueTask<SceneLoadResult> PumpAsync(SceneInstanceId id, SceneLoadPumpOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Requests unload; idempotent. Root id is rejected.</summary>
    bool RequestUnload(SceneInstanceId id);

    /// <summary>Try get world for a non-terminal scene.</summary>
    bool TryGetWorld(SceneInstanceId id, out World world);

    /// <summary>Try get scheduler for a non-terminal scene.</summary>
    bool TryGetScheduler(SceneInstanceId id, out SystemScheduler scheduler);

    /// <summary>Status snapshot for one scene.</summary>
    bool TryGetSceneStatus(SceneInstanceId id, out SceneStatus status);

    /// <summary>All non-root scenes currently tracked (including terminal states until reclaimed).</summary>
    IReadOnlyList<SceneStatus> EnumerateScenes();

    /// <summary>
    /// Copies supported components from <paramref name="sourceEntity"/> in <paramref name="sourceWorld"/> into a new entity in
    /// <paramref name="destinationWorld"/>; destroys the source entity on success.
    /// </summary>
    bool TryLiftEntity(World sourceWorld, EntityId sourceEntity, World destinationWorld, out EntityId destinationEntity,
        out IReadOnlyDictionary<EntityId, EntityId> remap);

    /// <summary>Lifts many roots in one deterministic batch.</summary>
    bool TryLiftEntities(World sourceWorld, ReadOnlySpan<EntityId> sourceRoots, World destinationWorld,
        out IReadOnlyDictionary<EntityId, EntityId> remap);

    /// <summary>Lifts <paramref name="root"/> and all transform descendants (bottom-up destroy on source).</summary>
    bool TryLiftSubtree(World sourceWorld, EntityId root, World destinationWorld, out EntityId destinationRoot,
        out IReadOnlyDictionary<EntityId, EntityId> remap);

    /// <summary>
    /// Parses a scene JSON file from the layered VFS and spawns all entities into an existing world (typically the process root).
    /// </summary>
    ValueTask<SceneSpawnResult> SpawnIntoWorldAsync(
        World world,
        string scenePath,
        SceneSpawnOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Registers a mod component deserializer for scene JSON <c>type</c> strings.</summary>
    void RegisterComponentDeserializer(string typeId, SceneComponentDeserializer deserializer);

    /// <summary>Registers a schema migrator from <paramref name="fromVersion"/> to <paramref name="toVersion"/>.</summary>
    void RegisterSchemaMigrator(int fromVersion, int toVersion, SceneSchemaMigrator migrator);

    /// <summary>Maximum additive scenes allowed (0 = unlimited).</summary>
    int MaxAdditiveScenes { get; set; }
}

/// <summary>
/// Deserializes one component payload onto an entity in a target world.
/// </summary>
public delegate void SceneComponentDeserializer(in SceneComponentDeserializeContext context);

/// <summary>
/// Context passed to <see cref="SceneComponentDeserializer"/>.
/// </summary>
public readonly struct SceneComponentDeserializeContext
{
    /// <summary>Creates a deserialize context.</summary>
    public SceneComponentDeserializeContext(
        World world,
        EntityId entityId,
        SceneInstanceId sceneId,
        JsonElement data,
        ILocalizedContentStrings? strings)
        : this(world, entityId, sceneId, data, strings, null)
    {
    }

    internal SceneComponentDeserializeContext(
        World world,
        EntityId entityId,
        SceneInstanceId sceneId,
        JsonElement data,
        ILocalizedContentStrings? strings,
        SceneSpawnSession? spawnSession)
    {
        World = world;
        EntityId = entityId;
        SceneId = sceneId;
        Data = data;
        Strings = strings;
        SpawnSession = spawnSession;
    }

    /// <summary>Target world row belongs to.</summary>
    public World World { get; }

    /// <summary>Target entity.</summary>
    public EntityId EntityId { get; }

    /// <summary>Owning additive scene (root for root-world authoring helpers).</summary>
    public SceneInstanceId SceneId { get; }

    /// <summary>JSON payload for the component.</summary>
    public JsonElement Data { get; }

    /// <summary>Merged localization strings for key resolution.</summary>
    public ILocalizedContentStrings? Strings { get; }

    /// <summary>Non-null during <see cref="ISceneRuntime.SpawnIntoWorldAsync"/> for deferred parent wiring.</summary>
    internal SceneSpawnSession? SpawnSession { get; }
}

/// <summary>
/// Migrates a scene JSON root from one schema version toward another; returns replacement root element or throws.
/// </summary>
public delegate JsonElement SceneSchemaMigrator(JsonElement root);

/// <summary>
/// Narrow string surface used during scene deserialization.
/// </summary>
public interface ILocalizedContentStrings
{
    /// <summary>Try resolve a merged locale string by key.</summary>
    bool TryGetString(string key, out string value);
}
