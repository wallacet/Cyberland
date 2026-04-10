namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Stable numeric id for a component struct type within a <see cref="World"/>.
/// Used in canonical sorted signatures for archetype lookup.
/// </summary>
public readonly struct ComponentId : IEquatable<ComponentId>
{
    /// <summary>Runtime-unique id for a component struct type inside the registry.</summary>
    public uint Value { get; }

    /// <summary>Creates an id (normally only the ECS core assigns these).</summary>
    public ComponentId(uint value) => Value = value;

    /// <inheritdoc />
    public bool Equals(ComponentId other) => Value == other.Value;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ComponentId o && Equals(o);
    /// <inheritdoc />
    public override int GetHashCode() => (int)Value;
    /// <summary>Equality comparison.</summary>
    public static bool operator ==(ComponentId a, ComponentId b) => a.Equals(b);
    /// <summary>Inequality comparison.</summary>
    public static bool operator !=(ComponentId a, ComponentId b) => !a.Equals(b);
}
