namespace Cyberland.Engine.Audio;

/// <summary>Opaque handle for an active or recently stopped mixer voice.</summary>
public readonly struct VoiceId : IEquatable<VoiceId>
{
    /// <summary>No voice.</summary>
    public static VoiceId None => default;

    /// <summary>Creates a voice id from a positive mixer-assigned value.</summary>
    public VoiceId(int value) => Value = value;

    /// <summary>Underlying id; 0 is none.</summary>
    public int Value { get; }

    /// <summary>True when this id may refer to a voice slot.</summary>
    public bool IsValid => Value > 0;

    /// <inheritdoc />
    public bool Equals(VoiceId other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is VoiceId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => IsValid ? $"Voice({Value})" : "Voice(None)";

    /// <summary>Equality.</summary>
    public static bool operator ==(VoiceId a, VoiceId b) => a.Equals(b);

    /// <summary>Inequality.</summary>
    public static bool operator !=(VoiceId a, VoiceId b) => !a.Equals(b);
}
