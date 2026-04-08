namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Opaque entity handle: lower bits are a dense index, upper bits are a generation counter.
/// Reusing an index bumps generation so stale handles are detectable.
/// </summary>
public readonly struct EntityId : IEquatable<EntityId>
{
    public const int IndexBits = 20;
    public const uint IndexMask = (1u << IndexBits) - 1;

    public uint Raw { get; }

    public EntityId(uint raw) => Raw = raw;

    public uint Index => Raw & IndexMask;
    public uint Generation => Raw >> IndexBits;

    public static EntityId FromParts(uint index, uint generation) =>
        new((generation << IndexBits) | (index & IndexMask));

    public bool Equals(EntityId other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is EntityId e && Equals(e);
    public override int GetHashCode() => Raw.GetHashCode();
    public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);
    public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
}
