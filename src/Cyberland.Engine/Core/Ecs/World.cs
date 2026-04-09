namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Owns entity lifetime, the archetype graph, and chunk queries. Systems access components via
/// <see cref="Components{T}"/> or <see cref="QueryChunks{T}"/>.
/// </summary>
public sealed class World
{
    private readonly EntityRegistry _entities = new();
    private readonly ArchetypeWorld _ecs = new();
    private readonly Dictionary<Type, object> _componentFacades = new();

    public EntityRegistry Entities => _entities;

    public EntityId CreateEntity()
    {
        var id = _entities.Create();
        _ecs.OnEntityCreated(id);
        return id;
    }

    public void DestroyEntity(EntityId id)
    {
        if (!_entities.IsAlive(id))
            return;

        ref var rec = ref _ecs.GetRecordRef(id);
        _ecs.DestroyEntityLayout(id, ref rec);
        _entities.Destroy(id);
        _ecs.OnEntityDestroyed(id);
    }

    public bool IsAlive(EntityId id) => _entities.IsAlive(id);

    public ComponentStore<T> Components<T>() where T : struct
    {
        if (!_componentFacades.TryGetValue(typeof(T), out var fac))
        {
            fac = new ComponentStore<T>(this);
            _componentFacades[typeof(T)] = fac;
        }

        return (ComponentStore<T>)fac;
    }

    /// <summary>
    /// All chunks that store <typeparamref name="T"/> (each chunk yields contiguous spans for SIMD-friendly work).
    /// </summary>
    public ChunkQuery<T> QueryChunks<T>() where T : struct => new(_ecs);

    /// <summary>
    /// Chunks that contain both <typeparamref name="T0"/> and <typeparamref name="T1"/>.
    /// </summary>
    public ChunkQuery2<T0, T1> QueryChunks<T0, T1>()
        where T0 : struct
        where T1 : struct => new(_ecs);

    internal ref T RefGetOrAdd<T>(EntityId entity, T initial = default) where T : struct =>
        ref _ecs.GetOrAddComponent(entity, initial);

    internal bool TryGetComponent<T>(EntityId entity, out T value) where T : struct =>
        _ecs.TryGetComponent<T>(entity, out value);

    internal ref T RefGet<T>(EntityId entity) where T : struct => ref _ecs.GetComponent<T>(entity);

    internal void RemoveComponent<T>(EntityId entity) where T : struct =>
        _ecs.RemoveComponent<T>(entity);

    internal bool HasComponent<T>(EntityId entity) where T : struct =>
        _ecs.HasComponent<T>(entity);

    internal void GrowRecordsForIndexForTests(int index) => _ecs.GrowRecordsForIndex(index);
}
