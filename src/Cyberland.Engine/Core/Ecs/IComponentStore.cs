namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Non-generic surface implemented by <see cref="ComponentStore{T}"/> for typed removal and presence checks.
/// </summary>
public interface IComponentStore
{
    /// <summary>Removes this component type from <paramref name="entity"/> if present (may migrate archetype).</summary>
    void Remove(EntityId entity);
    /// <summary>Whether <paramref name="entity"/> currently has this component type.</summary>
    bool Contains(EntityId entity);
}
