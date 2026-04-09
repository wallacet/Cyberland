namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Stable numeric id for a component struct type within a <see cref="World"/>.
/// Used in canonical sorted signatures for archetype lookup.
/// </summary>
public readonly struct ComponentId : IEquatable<ComponentId>
{
    public uint Value { get; }

    public ComponentId(uint value) => Value = value;

    public bool Equals(ComponentId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ComponentId o && Equals(o);
    public override int GetHashCode() => (int)Value;
    public static bool operator ==(ComponentId a, ComponentId b) => a.Equals(b);
    public static bool operator !=(ComponentId a, ComponentId b) => !a.Equals(b);
}
