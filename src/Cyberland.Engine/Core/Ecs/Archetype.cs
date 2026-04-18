namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// All entities with the same sorted component signature; storage is split into <see cref="ArchetypeChunk"/>s.
/// </summary>
internal sealed class Archetype
{
    public readonly ComponentId[] Signature;
    public readonly List<ArchetypeChunk> Chunks = new();

    public Archetype(ComponentId[] signature) => Signature = signature;

    public int ColumnIndexOf(ComponentId componentIdValue)
    {
        var idx = Array.BinarySearch(Signature, componentIdValue);
        if (idx < 0)
            throw new InvalidOperationException("Component is not part of this archetype.");
        return idx;
    }

    public bool TryColumnIndexOf(ComponentId componentIdValue, out int index)
    {
        index = Array.BinarySearch(Signature, componentIdValue);
        if (index < 0)
        {
            index = 0;
            return false;
        }

        return true;
    }

    public bool SignatureContains(ComponentId componentIdValue) => Array.BinarySearch(Signature, componentIdValue) >= 0;
}
