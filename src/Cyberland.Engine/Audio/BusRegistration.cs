namespace Cyberland.Engine.Audio;

/// <summary>Options for <see cref="IAudioService.RegisterBus(string, in BusRegistration)"/>.</summary>
public struct BusRegistration
{
    /// <summary>Initial linear gain (default 1).</summary>
    public float DefaultGain;

    /// <summary>Max simultaneous voices on this bus; 0 or negative means unlimited (global cap still applies).</summary>
    public int MaxVoices;

    /// <summary>Steal policy when <see cref="MaxVoices"/> is exceeded.</summary>
    public VoiceStealMode StealMode;

    /// <summary>Creates registration with gain 1, unlimited voices, steal lowest priority.</summary>
    public static BusRegistration Default => new()
    {
        DefaultGain = 1f,
        MaxVoices = 0,
        StealMode = VoiceStealMode.StealLowestPriority,
    };
}
