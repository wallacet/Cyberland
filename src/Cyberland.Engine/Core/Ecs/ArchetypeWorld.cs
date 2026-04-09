namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Archetype graph, chunk allocation, entity location table, and migration between signatures.
/// </summary>
internal sealed class ArchetypeWorld
{
    public readonly ComponentRegistry Registry = new();
    private readonly List<Archetype> _archetypes = new();
    private readonly Dictionary<uint[], int> _signatureToIndex;
    private readonly Dictionary<ComponentId, List<int>> _archetypesByComponent = new();
    private EntityRecord[] _records;

    public ArchetypeWorld()
    {
        _signatureToIndex = new Dictionary<uint[], int>(SignatureComparer.Instance);
        _records = new EntityRecord[1024];
        ClearRecords(_records);
    }

    private static void ClearRecords(EntityRecord[] records)
    {
        for (var i = 0; i < records.Length; i++)
            records[i] = new EntityRecord { ArchetypeIndex = EntityRecord.NoArchetype };
    }

    public IReadOnlyList<Archetype> Archetypes => _archetypes;

    public ref EntityRecord GetRecordRef(EntityId entity)
    {
        EnsureRecord((int)entity.Index);
        return ref _records[entity.Index];
    }

    public void OnEntityCreated(EntityId entity)
    {
        EnsureRecord((int)entity.Index);
        _records[entity.Index] = new EntityRecord { ArchetypeIndex = EntityRecord.NoArchetype };
    }

    public void OnEntityDestroyed(EntityId entity)
    {
        EnsureRecord((int)entity.Index);
        _records[entity.Index] = new EntityRecord { ArchetypeIndex = EntityRecord.NoArchetype };
    }

    private void EnsureRecord(int index)
    {
        if (index < _records.Length)
            return;

        var oldLen = _records.Length;
        var newLen = Math.Max(_records.Length * 2, index + 1);
        Array.Resize(ref _records, newLen);
        for (var i = oldLen; i < newLen; i++)
            _records[i] = new EntityRecord { ArchetypeIndex = EntityRecord.NoArchetype };
    }

    /// <summary>Expands the entity record table so <paramref name="index"/> is addressable (tests, tooling).</summary>
    internal void GrowRecordsForIndex(int index) => EnsureRecord(index);

    public (Archetype arch, int index) GetOrCreateArchetype(uint[] signatureKey)
    {
        if (_signatureToIndex.TryGetValue(signatureKey, out var idx))
            return (_archetypes[idx], idx);

        var arch = new Archetype(signatureKey);
        idx = _archetypes.Count;
        _archetypes.Add(arch);
        _signatureToIndex[arch.Signature] = idx;
        foreach (var u in arch.Signature)
        {
            var cid = new ComponentId(u);
            if (!_archetypesByComponent.TryGetValue(cid, out var list))
            {
                list = new List<int>();
                _archetypesByComponent[cid] = list;
            }

            list.Add(idx);
        }

        return (arch, idx);
    }

    public (ArchetypeChunk chunk, int chunkIndex, int row) AppendRow(Archetype arch)
    {
        ArchetypeChunk? chunk = null;
        var ci = 0;
        if (arch.Chunks.Count > 0)
        {
            var last = arch.Chunks[^1];
            if (last.Count < ArchetypeChunk.Capacity)
            {
                chunk = last;
                ci = arch.Chunks.Count - 1;
            }
        }

        if (chunk is null)
        {
            chunk = new ArchetypeChunk(arch.Signature, Registry);
            arch.Chunks.Add(chunk);
            ci = arch.Chunks.Count - 1;
        }

        var row = chunk.Count;
        return (chunk, ci, row);
    }

    public void RemoveEntityAt(Archetype arch, int chunkIndex, int row, EntityId entity)
    {
        var chunk = arch.Chunks[chunkIndex];
        var last = chunk.Count - 1;
        var moved = chunk.Entities[last];
        if (row != last)
        {
            chunk.Entities[row] = moved;
            foreach (var col in chunk.Columns)
                col.SwapRemove(row, last);

            ref var movedRec = ref GetRecordRef(moved);
            movedRec.Row = row;
        }

        chunk.Count--;
    }

    public void DestroyEntityLayout(EntityId id, ref EntityRecord rec)
    {
        if (!rec.HasLayout)
            return;

        var arch = _archetypes[(int)rec.ArchetypeIndex];
        RemoveEntityAt(arch, rec.ChunkIndex, rec.Row, id);
        rec.ArchetypeIndex = EntityRecord.NoArchetype;
    }

    public ref T GetOrAddComponent<T>(EntityId entity, T initial) where T : struct
    {
        ref var rec = ref GetRecordRef(entity);
        var tid = Registry.GetOrRegister<T>().Value;

        if (!rec.HasLayout)
            return ref AddFirstComponent(ref rec, entity, tid, initial);

        var oldArch = _archetypes[(int)rec.ArchetypeIndex];
        if (oldArch.SignatureContains(tid))
        {
            var colIdx = oldArch.ColumnIndexOf(tid);
            var chunk = oldArch.Chunks[rec.ChunkIndex];
            return ref ((Column<T>)chunk.Columns[colIdx]).At(rec.Row);
        }

        var newSig = SignatureHelpers.InsertSorted(oldArch.Signature, tid);
        MigrateToSignature(ref rec, entity, oldArch, newSig, tid, initial);
        var newArch = _archetypes[(int)rec.ArchetypeIndex];
        var newCol = newArch.ColumnIndexOf(tid);
        var nc = newArch.Chunks[rec.ChunkIndex];
        return ref ((Column<T>)nc.Columns[newCol]).At(rec.Row);
    }

