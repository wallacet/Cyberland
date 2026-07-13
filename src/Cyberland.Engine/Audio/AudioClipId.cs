namespace Cyberland.Engine.Audio;

/// <summary>Opaque handle for a decoded (or streaming) clip in the mixer cache.</summary>
public readonly struct AudioClipId : IEquatable<AudioClipId>
{
    /// <summary>Invalid / missing clip (mirrors texture missing-id pattern).</summary>
    public static AudioClipId Invalid => default;

    /// <summary>Creates a clip id from a positive mixer-assigned value.</summary>
    public AudioClipId(int value) => Value = value;

    /// <summary>Underlying id; 0 is invalid.</summary>
    public int Value { get; }

    /// <summary>True when this id refers to a loaded clip.</summary>
    public bool IsValid => Value > 0;

    /// <inheritdoc />
    public bool Equals(AudioClipId other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AudioClipId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => IsValid ? $"AudioClip({Value})" : "AudioClip(Invalid)";

    /// <summary>Equality.</summary>
    public static bool operator ==(AudioClipId a, AudioClipId b) => a.Equals(b);

    /// <summary>Inequality.</summary>
    public static bool operator !=(AudioClipId a, AudioClipId b) => !a.Equals(b);
}
