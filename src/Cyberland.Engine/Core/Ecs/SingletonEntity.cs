namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Handle for the single entity resolved from an <see cref="ISingletonSystem"/>’s <see cref="IEcsQuerySource.QuerySpec"/>.
/// Passes through to <see cref="World"/> component storage for that row.
/// </summary>
public readonly struct SingletonEntity
{
    private readonly World _world;
    private readonly EntityId _id;

    /// <summary>Creates a handle used by the scheduler; mods normally receive this from singleton phase callbacks.</summary>
    public SingletonEntity(World world, EntityId entityId)
    {
        _world = world;
        _id = entityId;
    }

    /// <summary>The resolved singleton entity id (exactly one row matched the query at <see cref="ISingletonSystem"/> startup).</summary>
    public EntityId Entity => _id;

    /// <summary>ECS world that owns <see cref="Entity"/>.</summary>
    public World World => _world;

    /// <inheritdoc cref="World.Get{T}(EntityId)"/>
    public ref T Get<T>() where T : struct, IComponent => ref _world.Get<T>(_id);

    /// <inheritdoc cref="World.TryGet{T}(EntityId, out T)"/>
    public bool TryGet<T>(out T value) where T : struct, IComponent => _world.TryGet(_id, out value);
}
