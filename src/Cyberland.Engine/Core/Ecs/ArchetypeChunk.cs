namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Fixed-capacity block of entities sharing one archetype: SoA columns + parallel entity ids.
/// </summary>
internal sealed class ArchetypeChunk
{
    /// <summary>Multiple of 8 for typical float SIMD inner loops.</summary>
    public const int Capacity = 512;

    public readonly EntityId[] Entities;
    public readonly ColumnBase[] Columns;
    public int Count;

    public ArchetypeChunk(ComponentId[] signature, ComponentRegistry registry)
    {
        Entities = new EntityId[Capacity];
        Columns = new ColumnBase[signature.Length];
        for (var i = 0; i < signature.Length; i++)
            Columns[i] = registry.CreateColumn(signature[i], Capacity);
    }
}
