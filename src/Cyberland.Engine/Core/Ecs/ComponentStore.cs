namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Dense storage for a single component type: parallel iteration is a tight loop over <see cref="AsSpan"/>.
/// Sparse array maps entity index to dense slot for O(1) lookup.
/// </summary>
public sealed class ComponentStore<T> : IComponentStore where T : struct
{
    private int[] _sparse;
    private readonly List<EntityId> _entities = new();
    private readonly List<T> _dense = new();
    private const int Invalid = -1;

    public ComponentStore()
    {
        _sparse = new int[1024];
        Array.Fill(_sparse, Invalid);
    }

    public ReadOnlySpan<EntityId> Entities => CollectionsMarshal.AsSpan(_entities);
    public Span<T> AsSpan() => CollectionsMarshal.AsSpan(_dense);

    public int Count => _dense.Count;

    public ref T GetOrAdd(EntityId entity, T initial = default)
    {
        EnsureSparse((int)entity.Index);
        ref var slotIdx = ref _sparse[entity.Index];
        if (slotIdx != Invalid)
            return ref CollectionsMarshal.AsSpan(_dense)[slotIdx];

        slotIdx = _dense.Count;
        _entities.Add(entity);
        _dense.Add(initial);
        return ref CollectionsMarshal.AsSpan(_dense)[slotIdx];
    }

    public bool TryGet(EntityId entity, out T value)
    {
        value = default;
        if ((int)entity.Index >= _sparse.Length)
            return false;

        var slot = _sparse[entity.Index];
        if (slot == Invalid)
            return false;

        value = _dense[slot];
        return true;
    }

    public ref T Get(EntityId entity)
    {
        var slot = _sparse[entity.Index];
        if (slot == Invalid)
            throw new InvalidOperationException("Component missing for entity.");

        return ref CollectionsMarshal.AsSpan(_dense)[slot];
    }

    public void Remove(EntityId entity)
    {
        if ((int)entity.Index >= _sparse.Length)
            return;

        var slot = _sparse[entity.Index];
        if (slot == Invalid)
            return;

        var last = _dense.Count - 1;
        if (slot != last)
        {
            _dense[slot] = _dense[last];
            var movedEntity = _entities[last];
            _entities[slot] = movedEntity;
            _sparse[movedEntity.Index] = slot;
        }

        _dense.RemoveAt(last);
        _entities.RemoveAt(last);
        _sparse[entity.Index] = Invalid;
    }

    public bool Contains(EntityId entity) =>
        (int)entity.Index < _sparse.Length && _sparse[entity.Index] != Invalid;

    private void EnsureSparse(int index)
    {
        if (index < _sparse.Length)
            return;

        var newLen = Math.Max(_sparse.Length * 2, index + 1);
        var next = new int[newLen];
        Array.Fill(next, Invalid);
        Array.Copy(_sparse, next, _sparse.Length);
        _sparse = next;
    }
}
