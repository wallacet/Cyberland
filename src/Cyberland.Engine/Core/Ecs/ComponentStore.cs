namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Per-component-type facade for entity-scoped access; iteration uses <see cref="World.QueryChunks{T}"/>.
/// </summary>
public sealed class ComponentStore<T> : IComponentStore where T : struct
{
    private readonly World _world;

    internal ComponentStore(World world) => _world = world;

    /// <summary>
    /// Adds <typeparamref name="T"/> using <c>new T()</c> so struct field initializers and the parameterless constructor run.
    /// </summary>
    public ref T GetOrAdd(EntityId entity) => ref _world.RefGetOrAdd<T>(entity);

    /// <summary>Adds or returns existing; uses <paramref name="initial"/> as the value for a new component (no <c>new T()</c>).</summary>
    public ref T GetOrAdd(EntityId entity, T initial) => ref _world.RefGetOrAdd(entity, initial);

    public bool TryGet(EntityId entity, out T value) => _world.TryGetComponent(entity, out value);

    public ref T Get(EntityId entity) => ref _world.RefGet<T>(entity);

    public void Remove(EntityId entity) => _world.RemoveComponent<T>(entity);

    public bool Contains(EntityId entity) => _world.HasComponent<T>(entity);
}
