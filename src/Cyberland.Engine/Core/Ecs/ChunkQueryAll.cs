namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Scheduler-facing query result: all archetype chunks whose entities satisfy the owning <see cref="SystemQuerySpec"/>.
/// </summary>
public readonly struct ChunkQueryAll
{
    private readonly ArchetypeWorld _world;

    /// <summary>Spec this query was built from (same instance the scheduler used for this system).</summary>
    public SystemQuerySpec Spec { get; }

    internal ChunkQueryAll(ArchetypeWorld world, SystemQuerySpec spec)
    {
        _world = world;
        Spec = spec;
    }

    /// <summary>Enumerates non-empty chunks matching the query.</summary>
    public ChunkQueryEnumeratorAll GetEnumerator() => new(_world, Spec);
}

/// <summary>
/// One chunk slice: entity ids plus one column per required component (indices align with sorted component ids in the query).
/// </summary>
public readonly struct MultiComponentChunkView
{
    internal readonly ArchetypeChunk Chunk;
    internal readonly int[] ColumnIndices;
    internal readonly RuntimeTypeHandle[] QueryTypeHandlesBySortedId;

    internal MultiComponentChunkView(ArchetypeChunk chunk, int[] columnIndices, RuntimeTypeHandle[] queryTypeHandlesBySortedId)
    {
        Chunk = chunk;
        ColumnIndices = columnIndices;
        QueryTypeHandlesBySortedId = queryTypeHandlesBySortedId;
    }

    /// <summary>Active rows in this chunk.</summary>
    public int Count => Chunk.Count;

    /// <summary>Entity id for each row.</summary>
    public ReadOnlySpan<EntityId> Entities => Chunk.Entities.AsSpan(0, Chunk.Count);

    /// <summary>SoA column for the component at index <paramref name="componentIndexInSpec"/> (0..ColumnIndices.Length-1).</summary>
    public Span<T> Column<T>(int componentIndexInSpec) where T : struct, IComponent =>
        ((Column<T>)Chunk.Columns[ColumnIndices[componentIndexInSpec]]).AsSpan(Chunk.Count);

    /// <summary>
    /// SoA column for <typeparamref name="T"/> in this query.
    /// Prefer the indexed overload in ultra-hot loops where the lookup index is already cached.
    /// </summary>
    public Span<T> Column<T>() where T : struct, IComponent
    {
        var want = typeof(T).TypeHandle;
        for (var i = 0; i < QueryTypeHandlesBySortedId.Length; i++)
        {
            if (QueryTypeHandlesBySortedId[i].Equals(want))
                return Column<T>(i);
        }

        throw new ArgumentException($"{typeof(T).FullName} is not part of this chunk query.");
    }
}

/// <summary>Struct enumerator backing <c>foreach</c> on <see cref="ChunkQueryAll"/>.</summary>
public struct ChunkQueryEnumeratorAll
{
    private readonly ArchetypeWorld _world;
    private readonly uint[] _sortedIds;
    private readonly RuntimeTypeHandle[] _queryTypeHandlesBySortedId;
    private readonly List<int>? _archetypeIndices;
    private int _archetypeEnumIndex;
    private Archetype? _currentArch;
    private int[]? _columnIndices;
    private int _chunkEnumIndex;
    private MultiComponentChunkView _current;

    internal ChunkQueryEnumeratorAll(ArchetypeWorld world, SystemQuerySpec spec)
    {
        _world = world;
        var sortedTypes = (Type[])spec.Types.Clone();
        Array.Sort(sortedTypes, (a, b) =>
        {
            var aId = world.Registry.GetOrRegister(a);
            var bId = world.Registry.GetOrRegister(b);
            return aId.CompareTo(bId);
        });
        _queryTypeHandlesBySortedId = new RuntimeTypeHandle[sortedTypes.Length];
        for (var i = 0; i < sortedTypes.Length; i++)
            _queryTypeHandlesBySortedId[i] = sortedTypes[i].TypeHandle;
        _sortedIds = spec.ResolveSortedComponentIds(world.Registry);
        _archetypeIndices = _sortedIds.Length == 0
            ? new List<int>()
            : world.GetArchetypeIndicesMatchingAll(_sortedIds);
        _archetypeEnumIndex = 0;
        _currentArch = null;
        _columnIndices = null;
        _chunkEnumIndex = 0;
        _current = default;
    }

    /// <summary>Current chunk view after a successful <see cref="MoveNext"/>.</summary>
    public MultiComponentChunkView Current => _current;

    /// <summary>Advances to the next non-empty matching chunk.</summary>
    public bool MoveNext()
    {
        if (_archetypeIndices is null || _archetypeIndices.Count == 0)
            return false;

        while (_archetypeEnumIndex < _archetypeIndices.Count)
        {
            if (_currentArch is null)
            {
                _currentArch = _world.Archetypes[_archetypeIndices[_archetypeEnumIndex]];
                _columnIndices = new int[_sortedIds.Length];
                for (var j = 0; j < _sortedIds.Length; j++)
                    _columnIndices[j] = _currentArch.ColumnIndexOf(_sortedIds[j]);

                _chunkEnumIndex = 0;
            }

            var arch = _currentArch!;
            var cols = _columnIndices!;
            while (_chunkEnumIndex < arch.Chunks.Count)
            {
                var ch = arch.Chunks[_chunkEnumIndex++];
                if (ch.Count == 0)
                    continue;

                _current = new MultiComponentChunkView(ch, cols, _queryTypeHandlesBySortedId);
                return true;
            }

            _currentArch = null;
            _columnIndices = null;
            _archetypeEnumIndex++;
        }

        return false;
    }
}
