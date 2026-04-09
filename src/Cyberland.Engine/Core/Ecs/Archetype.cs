namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// All entities with the same sorted component signature; storage is split into <see cref="ArchetypeChunk"/>s.
/// </summary>
internal sealed class Archetype
{
    public readonly uint[] Signature;
    public readonly List<ArchetypeChunk> Chunks = new();

    public Archetype(uint[] signature) => Signature = signature;

    public int ColumnIndexOf(uint componentIdValue)
    {
        var idx = Array.BinarySearch(Signature, componentIdValue);
        if (idx < 0)
            throw new InvalidOperationException("Component is not part of this archetype.");
        return idx;
    }

    public bool TryColumnIndexOf(uint componentIdValue, out int index)
    {
        index = Array.BinarySearch(Signature, componentIdValue);
        if (index < 0)
        {
            index = 0;
            return false;
        }

        return true;
    }

    public bool SignatureContains(uint componentIdValue) => Array.BinarySearch(Signature, componentIdValue) >= 0;
}
