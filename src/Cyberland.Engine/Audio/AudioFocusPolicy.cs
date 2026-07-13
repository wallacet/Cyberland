namespace Cyberland.Engine.Audio;

/// <summary>Behavior when the game window loses focus.</summary>
public readonly struct AudioFocusPolicy
{
    /// <summary>No change when unfocused.</summary>
    public static AudioFocusPolicy Ignore => new(FocusKind.Ignore, 1f);

    /// <summary>Mute master while unfocused.</summary>
    public static AudioFocusPolicy MuteMaster => new(FocusKind.MuteMaster, 0f);

    /// <summary>Scale master by <paramref name="gain"/> while unfocused.</summary>
    public static AudioFocusPolicy DuckMaster(float gain) => new(FocusKind.DuckMaster, gain);

    private AudioFocusPolicy(FocusKind kind, float duckGain)
    {
        Kind = kind;
        DuckGain = duckGain;
    }

    /// <summary>Policy kind.</summary>
    public FocusKind Kind { get; }

    /// <summary>Master scale when <see cref="Kind"/> is <see cref="FocusKind.DuckMaster"/>.</summary>
    public float DuckGain { get; }

    /// <summary>Focus policy discriminant.</summary>
    public enum FocusKind : byte
    {
        /// <summary>Leave mix unchanged.</summary>
        Ignore = 0,

        /// <summary>Master gain becomes 0.</summary>
        MuteMaster = 1,

        /// <summary>Master gain multiplied by <see cref="DuckGain"/>.</summary>
        DuckMaster = 2,
    }
}
