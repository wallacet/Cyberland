namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Where an entity's components live: one archetype chunk row, or no layout yet.
/// </summary>
internal struct EntityRecord
{
    public const int NoArchetype = int.MaxValue;

    /// <summary>Archetype index in the owning world's archetype list.</summary>
    public int ArchetypeIndex;

    public int ChunkIndex;
    public int Row;

    public readonly bool HasLayout => ArchetypeIndex != NoArchetype;
}
