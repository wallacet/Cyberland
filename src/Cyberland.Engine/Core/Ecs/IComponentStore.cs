namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Non-generic view so <see cref="World"/> can track all component storages without boxing per component type.
/// </summary>
public interface IComponentStore
{
    void Remove(EntityId entity);
    bool Contains(EntityId entity);
}
