using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Cyberland.Engine.Localization;
using Silk.NET.Maths;
using Silk.NET.OpenAL;

namespace Cyberland.Engine.Audio;

/// <summary>
/// OpenAL Soft mixer: dedicated thread owns the context; ECS/mod threads enqueue commands.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Requires an OpenAL device and dedicated mixer thread at runtime.")]
public sealed class OpenALAudioMixer : IAudioService, IDisposable
{
    private const int DefaultMaxVoices = 48;
    private const float AudibilityCull = 0.001f;
    private const float DefaultPanDistance = 320f;

    private readonly ILocalizedContent _localized;
    private readonly AudioBusRegistry _buses = new();
    private readonly AudioDuckMixer _ducks = new();
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly ConcurrentQueue<AudioEnvironmentVolumeSubmission> _envVolumes = new();
    private readonly object _clipGate = new();
    private readonly Dictionary<string, AudioClipId> _pathToClip = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, LoadedClip> _clips = new();
    private readonly Dictionary<string, CueRuntime> _cues = new(StringComparer.Ordinal);
    private readonly List<VoiceSlot> _voices = new();
    private readonly object _statsGate = new();

    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private readonly ManualResetEventSlim _started = new(false);
    private readonly int _maxVoices;

    private OpenALAudioDevice? _device;
    private AL? _al;
    private bool _ready;
    private bool _disposed;
    private int _nextClipId = 1;
    private int _nextVoiceId = 1;
    private ListenerState _listener;
    private AudioEnvironmentSettings _globalEnv = AudioEnvironmentSettings.Default;
    private AudioEnvironmentSettings _blendedEnv = AudioEnvironmentSettings.Default;
    private AudioEnvironmentSettings _targetEnv = AudioEnvironmentSettings.Default;
    private float _envBlendT = 1f;
    private AudioFocusPolicy _focusPolicy = AudioFocusPolicy.Ignore;
    private bool _windowFocused = true;
    private bool _gameplayPaused;
    private float _timeScale = 1f;
    private VoiceId _musicVoice = VoiceId.None;
    private VoiceId _musicFadeOutVoice = VoiceId.None;
    private int _stealsRecent;
    private double _stealWindowStart;
    private readonly Random _rng = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>
    /// Creates the mixer and starts the OpenAL thread. Throws if the device cannot open
    /// (host should catch and use <see cref="NullAudioService"/>).
    /// </summary>
    public OpenALAudioMixer(ILocalizedContent localized, int maxVoices = DefaultMaxVoices)
    {
        _localized = localized ?? throw new ArgumentNullException(nameof(localized));
        _maxVoices = Math.Clamp(maxVoices, 8, 128);
        _buses.RegisterStockBuses();

        _thread = new Thread(MixerThreadMain)
        {
            Name = "Cyberland.AudioMixer",
            IsBackground = true,
        };
        _thread.Start();
        if (!_started.Wait(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("OpenAL mixer thread failed to start.");
        if (!_ready)
            throw new InvalidOperationException("OpenAL mixer failed to open a device.");
    }

    /// <inheritdoc />
    public bool IsReady => _ready && !_disposed;

    /// <inheritdoc />
    public void PlayOneShot(in OneShotRequest request)
    {
        var r = request;
        Enqueue(() => StartOneShot(r));
    }

    /// <inheritdoc />
    public VoiceId PlayLoop(in LoopRequest request)
    {
        var id = new VoiceId(Interlocked.Increment(ref _nextVoiceId));
        var r = request;
        Enqueue(() => StartLoop(id, r));
        return id;
    }

    /// <inheritdoc />
    public void Stop(VoiceId voice, float fadeOutSeconds = 0f)
    {
        var v = voice;
        var fade = fadeOutSeconds;
        Enqueue(() => StopVoice(v, fade));
    }

    /// <inheritdoc />
    public void Pause(VoiceId voice)
    {
        var v = voice;
        Enqueue(() =>
        {
            if (TryFindVoice(v, out var slot) && _al is not null)
            {
                slot.Paused = true;
                _al.SourcePause(slot.Source);
            }
        });
    }

    /// <inheritdoc />
    public void Resume(VoiceId voice)
    {
        var v = voice;
        Enqueue(() =>
        {
            if (TryFindVoice(v, out var slot) && _al is not null && !slot.GameplayHeld)
            {
                slot.Paused = false;
                _al.SourcePlay(slot.Source);
            }
        });
    }

    /// <inheritdoc />
    public void Seek(VoiceId voice, float seconds)
    {
        var v = voice;
        var s = seconds;
        Enqueue(() =>
        {
            if (TryFindVoice(v, out var slot) && _al is not null)
                _al.SetSourceProperty(slot.Source, SourceFloat.SecOffset, Math.Max(0f, s));
        });
    }

    /// <inheritdoc />
    public void SetVoiceParams(VoiceId voice, float? gain = null, float? pitch = null, Vector2D<float>? positionWorld = null)
    {
        var v = voice;
        Enqueue(() =>
        {
            if (!TryFindVoice(v, out var slot))
                return;
            if (gain.HasValue)
                slot.VoiceGain = gain.Value;
            if (pitch.HasValue)
                slot.Pitch = pitch.Value;
            if (positionWorld.HasValue)
                slot.PositionWorld = positionWorld.Value;
        });
    }

    /// <inheritdoc />
    public bool TryGetVoiceState(VoiceId voice, out VoiceState state)
    {
        // Best-effort snapshot without blocking the mixer forever.
        VoiceState snap = default;
        var found = false;
        using var done = new ManualResetEventSlim(false);
        Enqueue(() =>
        {
            if (TryFindVoice(voice, out var slot))
            {
                snap = new VoiceState
                {
                    IsPlaying = true,
                    IsPaused = slot.Paused || slot.GameplayHeld,
                    Envelope = slot.Envelope,
                    BusId = slot.BusId,
                };
                found = true;
            }
            done.Set();
        });
        done.Wait(50);
        state = snap;
        return found;
    }

    /// <inheritdoc />
    public void PlayMusic(in MusicRequest request)
    {
        var r = request;
        Enqueue(() => StartMusic(r, crossfadeOut: 0f));
    }

    /// <inheritdoc />
    public void StopMusic(float fadeOutSeconds = 0f)
    {
        var fade = fadeOutSeconds;
        Enqueue(() =>
        {
            if (_musicVoice.IsValid)
                StopVoice(_musicVoice, fade);
            _musicVoice = VoiceId.None;
        });
    }

    /// <inheritdoc />
    public void CrossfadeMusic(in MusicRequest request, float fadeOutSeconds)
    {
        var r = request;
        var fade = fadeOutSeconds;
        Enqueue(() => StartMusic(r, fade));
    }

    /// <inheritdoc />
    public void RegisterCue(string cueId, in AudioCueDesc desc)
    {
        if (string.IsNullOrWhiteSpace(cueId))
            return;
        var id = cueId.Trim();
        var d = desc;
        Enqueue(() =>
        {
            _cues[id] = new CueRuntime
            {
                Desc = d,
                RoundRobin = 0,
                LastPlaySeconds = -1e9,
                ActiveInstances = 0,
            };
        });
    }

    /// <inheritdoc />
    public void PlayCue(string cueId, in PlayCueRequest request)
    {
        if (string.IsNullOrWhiteSpace(cueId))
            return;
        var id = cueId.Trim();
        var r = request;
        Enqueue(() => PlayCueCore(id, r));
    }

    /// <inheritdoc />
    public void SetListener(in ListenerState state)
    {
        var s = state;
        Enqueue(() => _listener = s);
    }

    /// <inheritdoc />
    public void SetGlobalAudioEnvironment(in AudioEnvironmentSettings settings)
    {
        var s = settings;
        Enqueue(() =>
        {
            _globalEnv = s;
            // Volumes submitted this frame will re-resolve; until then target follows global.
            _targetEnv = s;
            _envBlendT = 0f;
        });
    }

    /// <inheritdoc />
    public void SubmitAudioEnvironmentVolume(
        in AudioEnvironmentVolume volume,
        Vector2D<float> worldPosition,
        float worldRotationRadians,
        Vector2D<float> worldScale)
    {
        _envVolumes.Enqueue(new AudioEnvironmentVolumeSubmission(volume, worldPosition, worldRotationRadians, worldScale));
    }

    /// <inheritdoc />
    public void RegisterBus(string busId, in BusRegistration registration)
    {
        _buses.Register(busId, registration);
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
        var buf = new string[Math.Max(16, _buses.Count)];
        var n = _buses.CopyBusIds(buf);
        var list = new string[n];
        Array.Copy(buf, list, n);
        return list;
    }

    /// <inheritdoc />
    public void RegisterDuckRule(in AudioDuckRule rule)
    {
        var r = rule;
        Enqueue(() => _ducks.Register(r));
    }

    /// <inheritdoc />
    public void ClearDuckRules() => Enqueue(() => _ducks.Clear());

    /// <inheritdoc />
    public void SetFocusPolicy(in AudioFocusPolicy policy)
    {
        var p = policy;
        Enqueue(() => _focusPolicy = p);
    }

    /// <inheritdoc />
    public void SetWindowFocused(bool focused)
    {
        var f = focused;
        Enqueue(() => _windowFocused = f);
    }

    /// <inheritdoc />
    public void SetGameplayAudioPaused(bool paused)
    {
        var p = paused;
        Enqueue(() =>
        {
            _gameplayPaused = p;
            ApplyGameplayPause();
        });
    }

    /// <inheritdoc />
    public void SetTimeScale(float timeScale)
    {
        var t = timeScale;
        Enqueue(() => _timeScale = Math.Max(0f, t));
    }

    /// <inheritdoc />
    public async Task<AudioClipId> LoadClipAsync(string canonicalPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(canonicalPath))
            return AudioClipId.Invalid;

        var path = canonicalPath.Replace('\\', '/').TrimStart('/');
        lock (_clipGate)
        {
            if (_pathToClip.TryGetValue(path, out var existing))
                return existing;
        }

        var bytes = await _localized.TryLoadLocalizedBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
            return AudioClipId.Invalid;

        var decoded = AudioClipDecoder.TryDecode(path, bytes);
        if (decoded is null)
            return AudioClipId.Invalid;

        var tcs = new TaskCompletionSource<AudioClipId>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(() =>
        {
            try
            {
                lock (_clipGate)
                {
                    if (_pathToClip.TryGetValue(path, out var existing))
                    {
                        tcs.TrySetResult(existing);
                        return;
                    }
                }

                if (_al is null)
                {
                    tcs.TrySetResult(AudioClipId.Invalid);
                    return;
                }

                var buffer = _al.GenBuffer();
                var format = ToAlFormat(decoded.Channels, decoded.BitsPerSample);
                _al.BufferData(buffer, format, decoded.InterleavedPcm, decoded.SampleRate);

                var id = new AudioClipId(_nextClipId++);
                var loaded = new LoadedClip
                {
                    Id = id,
                    CanonicalPath = path,
                    Buffer = buffer,
                    SampleRate = decoded.SampleRate,
                    Channels = decoded.Channels,
                    DurationSeconds = decoded.DurationSeconds,
                };
                lock (_clipGate)
                {
                    _clips[id.Value] = loaded;
                    _pathToClip[path] = id;
                }
                tcs.TrySetResult(id);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void UnloadClip(AudioClipId clipId)
    {
        var id = clipId;
        Enqueue(() =>
        {
            if (!id.IsValid || _al is null)
                return;
            lock (_clipGate)
            {
                if (!_clips.TryGetValue(id.Value, out var clip))
                    return;
                // Stop voices using this clip.
                for (var i = _voices.Count - 1; i >= 0; i--)
                {
                    if (_voices[i].ClipId == id)
                        FreeVoiceAt(i);
                }
                _al.DeleteBuffer(clip.Buffer);
                _pathToClip.Remove(clip.CanonicalPath);
                _clips.Remove(id.Value);
            }
        });
    }

    /// <inheritdoc />
    public bool IsClipLoaded(AudioClipId clipId)
    {
        if (!clipId.IsValid)
            return false;
        lock (_clipGate)
            return _clips.ContainsKey(clipId.Value);
    }

    /// <inheritdoc />
    public bool IsClipLoaded(string canonicalPath)
    {
        if (string.IsNullOrWhiteSpace(canonicalPath))
            return false;
        var path = canonicalPath.Replace('\\', '/').TrimStart('/');
        lock (_clipGate)
            return _pathToClip.ContainsKey(path);
    }

    /// <inheritdoc />
    public void GetStats(out AudioMixerStats stats)
    {
        lock (_statsGate)
        {
            stats = new AudioMixerStats
            {
                ActiveVoices = _voices.Count,
                StealsRecent = _stealsRecent,
                RegisteredBusCount = _buses.Count,
                LoadedClipCount = _clips.Count,
                IsReady = IsReady,
            };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cts.Cancel();
        _thread.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
        _started.Dispose();
    }

    private void Enqueue(Action action)
    {
        if (_disposed)
            return;
        _commands.Enqueue(action);
    }

    private void MixerThreadMain()
    {
        try
        {
            _device = new OpenALAudioDevice();
            _al = _device.Al;
            _ready = true;
        }
        catch
        {
            _ready = false;
            _started.Set();
            return;
        }

        _started.Set();
        var last = _clock.Elapsed.TotalSeconds;

        while (!_cts.IsCancellationRequested)
        {
            var now = _clock.Elapsed.TotalSeconds;
            var dt = (float)Math.Clamp(now - last, 0, 0.1);
            last = now;

            while (_commands.TryDequeue(out var cmd))
            {
                try { cmd(); }
                catch { /* keep mixer alive */ }
            }

            ResolveEnvironment();
            _ducks.Tick(dt, bus => BusHasActiveVoice(bus));
            UpdateVoices(dt, now);

            if (now - _stealWindowStart >= 1.0)
            {
                lock (_statsGate)
                {
                    // decay window
                }
                _stealsRecent = 0;
                _stealWindowStart = now;
            }

            Thread.Sleep(5); // ~200 Hz command drain; mix updates ~same
        }

        // Teardown on mixer thread (context current here).
        if (_al is not null)
        {
            for (var i = _voices.Count - 1; i >= 0; i--)
                FreeVoiceAt(i);
            lock (_clipGate)
            {
                foreach (var clip in _clips.Values)
                    _al.DeleteBuffer(clip.Buffer);
                _clips.Clear();
                _pathToClip.Clear();
            }
        }

        _device?.Dispose();
        _device = null;
        _al = null;
        _ready = false;
    }

    private void ResolveEnvironment()
    {
        var list = new List<AudioEnvironmentVolumeSubmission>(8);
        while (_envVolumes.TryDequeue(out var v))
            list.Add(v);

        var resolved = list.Count == 0
            ? _globalEnv
            : AudioEnvironmentMerge.ResolveAtPoint(_globalEnv, list.ToArray(), _listener.PositionWorld);

        // Detect target change → restart blend.
        if (!EnvApproxEqual(_targetEnv, resolved))
        {
            _targetEnv = resolved;
            _envBlendT = 0f;
        }

        var blendSeconds = Math.Max(0.01f, _targetEnv.BlendSeconds);
        _envBlendT = Math.Clamp(_envBlendT + (1f / 60f) / blendSeconds, 0f, 1f);
        // Use small fixed step based on last tick; UpdateVoices passes dt — approximate with 1/60 here if needed.
        _blendedEnv = AudioEnvironmentMerge.Blend(_blendedEnv, _targetEnv, Math.Clamp(1f / (blendSeconds * 60f), 0f, 1f));
    }

    private static bool EnvApproxEqual(in AudioEnvironmentSettings a, in AudioEnvironmentSettings b) =>
        Math.Abs(a.LowPassHz - b.LowPassHz) < 1f
        && Math.Abs(a.MasterScale - b.MasterScale) < 1e-3f
        && Math.Abs(a.BlendSeconds - b.BlendSeconds) < 1e-3f;

    private void UpdateVoices(float dt, double now)
    {
        if (_al is null)
            return;

        var focus = AudioGainMath.FocusFactor(_focusPolicy, _windowFocused);
        var master = _buses.GetMasterGain() * focus * _blendedEnv.MasterScale;

        for (var i = _voices.Count - 1; i >= 0; i--)
        {
            var slot = _voices[i];
            if (slot.DelayRemaining > 0f)
            {
                slot.DelayRemaining -= dt;
                _voices[i] = slot;
                if (slot.DelayRemaining > 0f)
                    continue;
                if (!slot.Paused && !slot.GameplayHeld)
                    _al.SourcePlay(slot.Source);
            }

            if (slot.FadingOut)
            {
                slot.Envelope = AudioFade.AdvanceFadeOut(slot.Envelope, slot.FadeOutSeconds, dt, out var done);
                if (done)
                {
                    FreeVoiceAt(i);
                    continue;
                }
            }
            else if (slot.Envelope < 1f)
            {
                slot.Envelope = AudioFade.AdvanceFadeIn(slot.Envelope, slot.FadeInSeconds, dt, out _);
            }

            var distGain = 1f;
            var pan = slot.Pan;
            if (slot.Space == AudioSpace.World)
            {
                distGain = AudioAttenuation.WorldAudibilityGain(
                    slot.PositionWorld, _listener.PositionWorld,
                    slot.RefDistance, slot.MaxDistance, slot.Rolloff);
                if (distGain <= AudibilityCull && !slot.Looping)
                {
                    FreeVoiceAt(i);
                    continue;
                }
                pan = AudioAttenuation.StereoPan(
                    slot.PositionWorld, _listener.PositionWorld,
                    _listener.RotationRadians, DefaultPanDistance);
            }
            else if (slot.Space == AudioSpace.Cinematic)
            {
                pan = 0f;
            }

            var busGain = _buses.GetEffectiveBusGain(slot.BusId);
            var duck = _ducks.GetMultiplier(slot.BusId);
            var envBus = AudioEnvironmentMerge.GetBusMultiplier(_blendedEnv, slot.BusId);
            var gain = AudioGainMath.Compose(
                1f, slot.VoiceGain, slot.CueJitter, busGain, master, duck, envBus, 1f, 1f) * slot.Envelope * distGain;

            var pitch = slot.Pitch;
            if (slot.ApplyTimeScale)
                pitch *= Math.Max(0.01f, _timeScale);

            _al.SetSourceProperty(slot.Source, SourceFloat.Gain, gain);
            _al.SetSourceProperty(slot.Source, SourceFloat.Pitch, Math.Clamp(pitch, 0.01f, 4f));
            // Simple stereo pan via OpenAL position on X (listener at origin in AL space).
            _al.SetSourceProperty(slot.Source, SourceVector3.Position, pan, 0f, 0f);

            _al.GetSourceProperty(slot.Source, GetSourceInteger.SourceState, out var state);
            if (!slot.Looping && !slot.FadingOut && state == (int)SourceState.Stopped && slot.DelayRemaining <= 0f)
            {
                FreeVoiceAt(i);
                continue;
            }

            _voices[i] = slot;
        }

        // Keep AL listener at origin; we pan sources in listener-relative X.
        _al.SetListenerProperty(ListenerVector3.Position, 0f, 0f, 0f);
    }

    private void StartOneShot(in OneShotRequest request)
    {
        var bus = ResolveBus(request.BusId, request.Space, forMusic: false, forLoop: false);
        if (!TryResolveClip(request.ClipId, request.ClipPath, out var clip))
            return;
        if (!TryAllocateVoice(bus, request.Priority, out var slotIndex))
            return;

        var id = new VoiceId(_nextVoiceId++);
        var slot = CreateSlot(id, clip, bus, request.Space, request.Gain <= 0f ? 1f : request.Gain,
            request.Pitch <= 0f ? 1f : request.Pitch, request.Priority, request.FadeInSeconds,
            request.PauseWithGameplay, request.ApplyTimeScale, looping: false,
            request.PositionWorld, request.RefDistance > 0 ? request.RefDistance : 64f,
            request.MaxDistance > 0 ? request.MaxDistance : 480f,
            request.Rolloff > 0 ? request.Rolloff : 1f, request.Pan, request.DelaySeconds, cueJitter: 1f);
        BindAndPlay(slot, slotIndex);
    }

    private void StartLoop(VoiceId id, in LoopRequest request)
    {
        var bus = ResolveBus(request.BusId, request.Space, forMusic: false, forLoop: true);
        if (!TryResolveClip(request.ClipId, request.ClipPath, out var clip))
            return;
        if (!TryAllocateVoice(bus, request.Priority, out var slotIndex))
            return;

        var slot = CreateSlot(id, clip, bus, request.Space, request.Gain <= 0f ? 1f : request.Gain,
            request.Pitch <= 0f ? 1f : request.Pitch, request.Priority, request.FadeInSeconds,
            request.PauseWithGameplay, request.ApplyTimeScale, looping: true,
            request.PositionWorld, request.RefDistance > 0 ? request.RefDistance : 64f,
            request.MaxDistance > 0 ? request.MaxDistance : 480f,
            request.Rolloff > 0 ? request.Rolloff : 1f, request.Pan, 0f, cueJitter: 1f);
        BindAndPlay(slot, slotIndex);
    }

    private void StartMusic(in MusicRequest request, float crossfadeOut)
    {
        if (_musicVoice.IsValid)
        {
            _musicFadeOutVoice = _musicVoice;
            StopVoice(_musicVoice, crossfadeOut);
        }

        var bus = string.IsNullOrWhiteSpace(request.BusId) ? AudioBusIds.Music : request.BusId!;
        if (!TryResolveClip(request.ClipId, request.ClipPath, out var clip))
            return;
        if (!TryAllocateVoice(bus, 100, out var slotIndex))
            return;

        var id = new VoiceId(_nextVoiceId++);
        var slot = CreateSlot(id, clip, bus, AudioSpace.Direct,
            request.Gain <= 0f ? 1f : request.Gain, 1f, 100,
            request.FadeInSeconds, request.PauseWithGameplay, applyTimeScale: false,
            looping: request.Loop, default, 64f, 480f, 1f, 0f, 0f, 1f);
        BindAndPlay(slot, slotIndex);
        _musicVoice = id;
    }

    private void PlayCueCore(string cueId, in PlayCueRequest request)
    {
        if (!_cues.TryGetValue(cueId, out var cue))
            return;
        var now = _clock.Elapsed.TotalSeconds;
        if (!VoiceLimitLogic.CooldownAllows(now, cue.LastPlaySeconds, cue.Desc.CooldownSeconds))
            return;
        if (cue.Desc.MaxInstances > 0 && cue.ActiveInstances >= cue.Desc.MaxInstances)
            return;

        var paths = cue.Desc.ClipPaths;
        if (paths is null || paths.Length == 0)
            return;

        var idx = AudioCueVariation.PickIndex(paths.Length, cue.Desc.PickMode, ref cue.RoundRobin, _rng);
        if (idx < 0)
            return;

        var pitchMin = cue.Desc.PitchMin > 0 ? cue.Desc.PitchMin : 1f;
        var pitchMax = cue.Desc.PitchMax > 0 ? cue.Desc.PitchMax : pitchMin;
        var gainMin = cue.Desc.GainMin > 0 ? cue.Desc.GainMin : 1f;
        var gainMax = cue.Desc.GainMax > 0 ? cue.Desc.GainMax : gainMin;
        var pitch = AudioCueVariation.SampleRange(pitchMin, pitchMax, _rng);
        var jitter = AudioCueVariation.SampleRange(gainMin, gainMax, _rng);
        var space = request.HasSpace ? request.Space : cue.Desc.Space;
        var bus = !string.IsNullOrWhiteSpace(request.BusId)
            ? request.BusId!
            : (string.IsNullOrWhiteSpace(cue.Desc.BusId) ? AudioBusIds.Sfx : cue.Desc.BusId!);
        var priority = request.Priority == int.MinValue ? cue.Desc.Priority : request.Priority;
        var gain = (request.Gain > 0f ? request.Gain : 1f);

        if (!TryResolveClip(AudioClipId.Invalid, paths[idx], out var clip))
            return;
        if (!TryAllocateVoice(bus, priority, out var slotIndex))
            return;

        var id = new VoiceId(_nextVoiceId++);
        var slot = CreateSlot(id, clip, bus, space, gain, pitch, priority, 0f,
            pauseWithGameplay: true, applyTimeScale: true, looping: false,
            request.PositionWorld,
            cue.Desc.RefDistance > 0 ? cue.Desc.RefDistance : 64f,
            cue.Desc.MaxDistance > 0 ? cue.Desc.MaxDistance : 480f,
            cue.Desc.Rolloff > 0 ? cue.Desc.Rolloff : 1f, 0f, 0f, jitter);
        slot.CueId = cueId;
        BindAndPlay(slot, slotIndex);

        cue.LastPlaySeconds = now;
        cue.ActiveInstances++;
        _cues[cueId] = cue;
    }

    private VoiceSlot CreateSlot(
        VoiceId id, LoadedClip clip, string bus, AudioSpace space,
        float gain, float pitch, int priority, float fadeIn,
        bool pauseWithGameplay, bool applyTimeScale, bool looping,
        Vector2D<float> pos, float refDist, float maxDist, float rolloff, float pan, float delay, float cueJitter)
    {
        return new VoiceSlot
        {
            Id = id,
            ClipId = clip.Id,
            Buffer = clip.Buffer,
            BusId = bus,
            Space = space,
            VoiceGain = gain,
            Pitch = pitch,
            Priority = priority,
            FadeInSeconds = fadeIn,
            Envelope = fadeIn <= 1e-6f ? 1f : 0f,
            PauseWithGameplay = pauseWithGameplay,
            ApplyTimeScale = applyTimeScale,
            Looping = looping,
            PositionWorld = pos,
            RefDistance = refDist,
            MaxDistance = maxDist,
            Rolloff = rolloff,
            Pan = pan,
            DelayRemaining = delay,
            CueJitter = cueJitter,
            StartedAtSeconds = _clock.Elapsed.TotalSeconds,
            GameplayHeld = pauseWithGameplay && _gameplayPaused,
        };
    }

    private void BindAndPlay(VoiceSlot slot, int replaceIndex)
    {
        if (_al is null)
            return;
        var source = _al.GenSource();
        slot.Source = source;
        _al.SetSourceProperty(source, SourceBoolean.Looping, slot.Looping);
        _al.SetSourceProperty(source, SourceInteger.Buffer, slot.Buffer);
        _al.SetSourceProperty(source, SourceFloat.Gain, 0f);
        _al.SetSourceProperty(source, SourceBoolean.SourceRelative, true);

        if (replaceIndex >= 0 && replaceIndex < _voices.Count)
            _voices[replaceIndex] = slot;
        else
            _voices.Add(slot);

        if (slot.DelayRemaining <= 0f && !slot.Paused && !slot.GameplayHeld)
            _al.SourcePlay(source);
    }

    private bool TryAllocateVoice(string bus, int priority, out int replaceIndex)
    {
        replaceIndex = -1;

        // Per-bus cap
        if (_buses.TryGetPolyphony(bus, out var maxBus, out var stealMode) && maxBus > 0)
        {
            var candidates = CollectBusCandidates(bus);
            var decision = VoiceLimitLogic.ChooseSteal(candidates, candidates.Length, maxBus, stealMode, priority);
            if (decision == -2)
                return false;
            if (decision >= 0)
            {
                StealSlot(decision);
                _stealsRecent++;
            }
        }

        // Global cap
        if (_voices.Count >= _maxVoices)
        {
            var all = CollectAllCandidates();
            var decision = VoiceLimitLogic.ChooseSteal(all, all.Length, _maxVoices, VoiceStealMode.StealLowestPriority, priority);
            if (decision == -2)
                return false;
            if (decision >= 0)
            {
                StealSlot(decision);
                _stealsRecent++;
            }
        }

        return true;
    }

    private VoiceLimitLogic.VoiceCandidate[] CollectBusCandidates(string bus)
    {
        var list = new List<VoiceLimitLogic.VoiceCandidate>(_voices.Count);
        for (var i = 0; i < _voices.Count; i++)
        {
            if (string.Equals(_voices[i].BusId, bus, StringComparison.Ordinal))
                list.Add(new VoiceLimitLogic.VoiceCandidate(i, _voices[i].Priority, _voices[i].StartedAtSeconds));
        }
        return list.ToArray();
    }

    private VoiceLimitLogic.VoiceCandidate[] CollectAllCandidates()
    {
        var list = new VoiceLimitLogic.VoiceCandidate[_voices.Count];
        for (var i = 0; i < _voices.Count; i++)
            list[i] = new VoiceLimitLogic.VoiceCandidate(i, _voices[i].Priority, _voices[i].StartedAtSeconds);
        return list;
    }

    private void StealSlot(int index)
    {
        if (index < 0 || index >= _voices.Count)
            return;
        FreeVoiceAt(index);
    }

    private void StopVoice(VoiceId id, float fadeOut)
    {
        if (!TryFindVoice(id, out var slot))
            return;
        if (fadeOut <= 1e-6f)
        {
            for (var i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Id == id)
                {
                    FreeVoiceAt(i);
                    return;
                }
            }
            return;
        }
        slot.FadingOut = true;
        slot.FadeOutSeconds = fadeOut;
        for (var i = 0; i < _voices.Count; i++)
        {
            if (_voices[i].Id == id)
            {
                _voices[i] = slot;
                break;
            }
        }
    }

    private void FreeVoiceAt(int index)
    {
        if (_al is null || index < 0 || index >= _voices.Count)
            return;
        var slot = _voices[index];
        _al.SourceStop(slot.Source);
        _al.DeleteSource(slot.Source);
        if (!string.IsNullOrEmpty(slot.CueId) && _cues.TryGetValue(slot.CueId, out var cue))
        {
            cue.ActiveInstances = Math.Max(0, cue.ActiveInstances - 1);
            _cues[slot.CueId] = cue;
        }
        if (slot.Id == _musicVoice)
            _musicVoice = VoiceId.None;
        _voices.RemoveAt(index);
    }

    private bool TryFindVoice(VoiceId id, out VoiceSlot slot)
    {
        for (var i = 0; i < _voices.Count; i++)
        {
            if (_voices[i].Id == id)
            {
                slot = _voices[i];
                return true;
            }
        }
        slot = default;
        return false;
    }

    private bool BusHasActiveVoice(string busId)
    {
        for (var i = 0; i < _voices.Count; i++)
        {
            if (string.Equals(_voices[i].BusId, busId, StringComparison.Ordinal)
                && !_voices[i].FadingOut
                && !_voices[i].Paused)
                return true;
        }
        return false;
    }

    private void ApplyGameplayPause()
    {
        if (_al is null)
            return;
        for (var i = 0; i < _voices.Count; i++)
        {
            var slot = _voices[i];
            if (!slot.PauseWithGameplay)
                continue;
            if (_gameplayPaused)
            {
                slot.GameplayHeld = true;
                _al.SourcePause(slot.Source);
            }
            else if (slot.GameplayHeld)
            {
                slot.GameplayHeld = false;
                if (!slot.Paused)
                    _al.SourcePlay(slot.Source);
            }
            _voices[i] = slot;
        }
    }

    private bool TryResolveClip(AudioClipId clipId, string? path, out LoadedClip clip)
    {
        clip = default!;
        lock (_clipGate)
        {
            if (clipId.IsValid && _clips.TryGetValue(clipId.Value, out var byId))
            {
                clip = byId;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(path))
            {
                var p = path.Replace('\\', '/').TrimStart('/');
                if (_pathToClip.TryGetValue(p, out var id) && _clips.TryGetValue(id.Value, out var byPath))
                {
                    clip = byPath;
                    return true;
                }
            }
        }

        // Synchronous load on mixer thread for convenience (still localized).
        if (string.IsNullOrWhiteSpace(path) || _al is null)
            return false;
        var canonical = path.Replace('\\', '/').TrimStart('/');
        var bytes = _localized.TryLoadLocalizedBytesAsync(canonical).GetAwaiter().GetResult();
        if (bytes is null)
            return false;
        var decoded = AudioClipDecoder.TryDecode(canonical, bytes);
        if (decoded is null)
            return false;

        var buffer = _al.GenBuffer();
        _al.BufferData(buffer, ToAlFormat(decoded.Channels, decoded.BitsPerSample), decoded.InterleavedPcm, decoded.SampleRate);
        var newId = new AudioClipId(_nextClipId++);
        clip = new LoadedClip
        {
            Id = newId,
            CanonicalPath = canonical,
            Buffer = buffer,
            SampleRate = decoded.SampleRate,
            Channels = decoded.Channels,
            DurationSeconds = decoded.DurationSeconds,
        };
        lock (_clipGate)
        {
            _clips[newId.Value] = clip;
            _pathToClip[canonical] = newId;
        }
        return true;
    }

    private static string ResolveBus(string? busId, AudioSpace space, bool forMusic, bool forLoop)
    {
        if (!string.IsNullOrWhiteSpace(busId))
            return busId.Trim();
        if (forMusic)
            return AudioBusIds.Music;
        if (space == AudioSpace.Cinematic)
            return AudioBusIds.Cinematic;
        if (forLoop)
            return AudioBusIds.Ambient;
        if (space == AudioSpace.Direct)
            return AudioBusIds.Sfx;
        return AudioBusIds.Sfx;
    }

    private static BufferFormat ToAlFormat(int channels, int bits) =>
        (channels, bits) switch
        {
            (1, 8) => BufferFormat.Mono8,
            (1, 16) => BufferFormat.Mono16,
            (2, 8) => BufferFormat.Stereo8,
            (2, 16) => BufferFormat.Stereo16,
            _ => BufferFormat.Mono16,
        };

    private sealed class LoadedClip
    {
        public AudioClipId Id;
        public string CanonicalPath = "";
        public uint Buffer;
        public int SampleRate;
        public int Channels;
        public double DurationSeconds;
    }

    private struct VoiceSlot
    {
        public VoiceId Id;
        public AudioClipId ClipId;
        public uint Source;
        public uint Buffer;
        public string BusId;
        public AudioSpace Space;
        public float VoiceGain;
        public float Pitch;
        public int Priority;
        public float FadeInSeconds;
        public float FadeOutSeconds;
        public float Envelope;
        public bool FadingOut;
        public bool PauseWithGameplay;
        public bool ApplyTimeScale;
        public bool Looping;
        public bool Paused;
        public bool GameplayHeld;
        public Vector2D<float> PositionWorld;
        public float RefDistance;
        public float MaxDistance;
        public float Rolloff;
        public float Pan;
        public float DelayRemaining;
        public float CueJitter;
        public double StartedAtSeconds;
        public string? CueId;
    }

    private struct CueRuntime
    {
        public AudioCueDesc Desc;
        public int RoundRobin;
        public double LastPlaySeconds;
        public int ActiveInstances;
    }
}
