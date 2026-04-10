namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Lightweight handle to a row in the ECS. Packed into one <see cref="uint"/>: index + generation so recycled ids do not alias old entities.
/// </summary>
public readonly struct EntityId : IEquatable<EntityId>
{
    /// <summary>Bits used for the dense entity index (see <see cref="Index"/>).</summary>
    public const int IndexBits = 20;
    /// <summary>Bitmask for the index portion of <see cref="Raw"/>.</summary>
    public const uint IndexMask = (1u << IndexBits) - 1;

    /// <summary>Opaque packed value; prefer <see cref="Index"/> / <see cref="Generation"/> for debugging.</summary>
    public uint Raw { get; }

    /// <summary>Reconstructs from a packed value (advanced; normally use ids from <see cref="World.CreateEntity"/>).</summary>
    public EntityId(uint raw) => Raw = raw;

    /// <summary>Dense slot index (stable until destroy frees it).</summary>
    public uint Index => Raw & IndexMask;
    /// <summary>Incremented when an index is reused so stale handles fail <see cref="EntityRegistry.IsAlive"/>.</summary>
    public uint Generation => Raw >> IndexBits;

    /// <summary>Builds an id from separate index and generation (mainly tests / deserialization).</summary>
    public static EntityId FromParts(uint index, uint generation) =>
        new((generation << IndexBits) | (index & IndexMask));

    /// <inheritdoc />
    public bool Equals(EntityId other) => Raw == other.Raw;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EntityId e && Equals(e);
    /// <inheritdoc />
    public override int GetHashCode() => Raw.GetHashCode();
    /// <summary>Equality comparison.</summary>
    public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);
    /// <summary>Inequality comparison.</summary>
    public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
}
