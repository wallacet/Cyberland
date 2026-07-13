using Silk.NET.Maths;

namespace Cyberland.Engine.Audio;

/// <summary>Listener pose for spatialization.</summary>
public struct ListenerState
{
    /// <summary>World position (+Y up).</summary>
    public Vector2D<float> PositionWorld;

    /// <summary>Facing rotation in radians (CCW); used for stereo pan.</summary>
    public float RotationRadians;
}

/// <summary>Fire-and-forget one-shot request.</summary>
public struct OneShotRequest
{
    /// <summary>Canonical content path (preferred when <see cref="ClipId"/> is invalid).</summary>
    public string? ClipPath;

    /// <summary>Preloaded clip id.</summary>
    public AudioClipId ClipId;

    /// <summary>Playback space.</summary>
    public AudioSpace Space;

    /// <summary>Mix bus id; empty → stock default for the API.</summary>
    public string? BusId;

    /// <summary>Linear gain.</summary>
    public float Gain;

    /// <summary>Pitch multiplier (1 = original).</summary>
    public float Pitch;

    /// <summary>Steal ranking (higher wins).</summary>
    public int Priority;

    /// <summary>Fade-in seconds.</summary>
    public float FadeInSeconds;

    /// <summary>Delay before start (seconds).</summary>
    public float DelaySeconds;

    /// <summary>When true, pauses with gameplay / session pause.</summary>
    public bool PauseWithGameplay;

    /// <summary>When true, pitch follows session time scale.</summary>
    public bool ApplyTimeScale;

    /// <summary>World position when <see cref="Space"/> is <see cref="AudioSpace.World"/>.</summary>
    public Vector2D<float> PositionWorld;

    /// <summary>Reference distance for attenuation.</summary>
    public float RefDistance;

    /// <summary>Max audible distance.</summary>
    public float MaxDistance;

    /// <summary>Rolloff exponent.</summary>
    public float Rolloff;

    /// <summary>Explicit pan for Direct space [-1,1]; ignored for World.</summary>
    public float Pan;

    /// <summary>Defaults suitable for world SFX.</summary>
    public static OneShotRequest DefaultWorld => new()
    {
        Space = AudioSpace.World,
        Gain = 1f,
        Pitch = 1f,
        Priority = 0,
        PauseWithGameplay = true,
        ApplyTimeScale = true,
        RefDistance = 64f,
        MaxDistance = 480f,
        Rolloff = 1f,
    };

    /// <summary>Defaults suitable for UI clicks.</summary>
    public static OneShotRequest DefaultUi => new()
    {
        Space = AudioSpace.Direct,
        BusId = AudioBusIds.Ui,
        Gain = 1f,
        Pitch = 1f,
        Priority = 50,
        PauseWithGameplay = false,
        ApplyTimeScale = false,
    };
}

/// <summary>Looping voice request.</summary>
public struct LoopRequest
{
    /// <summary>Canonical content path.</summary>
    public string? ClipPath;

    /// <summary>Preloaded clip id.</summary>
    public AudioClipId ClipId;

    /// <summary>Playback space.</summary>
    public AudioSpace Space;

    /// <summary>Mix bus id.</summary>
    public string? BusId;

    /// <summary>Linear gain.</summary>
    public float Gain;

    /// <summary>Pitch multiplier.</summary>
    public float Pitch;

    /// <summary>Steal ranking.</summary>
    public int Priority;

    /// <summary>Fade-in seconds.</summary>
    public float FadeInSeconds;

    /// <summary>Pause with gameplay.</summary>
    public bool PauseWithGameplay;

    /// <summary>Apply session time scale to pitch.</summary>
    public bool ApplyTimeScale;

    /// <summary>World position.</summary>
    public Vector2D<float> PositionWorld;

    /// <summary>Reference distance.</summary>
    public float RefDistance;

    /// <summary>Max distance.</summary>
    public float MaxDistance;

    /// <summary>Rolloff.</summary>
    public float Rolloff;

    /// <summary>Direct-space pan.</summary>
    public float Pan;
}

/// <summary>Music bed request.</summary>
public struct MusicRequest
{
    /// <summary>Canonical track path.</summary>
    public string? ClipPath;

    /// <summary>Preloaded clip id.</summary>
    public AudioClipId ClipId;

    /// <summary>Bus (default music).</summary>
    public string? BusId;

    /// <summary>Loop the bed.</summary>
    public bool Loop;

    /// <summary>Fade-in seconds.</summary>
    public float FadeInSeconds;

    /// <summary>Gain.</summary>
    public float Gain;

    /// <summary>When true, pause with gameplay.</summary>
    public bool PauseWithGameplay;
}

/// <summary>Cue definition registered with the mixer.</summary>
public struct AudioCueDesc
{
    /// <summary>Canonical clip paths (localized at load).</summary>
    public string[]? ClipPaths;

    /// <summary>Default bus.</summary>
    public string? BusId;

    /// <summary>Default space.</summary>
    public AudioSpace Space;

    /// <summary>Pitch jitter min.</summary>
    public float PitchMin;

    /// <summary>Pitch jitter max.</summary>
    public float PitchMax;

    /// <summary>Gain jitter min.</summary>
    public float GainMin;

    /// <summary>Gain jitter max.</summary>
    public float GainMax;

    /// <summary>Max simultaneous instances of this cue.</summary>
    public int MaxInstances;

    /// <summary>Minimum seconds between retriggers.</summary>
    public float CooldownSeconds;

    /// <summary>Default priority.</summary>
    public int Priority;

    /// <summary>Variation pick mode.</summary>
    public AudioCueVariation.PickMode PickMode;

    /// <summary>World attenuation defaults.</summary>
    public float RefDistance;

    /// <summary>Max distance.</summary>
    public float MaxDistance;

    /// <summary>Rolloff.</summary>
    public float Rolloff;
}

/// <summary>Play a registered cue.</summary>
public struct PlayCueRequest
{
    /// <summary>Override space; if unset use cue default. Use nullable pattern via HasSpace.</summary>
    public bool HasSpace;

    /// <summary>Space override.</summary>
    public AudioSpace Space;

    /// <summary>World position.</summary>
    public Vector2D<float> PositionWorld;

    /// <summary>Optional bus override.</summary>
    public string? BusId;

    /// <summary>Gain scale on top of cue jitter.</summary>
    public float Gain;

    /// <summary>Priority override (int.MinValue = use cue default).</summary>
    public int Priority;
}

/// <summary>Snapshot of a voice for queries.</summary>
public struct VoiceState
{
    /// <summary>Voice is allocated and playing or paused.</summary>
    public bool IsPlaying;

    /// <summary>Paused.</summary>
    public bool IsPaused;

    /// <summary>Current gain envelope (includes fade).</summary>
    public float Envelope;

    /// <summary>Bus id.</summary>
    public string? BusId;
}

/// <summary>Mixer diagnostics for HUD / tests.</summary>
public struct AudioMixerStats
{
    /// <summary>Currently allocated real voices.</summary>
    public int ActiveVoices;

    /// <summary>Steals in the last mixer second (approximate).</summary>
    public int StealsRecent;

    /// <summary>Registered bus count.</summary>
    public int RegisteredBusCount;

    /// <summary>Loaded clip count.</summary>
    public int LoadedClipCount;

    /// <summary>True when OpenAL mixer is live.</summary>
    public bool IsReady;
}
