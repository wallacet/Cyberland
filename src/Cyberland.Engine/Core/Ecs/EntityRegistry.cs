namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Allocates entity indices and generations; recycles indices through a free list to keep arrays dense.
/// </summary>
public sealed class EntityRegistry
{
    private readonly List<uint> _generations = new();
    private readonly Stack<uint> _free = new();

    /// <summary>Allocates a new index (or reuses a recycled one) with a bumped generation.</summary>
    public EntityId Create()
    {
        if (_free.Count > 0)
        {
            var index = _free.Pop();
            var gen = _generations[(int)index];
            return EntityId.FromParts(index, gen);
        }

        var i = (uint)_generations.Count;
        _generations.Add(1);
        return EntityId.FromParts(i, 1);
    }

    /// <summary>
    /// Recycles <paramref name="id"/>'s index and bumps its generation so the slot can be reused.
    /// </summary>
    /// <remarks>
    /// This only invalidates the entity id. Prefer <see cref="World.DestroyEntity"/> so attached components are removed from ECS storage.
    /// </remarks>
    public void Destroy(EntityId id)
    {
        var index = (int)id.Index;
        if (index < 0 || index >= _generations.Count)
            return;

        _generations[index]++;
        _free.Push(id.Index);
    }

    /// <summary>True if <paramref name="id"/>’s generation still matches the slot (not destroyed / recycled).</summary>
    public bool IsAlive(EntityId id)
    {
        var index = (int)id.Index;
        if (index < 0 || index >= _generations.Count)
            return false;

        return _generations[index] == id.Generation;
    }
}
