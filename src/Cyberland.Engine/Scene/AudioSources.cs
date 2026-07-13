using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Canonical clip path plus cached mixer id after localized load.
/// </summary>
public struct AudioClipRef : IComponent
{
    /// <summary>Canonical content path (e.g. <c>Sounds/hit.wav</c>).</summary>
    public string CanonicalPath;

    /// <summary>Cached clip id; invalid until loaded.</summary>
    public AudioClipId ClipId;
}

/// <summary>
/// ECS audio emitter (one-shot or loop). Uses <see cref="ClipPath"/> or <see cref="CueId"/>.
/// </summary>
[RequiresComponent<Transform>]
public struct AudioEmitterSource : IComponent
{
    /// <summary>When false, ignored.</summary>
    public bool Active;

    /// <summary>Canonical clip path (used when <see cref="CueId"/> is empty).</summary>
    public string ClipPath;

    /// <summary>Registered cue id (preferred when non-empty).</summary>
    public string CueId;

    /// <summary>Playback space.</summary>
    public AudioSpace Space;

    /// <summary>Mix bus id.</summary>
    public string BusId;

    /// <summary>Looping bed when true.</summary>
    public bool Loop;

    /// <summary>Play once when the row becomes active / on start.</summary>
    public bool PlayOnEnable;

    /// <summary>Linear gain.</summary>
    public float Gain;

    /// <summary>Pitch.</summary>
    public float Pitch;

    /// <summary>Steal priority.</summary>
    public int Priority;

    /// <summary>Fade-in seconds.</summary>
    public float FadeInSeconds;

    /// <summary>Reference distance.</summary>
    public float RefDistance;

    /// <summary>Max distance.</summary>
    public float MaxDistance;

    /// <summary>Rolloff.</summary>
    public float Rolloff;

    /// <summary>Pause with gameplay.</summary>
    public bool PauseWithGameplay;

    /// <summary>Runtime: voice for looping emitters.</summary>
    public VoiceId RuntimeVoice;

    /// <summary>Runtime: whether PlayOnEnable already fired.</summary>
    public bool RuntimeStarted;
}

/// <summary>Music bed driven from ECS.</summary>
public struct MusicSource : IComponent
{
    /// <summary>When false, ignored.</summary>
    public bool Active;

    /// <summary>Canonical track path.</summary>
    public string ClipPath;

    /// <summary>Bus id (default music).</summary>
    public string BusId;

    /// <summary>Loop the bed.</summary>
    public bool Loop;

    /// <summary>Crossfade / fade-in seconds.</summary>
    public float CrossfadeSeconds;

    /// <summary>Pause with gameplay.</summary>
    public bool PauseWithGameplay;

    /// <summary>Priority among music rows (highest active wins).</summary>
    public int Priority;

    /// <summary>Runtime: last submitted path to avoid spam.</summary>
    public string RuntimeSubmittedPath;
}

/// <summary>Global audio environment baseline (highest priority active wins).</summary>
public struct GlobalAudioEnvironmentSource : IComponent
{
    /// <summary>When false, ignored.</summary>
    public bool Active;

    /// <summary>Higher wins.</summary>
    public int Priority;

    /// <summary>Settings.</summary>
    public AudioEnvironmentSettings Settings;
}

/// <summary>Spatial audio environment volume.</summary>
[RequiresComponent<Transform>]
public struct AudioEnvironmentVolumeSource : IComponent
{
    /// <summary>When false, ignored.</summary>
    public bool Active;

    /// <summary>Volume authoring.</summary>
    public AudioEnvironmentVolume Volume;
}

/// <summary>
/// Optional listener override. When active, listener follows this entity's world position
/// instead of the active camera.
/// </summary>
[RequiresComponent<Transform>]
public struct AudioListenerOverride : IComponent
{
    /// <summary>When true, this entity is a listener candidate.</summary>
    public bool Active;

    /// <summary>Higher wins among active overrides.</summary>
    public int Priority;
}
