namespace Cyberland.Engine.Audio;

/// <summary>
/// Stock mix-bus id conventions registered by the mixer at construction.
/// Mods may register additional buses; do not treat this list as exhaustive.
/// </summary>
public static class AudioBusIds
{
    /// <summary>Applied last to every voice.</summary>
    public const string Master = "master";

    /// <summary>Default for music beds.</summary>
    public const string Music = "music";

    /// <summary>Default for one-shot SFX.</summary>
    public const string Sfx = "sfx";

    /// <summary>Default for looping ambient beds.</summary>
    public const string Ambient = "ambient";

    /// <summary>Default for non-diegetic cinematic stings.</summary>
    public const string Cinematic = "cinematic";

    /// <summary>Default for menu/HUD clicks (typically does not pause with gameplay).</summary>
    public const string Ui = "ui";
}
