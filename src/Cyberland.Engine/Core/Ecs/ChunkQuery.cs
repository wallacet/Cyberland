namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Foreach-able source of per-chunk contiguous spans for one component type.
/// </summary>
public readonly struct ChunkQuery<T> where T : struct
{
    private readonly ArchetypeWorld _world;

    internal ChunkQuery(ArchetypeWorld world) => _world = world;

    public ChunkQueryEnumerator<T> GetEnumerator() => new(_world);
}

/// <summary>
/// One chunk's entities and SoA column for <typeparamref name="T"/> (contiguous for SIMD-friendly loops).
/// </summary>
public readonly struct ComponentChunkView<T> where T : struct
{
    internal readonly ArchetypeChunk Chunk;
    internal readonly int ColumnIndex;

    internal ComponentChunkView(ArchetypeChunk chunk, int columnIndex)
    {
        Chunk = chunk;
        ColumnIndex = columnIndex;
    }

    public int Count => Chunk.Count;

    public ReadOnlySpan<EntityId> Entities => Chunk.Entities.AsSpan(0, Chunk.Count);

    public Span<T> Components => ((Column<T>)Chunk.Columns[ColumnIndex]).AsSpan(Chunk.Count);
}

/// <summary>
/// Iterates all chunks (across archetypes) that include component <typeparamref name="T"/>.
/// </summary>
public struct ChunkQueryEnumerator<T> where T : struct
{
    private readonly ArchetypeWorld _world;
    private readonly ComponentId _tid;
    private readonly List<int>? _archetypeIndices;
    private int _archetypeEnumIndex;
    private Archetype? _currentArch;
    private int _columnIndex;
    private int _chunkEnumIndex;
    private ComponentChunkView<T> _current;

    internal ChunkQueryEnumerator(ArchetypeWorld world)
    {
        _world = world;
        _tid = world.Registry.GetOrRegister<T>();
        _archetypeIndices = world.GetArchetypeIndicesContaining(_tid);
        _archetypeEnumIndex = 0;
        _currentArch = null;
        _columnIndex = 0;
        _chunkEnumIndex = 0;
        _current = default;
    }

    public ComponentChunkView<T> Current => _current;

    public bool MoveNext()
    {
        if (_archetypeIndices is null || _archetypeIndices.Count == 0)
            return false;

        while (_archetypeEnumIndex < _archetypeIndices.Count)
        {
            if (_currentArch is null)
            {
                _currentArch = _world.Archetypes[_archetypeIndices[_archetypeEnumIndex]];
                _columnIndex = _currentArch.ColumnIndexOf(_tid.Value);
                _chunkEnumIndex = 0;
            }

            var arch = _currentArch!;
            while (_chunkEnumIndex < arch.Chunks.Count)
            {
                var ch = arch.Chunks[_chunkEnumIndex++];
                if (ch.Count == 0)
                    continue;

                _current = new ComponentChunkView<T>(ch, _columnIndex);
                return true;
            }

            _currentArch = null;
            _archetypeEnumIndex++;
        }

        return false;
    }
}

/// <summary>
/// Foreach-able query for entities that have both <typeparamref name="T0"/> and <typeparamref name="T1"/>.
/// </summary>
public readonly struct ChunkQuery2<T0, T1>
    where T0 : struct
    where T1 : struct
{
    private readonly ArchetypeWorld _world;

    internal ChunkQuery2(ArchetypeWorld world) => _world = world;

    public ChunkQueryEnumerator2<T0, T1> GetEnumerator() => new(_world);
}

public struct ChunkQueryEnumerator2<T0, T1>
    where T0 : struct
    where T1 : struct
{
    private readonly ArchetypeWorld _world;
    private readonly ComponentId _id0;
    private readonly ComponentId _id1;
    private readonly List<int>? _candidates;
    private int _archetypeEnumIndex;
    private Archetype? _currentArch;
    private int _col0;
    private int _col1;
    private int _chunkEnumIndex;
    private ComponentChunkView2<T0, T1> _current;

    internal ChunkQueryEnumerator2(ArchetypeWorld world)
    {
        _world = world;
        _id0 = world.Registry.GetOrRegister<T0>();
        _id1 = world.Registry.GetOrRegister<T1>();
        _candidates = world.GetArchetypeIndicesContaining(_id0);
        _archetypeEnumIndex = 0;
        _currentArch = null;
        _col0 = 0;
        _col1 = 0;
        _chunkEnumIndex = 0;
        _current = default;
    }

    public ComponentChunkView2<T0, T1> Current => _current;

    public bool MoveNext()
    {
        if (_candidates is null || _candidates.Count == 0)
            return false;

        while (_archetypeEnumIndex < _candidates.Count)
        {
            if (_currentArch is null)
            {
                var arch = _world.Archetypes[_candidates[_archetypeEnumIndex]];
                if (!arch.SignatureContains(_id1.Value))
                {
                    _archetypeEnumIndex++;
                    continue;
                }

                _currentArch = arch;
                _col0 = arch.ColumnIndexOf(_id0.Value);
                _col1 = arch.ColumnIndexOf(_id1.Value);
                _chunkEnumIndex = 0;
            }

            var a = _currentArch!;
            while (_chunkEnumIndex < a.Chunks.Count)
            {
                var ch = a.Chunks[_chunkEnumIndex++];
                if (ch.Count == 0)
                    continue;

                _current = new ComponentChunkView2<T0, T1>(ch, _col0, _col1);
                return true;
            }

            _currentArch = null;
            _archetypeEnumIndex++;
        }

        return false;
    }
}

/// <summary>
/// Two SoA columns for the same chunk (matching entities by row index).
/// </summary>
public readonly struct ComponentChunkView2<T0, T1>
    where T0 : struct
    where T1 : struct
{
    internal readonly ArchetypeChunk Chunk;
    internal readonly int ColumnIndex0;
    internal readonly int ColumnIndex1;

    internal ComponentChunkView2(ArchetypeChunk chunk, int columnIndex0, int columnIndex1)
    {
        Chunk = chunk;
        ColumnIndex0 = columnIndex0;
        ColumnIndex1 = columnIndex1;
    }

    public int Count => Chunk.Count;

    public ReadOnlySpan<EntityId> Entities => Chunk.Entities.AsSpan(0, Chunk.Count);

    public Span<T0> Components0 => ((Column<T0>)Chunk.Columns[ColumnIndex0]).AsSpan(Chunk.Count);

    public Span<T1> Components1 => ((Column<T1>)Chunk.Columns[ColumnIndex1]).AsSpan(Chunk.Count);
}