    private ref T AddFirstComponent<T>(ref EntityRecord rec, EntityId entity, uint tid, T initial) where T : struct
    {
        var sig = new[] { tid };
        var (arch, archIdx) = GetOrCreateArchetype(sig);
        var (chunk, ci, row) = AppendRow(arch);
        chunk.Entities[row] = entity;
        for (var i = 0; i < chunk.Columns.Length; i++)
            chunk.Columns[i].WriteDefault(row);

        ((Column<T>)chunk.Columns[0]).At(row) = initial;
        chunk.Count++;

        rec.ArchetypeIndex = (uint)archIdx;
        rec.ChunkIndex = ci;
        rec.Row = row;
        return ref ((Column<T>)chunk.Columns[0]).At(row);
    }

    private void MigrateToSignature<T>(ref EntityRecord rec, EntityId entity, Archetype oldArch, uint[] newSig,
        uint addedTid, T addedInitial) where T : struct
    {
        var oldChunk = oldArch.Chunks[rec.ChunkIndex];
        var oldRow = rec.Row;
        var (newArch, newIdx) = GetOrCreateArchetype(newSig);
        var (destChunk, destCi, destRow) = AppendRow(newArch);

        destChunk.Entities[destRow] = entity;
        CopySharedColumns(oldArch, oldChunk, oldRow, newArch, destChunk, destRow);
        var newTCol = newArch.ColumnIndexOf(addedTid);
        ((Column<T>)destChunk.Columns[newTCol]).At(destRow) = addedInitial;
        destChunk.Count++;

        RemoveEntityAt(oldArch, rec.ChunkIndex, oldRow, entity);

        rec.ArchetypeIndex = (uint)newIdx;
        rec.ChunkIndex = destCi;
        rec.Row = destRow;
    }

    private static void CopySharedColumns(
        Archetype oldArch, ArchetypeChunk oldChunk, int oldRow,
        Archetype newArch, ArchetypeChunk newChunk, int newRow)
    {
        foreach (var uid in newArch.Signature)
        {
            var newCol = newArch.ColumnIndexOf(uid);
            if (!oldArch.TryColumnIndexOf(uid, out var oldCol))
            {
                newChunk.Columns[newCol].WriteDefault(newRow);
                continue;
            }

            oldChunk.Columns[oldCol].CopyRowTo(newChunk.Columns[newCol], oldRow, newRow);
        }
    }

    public void RemoveComponent<T>(EntityId entity) where T : struct
    {
        ref var rec = ref GetRecordRef(entity);
        if (!rec.HasLayout)
            return;

        var tid = Registry.GetOrRegister<T>().Value;
        var oldArch = _archetypes[(int)rec.ArchetypeIndex];
        if (!oldArch.SignatureContains(tid))
            return;

        var oldChunk = oldArch.Chunks[rec.ChunkIndex];
        var oldRow = rec.Row;
        var newSig = SignatureHelpers.RemoveSorted(oldArch.Signature, tid);
        if (newSig.Length == 0)
        {
            RemoveEntityAt(oldArch, rec.ChunkIndex, oldRow, entity);
            rec.ArchetypeIndex = EntityRecord.NoArchetype;
            return;
        }

        var (newArch, newIdx) = GetOrCreateArchetype(newSig);
        var (destChunk, destCi, destRow) = AppendRow(newArch);
        destChunk.Entities[destRow] = entity;
        CopySharedColumns(oldArch, oldChunk, oldRow, newArch, destChunk, destRow);
        destChunk.Count++;

        RemoveEntityAt(oldArch, rec.ChunkIndex, oldRow, entity);

        rec.ArchetypeIndex = (uint)newIdx;
        rec.ChunkIndex = destCi;
        rec.Row = destRow;
    }

    public bool TryGetComponent<T>(EntityId entity, out T value) where T : struct
    {
        value = default;
        if ((int)entity.Index >= _records.Length)
            return false;

        ref var rec = ref _records[entity.Index];
        if (!rec.HasLayout)
            return false;

        var tid = Registry.GetOrRegister<T>().Value;
        var arch = _archetypes[(int)rec.ArchetypeIndex];
        if (!arch.TryColumnIndexOf(tid, out var colIdx))
            return false;

        var chunk = arch.Chunks[rec.ChunkIndex];
        value = ((Column<T>)chunk.Columns[colIdx]).At(rec.Row);
        return true;
    }

    public ref T GetComponent<T>(EntityId entity) where T : struct
    {
        ref var rec = ref GetRecordRef(entity);
        if (!rec.HasLayout)
            throw new InvalidOperationException("Component missing for entity.");

        var tid = Registry.GetOrRegister<T>().Value;
        var arch = _archetypes[(int)rec.ArchetypeIndex];
        var colIdx = arch.ColumnIndexOf(tid);
        var chunk = arch.Chunks[rec.ChunkIndex];
        return ref ((Column<T>)chunk.Columns[colIdx]).At(rec.Row);
    }

    public bool HasComponent<T>(EntityId entity) where T : struct
    {
        if ((int)entity.Index >= _records.Length)
            return false;

        ref var rec = ref _records[entity.Index];
        if (!rec.HasLayout)
            return false;

        var tid = Registry.GetOrRegister<T>().Value;
        var arch = _archetypes[(int)rec.ArchetypeIndex];
        return arch.SignatureContains(tid);
    }

    public List<int>? GetArchetypeIndicesContaining(ComponentId id) =>
        _archetypesByComponent.TryGetValue(id, out var list) ? list : null;
}
