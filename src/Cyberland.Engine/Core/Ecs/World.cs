namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// The ECS database for one running game: creates entities, stores <c>struct</c> components in archetype chunks, and supports fast queries.
/// </summary>
/// <remarks>
/// Mods receive the shared <see cref="World"/> from <see cref="Modding.ModLoadContext.World"/>. Use <see cref="Components{T}"/> for per-entity access and
/// <see cref="QueryChunks{T}"/> (or <see cref="QueryChunks{T0,T1}"/>) to iterate contiguous memory for hot loops / parallel jobs.
/// </remarks>
public sealed class World
{
    private readonly EntityRegistry _entities = new();
    private readonly ArchetypeWorld _ecs = new();
    private readonly Dictionary<Type, object> _componentFacades = new();

    /// <summary>Low-level id allocator; most code uses <see cref="CreateEntity"/> instead.</summary>
    public EntityRegistry Entities => _entities;

    /// <summary>Creates a new entity with no components yet.</summary>
    /// <returns>A fresh <see cref="EntityId"/> valid until <see cref="DestroyEntity"/>.</returns>
    public EntityId CreateEntity()
    {
        var id = _entities.Create();
        _ecs.OnEntityCreated(id);
        return id;
    }

    /// <summary>Removes all components and recycles the entity id.</summary>
    public void DestroyEntity(EntityId id)
    {
        if (!_entities.IsAlive(id))
            return;

        ref var rec = ref _ecs.GetRecordRef(id);
        _ecs.DestroyEntityLayout(id, ref rec);
        _entities.Destroy(id);
        _ecs.OnEntityDestroyed(id);
    }

    /// <summary>Returns false if the id was never issued or was destroyed (generation mismatch).</summary>
    public bool IsAlive(EntityId id) => _entities.IsAlive(id);

    /// <summary>
    /// Facade for adding, reading, and removing component type <typeparamref name="T"/> on entities.
    /// </summary>
    public ComponentStore<T> Components<T>() where T : struct, IComponent
    {
        if (!_componentFacades.TryGetValue(typeof(T), out var fac))
        {
            fac = new ComponentStore<T>(this);
            _componentFacades[typeof(T)] = fac;
        }

        return (ComponentStore<T>)fac;
    }

    /// <summary>Shorthand for <c>ref Components&lt;T&gt;().Get(entity)</c> when a <see cref="World"/> reference is already cached (e.g. per-system singleton access).</summary>
    public ref T Get<T>(EntityId entity) where T : struct, IComponent => ref RefGet<T>(entity);

    /// <inheritdoc cref="ComponentStore{T}.TryGet" />
    public bool TryGet<T>(EntityId entity, out T value) where T : struct, IComponent => TryGetComponent<T>(entity, out value);

    /// <inheritdoc cref="ComponentStore{T}.Contains" />
    public bool Has<T>(EntityId entity) where T : struct, IComponent => HasComponent<T>(entity);

    /// <inheritdoc cref="ComponentStore{T}.GetOrAdd(EntityId)" />
    public ref T GetOrAdd<T>(EntityId entity) where T : struct, IComponent => ref RefGetOrAdd<T>(entity);

    /// <inheritdoc cref="ComponentStore{T}.GetOrAdd(EntityId, T)" />
    public ref T GetOrAdd<T>(EntityId entity, T initial) where T : struct, IComponent => ref RefGetOrAdd(entity, initial);

    /// <inheritdoc cref="ComponentStore{T}.Remove" />
    public void Remove<T>(EntityId entity) where T : struct, IComponent => RemoveComponent<T>(entity);

    /// <summary>
    /// All chunks that store <typeparamref name="T"/> (each chunk yields contiguous spans for SIMD-friendly work).
    /// </summary>
    public ChunkQuery<T> QueryChunks<T>() where T : struct, IComponent => new(_ecs);

    /// <summary>
    /// Chunks that contain both <typeparamref name="T0"/> and <typeparamref name="T1"/>.
    /// </summary>
    public ChunkQuery2<T0, T1> QueryChunks<T0, T1>()
        where T0 : struct, IComponent
        where T1 : struct, IComponent => new(_ecs);

    /// <summary>
    /// Chunks matching the scheduler query spec (all listed components required). Prefer scheduler-passed <see cref="ChunkQueryAll"/>
    /// in <see cref="Tasks.SystemScheduler"/> systems; this is for tests and tooling.
    /// </summary>
    public ChunkQueryAll QueryChunks(SystemQuerySpec spec) => new(_ecs, spec);

    /// <summary>
    /// Zero-based column index for <typeparamref name="T"/> in <see cref="MultiComponentChunkView.Column{T}(int)"/> for <paramref name="spec"/>
    /// (sorted runtime component ids).
    /// </summary>
    public int GetQueryColumnIndex<T>(SystemQuerySpec spec) where T : struct, IComponent
    {
        if (Array.IndexOf(spec.Types, typeof(T)) < 0)
            throw new ArgumentException($"{typeof(T).FullName} is not part of this query spec.", nameof(spec));

        var want = _ecs.Registry.GetOrRegister<T>();
        var ids = spec.ResolveSortedComponentIds(_ecs.Registry);
        var idx = Array.BinarySearch(ids, want);
        return idx;
    }

    internal ref T RefGetOrAdd<T>(EntityId entity) where T : struct, IComponent
    {
        RequiresComponentEnforcer.EnsureDependencies<T>(this, entity);
        return ref _ecs.GetOrAddComponent(entity, new T());
    }

    internal ref T RefGetOrAdd<T>(EntityId entity, T initial) where T : struct, IComponent
    {
        RequiresComponentEnforcer.EnsureDependencies<T>(this, entity);
        return ref _ecs.GetOrAddComponent(entity, initial);
    }

    internal bool TryGetComponent<T>(EntityId entity, out T value) where T : struct, IComponent =>
        _ecs.TryGetComponent<T>(entity, out value);

    internal ref T RefGet<T>(EntityId entity) where T : struct, IComponent => ref _ecs.GetComponent<T>(entity);

    internal void RemoveComponent<T>(EntityId entity) where T : struct, IComponent =>
        _ecs.RemoveComponent<T>(entity);

    internal bool HasComponent<T>(EntityId entity) where T : struct, IComponent =>
        _ecs.HasComponent<T>(entity);

    internal void GrowRecordsForIndexForTests(int index) => _ecs.GrowRecordsForIndex(index);
}
