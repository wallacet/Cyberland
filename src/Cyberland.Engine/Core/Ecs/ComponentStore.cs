namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Typed accessor for component struct <typeparamref name="T"/> on entities: add/remove and get <c>ref</c> to stored values.
/// Bulk iteration over many entities uses <see cref="World.QueryChunks{T}"/> instead.
/// </summary>
public sealed class ComponentStore<T> : IComponentStore where T : struct, IComponent
{
    private readonly World _world;

    internal ComponentStore(World world) => _world = world;

    /// <summary>
    /// Adds <typeparamref name="T"/> using <c>new T()</c> so struct field initializers and the parameterless constructor run.
    /// </summary>
    public ref T GetOrAdd(EntityId entity) => ref _world.RefGetOrAdd<T>(entity);

    /// <summary>Adds or returns existing; uses <paramref name="initial"/> as the value for a new component (no <c>new T()</c>).</summary>
    public ref T GetOrAdd(EntityId entity, T initial) => ref _world.RefGetOrAdd(entity, initial);

    /// <summary>Reads the component if present without adding it.</summary>
    public bool TryGet(EntityId entity, out T value) => _world.TryGetComponent(entity, out value);

    /// <summary>Returns a mutable reference to <typeparamref name="T"/>; entity must already have the component.</summary>
    public ref T Get(EntityId entity) => ref _world.RefGet<T>(entity);

    /// <inheritdoc />
    public void Remove(EntityId entity) => _world.RemoveComponent<T>(entity);

    /// <inheritdoc />
    public bool Contains(EntityId entity) => _world.HasComponent<T>(entity);
}
