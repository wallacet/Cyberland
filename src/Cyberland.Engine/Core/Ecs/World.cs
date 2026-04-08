namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Owns entity lifetime and component stores. Systems query stores directly or via helpers.
/// </summary>
public sealed class World
{
    private readonly EntityRegistry _entities = new();
    private readonly Dictionary<Type, IComponentStore> _stores = new();

    public EntityRegistry Entities => _entities;

    public EntityId CreateEntity() => _entities.Create();

    public void DestroyEntity(EntityId id)
    {
        foreach (var store in _stores.Values)
            store.Remove(id);

        _entities.Destroy(id);
    }

    public bool IsAlive(EntityId id) => _entities.IsAlive(id);

    public ComponentStore<T> Components<T>() where T : struct
    {
        if (!_stores.TryGetValue(typeof(T), out var store))
        {
            var created = new ComponentStore<T>();
            _stores[typeof(T)] = created;
            return created;
        }

        return (ComponentStore<T>)store;
    }
}
