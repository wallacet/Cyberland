using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Audio;

/// <summary>
/// Silent <see cref="IAudioService"/> used when OpenAL is unavailable. Registers succeed; playback is a no-op.
/// </summary>
public sealed class NullAudioService : IAudioService
{
    private readonly AudioBusRegistry _buses = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, AudioCueDesc> _cues = new(StringComparer.Ordinal);
    private readonly List<string> _busList = new();

    /// <summary>Creates a null service with stock buses registered.</summary>
    public NullAudioService()
    {
        _buses.RegisterStockBuses();
        RefreshBusList();
    }

    /// <inheritdoc />
    public bool IsReady => false;

    /// <inheritdoc />
    public void PlayOneShot(in OneShotRequest request) { }

    /// <inheritdoc />
    public VoiceId PlayLoop(in LoopRequest request) => VoiceId.None;

    /// <inheritdoc />
    public void Stop(VoiceId voice, float fadeOutSeconds = 0f) { }

    /// <inheritdoc />
    public void Pause(VoiceId voice) { }

    /// <inheritdoc />
    public void Resume(VoiceId voice) { }

    /// <inheritdoc />
    public void Seek(VoiceId voice, float seconds) { }

    /// <inheritdoc />
    public void SetVoiceParams(VoiceId voice, float? gain = null, float? pitch = null, Vector2D<float>? positionWorld = null) { }

    /// <inheritdoc />
    public bool TryGetVoiceState(VoiceId voice, out VoiceState state)
    {
        state = default;
        return false;
    }

    /// <inheritdoc />
    public void PlayMusic(in MusicRequest request) { }

    /// <inheritdoc />
    public void StopMusic(float fadeOutSeconds = 0f) { }

    /// <inheritdoc />
    public void CrossfadeMusic(in MusicRequest request, float fadeOutSeconds) { }

    /// <inheritdoc />
    public void RegisterCue(string cueId, in AudioCueDesc desc)
    {
        if (string.IsNullOrWhiteSpace(cueId))
            return;
        lock (_gate)
            _cues[cueId.Trim()] = desc;
    }

    /// <inheritdoc />
    public void PlayCue(string cueId, in PlayCueRequest request) { }

    /// <inheritdoc />
    public void SetListener(in ListenerState state) { }

    /// <inheritdoc />
    public void SetGlobalAudioEnvironment(in AudioEnvironmentSettings settings) { }

    /// <inheritdoc />
    public void SubmitAudioEnvironmentVolume(
        in AudioEnvironmentVolume volume,
        Vector2D<float> worldPosition,
        float worldRotationRadians,
        Vector2D<float> worldScale)
    {
    }

    /// <inheritdoc />
    public void RegisterBus(string busId, in BusRegistration registration)
    {
        _buses.Register(busId, registration);
        RefreshBusList();
    }

    /// <inheritdoc />
    public void SetBusVolume(string busId, float gain) => _buses.SetVolume(busId, gain);

    /// <inheritdoc />
    public float GetBusVolume(string busId) => _buses.GetVolume(busId);

    /// <inheritdoc />
    public void SetBusMuted(string busId, bool muted) => _buses.SetMuted(busId, muted);

    /// <inheritdoc />
    public bool IsBusMuted(string busId) => _buses.IsMuted(busId);

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateBuses()
    {
        lock (_gate)
            return _busList.ToArray();
    }

    /// <inheritdoc />
    public void RegisterDuckRule(in AudioDuckRule rule) { }

    /// <inheritdoc />
    public void ClearDuckRules() { }

    /// <inheritdoc />
    public void SetFocusPolicy(in AudioFocusPolicy policy) { }

    /// <inheritdoc />
    public void SetWindowFocused(bool focused) { }

    /// <inheritdoc />
    public void SetGameplayAudioPaused(bool paused) { }

    /// <inheritdoc />
    public void SetTimeScale(float timeScale) { }

    /// <inheritdoc />
    public Task<AudioClipId> LoadClipAsync(string canonicalPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(AudioClipId.Invalid);

    /// <inheritdoc />
    public void UnloadClip(AudioClipId clipId) { }

    /// <inheritdoc />
    public bool IsClipLoaded(AudioClipId clipId) => false;

    /// <inheritdoc />
    public bool IsClipLoaded(string canonicalPath) => false;

    /// <inheritdoc />
    public void GetStats(out AudioMixerStats stats)
    {
        stats = new AudioMixerStats
        {
            ActiveVoices = 0,
            StealsRecent = 0,
            RegisteredBusCount = _buses.Count,
            LoadedClipCount = 0,
            IsReady = false,
        };
    }

    private void RefreshBusList()
    {
        lock (_gate)
        {
            _busList.Clear();
            var buf = new string[Math.Max(8, _buses.Count)];
            var n = _buses.CopyBusIds(buf);
            for (var i = 0; i < n; i++)
                _busList.Add(buf[i]);
        }
    }
}
