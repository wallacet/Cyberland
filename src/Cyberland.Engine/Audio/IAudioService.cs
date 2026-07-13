using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cyberland.Engine.Audio;

/// <summary>
/// Mod-facing audio API: playback, buses, cues, environments, and clip loading.
/// </summary>
/// <remarks>
/// <para>
/// Threading: play/submit/register methods are safe from <see cref="Core.Ecs.IParallelSystem"/> workers
/// (implementations synchronize onto a dedicated mixer thread that owns the OpenAL context).
/// </para>
/// <para>
/// Clip paths are always <b>canonical</b> content paths resolved through <see cref="Localization.ILocalizedContent"/>.
/// </para>
/// <para>
/// When OpenAL is unavailable the host assigns <see cref="NullAudioService"/> so callers never need null checks.
/// </para>
/// </remarks>
public interface IAudioService
{
    /// <summary>True once the mixer thread and device are live (always true for <see cref="NullAudioService"/> readiness semantics: false).</summary>
    bool IsReady { get; }

    /// <summary>Fire-and-forget one-shot.</summary>
    void PlayOneShot(in OneShotRequest request);

    /// <summary>Starts a looping voice; returns <see cref="VoiceId.None"/> on failure.</summary>
    VoiceId PlayLoop(in LoopRequest request);

    /// <summary>Stops a voice with optional fade-out.</summary>
    void Stop(VoiceId voice, float fadeOutSeconds = 0f);

    /// <summary>Pauses a voice.</summary>
    void Pause(VoiceId voice);

    /// <summary>Resumes a paused voice.</summary>
    void Resume(VoiceId voice);

    /// <summary>Seeks a voice to <paramref name="seconds"/> from the start (best-effort for streams).</summary>
    void Seek(VoiceId voice, float seconds);

    /// <summary>Updates gain/pitch/world position for an active voice.</summary>
    void SetVoiceParams(VoiceId voice, float? gain = null, float? pitch = null, Silk.NET.Maths.Vector2D<float>? positionWorld = null);

    /// <summary>Queries voice state.</summary>
    bool TryGetVoiceState(VoiceId voice, out VoiceState state);

    /// <summary>Starts or replaces the music bed.</summary>
    void PlayMusic(in MusicRequest request);

    /// <summary>Stops music with optional fade.</summary>
    void StopMusic(float fadeOutSeconds = 0f);

    /// <summary>Crossfades from the current bed to <paramref name="request"/>.</summary>
    void CrossfadeMusic(in MusicRequest request, float fadeOutSeconds);

    /// <summary>Registers a multi-clip cue.</summary>
    void RegisterCue(string cueId, in AudioCueDesc desc);

    /// <summary>Plays a registered cue.</summary>
    void PlayCue(string cueId, in PlayCueRequest request);

    /// <summary>Sets the listener pose used for world spatialization.</summary>
    void SetListener(in ListenerState state);

    /// <summary>Sets the global audio environment baseline (persists until next call).</summary>
    void SetGlobalAudioEnvironment(in AudioEnvironmentSettings settings);

    /// <summary>Submits one spatial environment volume for this frame (drained each mixer tick).</summary>
    void SubmitAudioEnvironmentVolume(
        in AudioEnvironmentVolume volume,
        Silk.NET.Maths.Vector2D<float> worldPosition,
        float worldRotationRadians,
        Silk.NET.Maths.Vector2D<float> worldScale);

    /// <summary>Declares a named mix bus (idempotent).</summary>
    void RegisterBus(string busId, in BusRegistration registration);

    /// <summary>Declares a named mix bus with default registration.</summary>
    void RegisterBus(string busId, float defaultGain = 1f) =>
        RegisterBus(busId, new BusRegistration
        {
            DefaultGain = defaultGain,
            MaxVoices = 0,
            StealMode = VoiceStealMode.StealLowestPriority,
        });

    /// <summary>Sets bus linear gain.</summary>
    void SetBusVolume(string busId, float gain);

    /// <summary>Gets bus linear gain (1 if unknown).</summary>
    float GetBusVolume(string busId);

    /// <summary>Mutes a bus without changing its slider gain.</summary>
    void SetBusMuted(string busId, bool muted);

    /// <summary>True when the bus is muted.</summary>
    bool IsBusMuted(string busId);

    /// <summary>Enumerates registered bus ids.</summary>
    IReadOnlyList<string> EnumerateBuses();

    /// <summary>Registers a duck rule.</summary>
    void RegisterDuckRule(in AudioDuckRule rule);

    /// <summary>Clears all duck rules.</summary>
    void ClearDuckRules();

    /// <summary>Sets window-focus audio policy.</summary>
    void SetFocusPolicy(in AudioFocusPolicy policy);

    /// <summary>Host reports whether the window is focused.</summary>
    void SetWindowFocused(bool focused);

    /// <summary>Pauses/ducks voices marked <c>PauseWithGameplay</c>.</summary>
    void SetGameplayAudioPaused(bool paused);

    /// <summary>Session time scale for voices with <c>ApplyTimeScale</c>.</summary>
    void SetTimeScale(float timeScale);

    /// <summary>Loads and caches a clip via localized content. Returns invalid id on failure.</summary>
    Task<AudioClipId> LoadClipAsync(string canonicalPath, CancellationToken cancellationToken = default);

    /// <summary>Unloads a clip from the cache (stops voices using it).</summary>
    void UnloadClip(AudioClipId clipId);

    /// <summary>True when the clip id is loaded.</summary>
    bool IsClipLoaded(AudioClipId clipId);

    /// <summary>True when a canonical path is already cached.</summary>
    bool IsClipLoaded(string canonicalPath);

    /// <summary>Fills mixer diagnostics.</summary>
    void GetStats(out AudioMixerStats stats);
}
