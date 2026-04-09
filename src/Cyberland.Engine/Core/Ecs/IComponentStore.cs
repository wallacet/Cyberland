namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Non-generic surface implemented by <see cref="ComponentStore{T}"/> for typed removal and presence checks.
/// </summary>
public interface IComponentStore
{
    void Remove(EntityId entity);
    bool Contains(EntityId entity);
}
