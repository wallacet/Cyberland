namespace Cyberland.Engine.Audio;

/// <summary>How a voice is positioned relative to the listener.</summary>
public enum AudioSpace : byte
{
    /// <summary>Diegetic 2D world position with distance attenuation and stereo pan.</summary>
    World = 0,

    /// <summary>Non-spatial / HUD; no attenuation (optional explicit pan).</summary>
    Direct = 1,

    /// <summary>Non-diegetic cinematic; ignores world pan (typically <see cref="AudioBusIds.Cinematic"/>).</summary>
    Cinematic = 2,
}
