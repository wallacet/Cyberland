namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Opaque handle for a runtime scene instance (root or additive). Not an ECS component.
/// </summary>
public readonly struct SceneInstanceId : IEquatable<SceneInstanceId>
{
    /// <summary>Reserved id for the process-lifetime root scene (see <see cref="SceneRuntime"/>).</summary>
    public static SceneInstanceId Root => new(0);

    /// <summary>Raw monotonic id; <c>0</c> is root.</summary>
    public ulong Value { get; }

    /// <summary>Constructs an id from a raw value (<c>0</c> must match <see cref="Root"/> only).</summary>
    public SceneInstanceId(ulong value) => Value = value;

    /// <inheritdoc />
    public bool Equals(SceneInstanceId other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SceneInstanceId o && Equals(o);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Equality.</summary>
    public static bool operator ==(SceneInstanceId a, SceneInstanceId b) => a.Equals(b);

    /// <summary>Inequality.</summary>
    public static bool operator !=(SceneInstanceId a, SceneInstanceId b) => !a.Equals(b);
}
