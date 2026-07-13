using System;
using System.Collections.Generic;

namespace Cyberland.Engine.Audio;

/// <summary>
/// Thread-safe registry of named mix buses with gain, mute, and polyphony settings.
/// </summary>
/// <remarks>
/// Voice gain uses <see cref="GetEffectiveBusGain"/> × master. Unknown bus ids return gain 1 (unmuted).
/// </remarks>
public sealed class AudioBusRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, BusState> _buses = new(StringComparer.Ordinal);

    /// <summary>Registers stock convention buses.</summary>
    public void RegisterStockBuses()
    {
        Register(AudioBusIds.Master, BusRegistration.Default);
        Register(AudioBusIds.Music, BusRegistration.Default);
        Register(AudioBusIds.Sfx, BusRegistration.Default);
        Register(AudioBusIds.Ambient, BusRegistration.Default);
        Register(AudioBusIds.Cinematic, BusRegistration.Default);
        Register(AudioBusIds.Ui, BusRegistration.Default);
    }

    /// <summary>Idempotent register; updates options if the bus already exists.</summary>
    public void Register(string busId, in BusRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(busId))
            return;

        var id = busId.Trim();
        var gain = Math.Clamp(registration.DefaultGain, 0f, 4f);
        lock (_gate)
        {
            if (_buses.TryGetValue(id, out var existing))
            {
                existing.MaxVoices = registration.MaxVoices;
                existing.StealMode = registration.StealMode;
                // Keep current Gain/Muted when re-registering; only seed gain if first time.
                _buses[id] = existing;
                return;
            }

            _buses[id] = new BusState
            {
                Gain = gain,
                Muted = false,
                MaxVoices = registration.MaxVoices,
                StealMode = registration.StealMode,
            };
        }
    }

    /// <summary>Sets linear gain for a registered bus; no-op if unknown.</summary>
    public void SetVolume(string busId, float gain)
    {
        if (string.IsNullOrWhiteSpace(busId))
            return;
        lock (_gate)
        {
            if (!_buses.TryGetValue(busId.Trim(), out var state))
                return;
            state.Gain = Math.Clamp(gain, 0f, 4f);
            _buses[busId.Trim()] = state;
        }
    }

    /// <summary>Returns stored gain, or 1 if the bus is unknown.</summary>
    public float GetVolume(string busId)
    {
        if (string.IsNullOrWhiteSpace(busId))
            return 1f;
        lock (_gate)
        {
            return _buses.TryGetValue(busId.Trim(), out var state) ? state.Gain : 1f;
        }
    }

    /// <summary>Mutes or unmutes without changing the stored slider gain.</summary>
    public void SetMuted(string busId, bool muted)
    {
        if (string.IsNullOrWhiteSpace(busId))
            return;
        lock (_gate)
        {
            if (!_buses.TryGetValue(busId.Trim(), out var state))
                return;
            state.Muted = muted;
            _buses[busId.Trim()] = state;
        }
    }

    /// <summary>True when the bus is registered and muted.</summary>
    public bool IsMuted(string busId)
    {
        if (string.IsNullOrWhiteSpace(busId))
            return false;
        lock (_gate)
        {
            return _buses.TryGetValue(busId.Trim(), out var state) && state.Muted;
        }
    }

    /// <summary>Gain used in the mix: 0 if muted, else stored gain. Unknown → 1.</summary>
    public float GetEffectiveBusGain(string busId)
    {
        if (string.IsNullOrWhiteSpace(busId))
            return 1f;
        lock (_gate)
        {
            if (!_buses.TryGetValue(busId.Trim(), out var state))
                return 1f;
            return state.Muted ? 0f : state.Gain;
        }
    }

    /// <summary>Master effective gain (muted → 0).</summary>
    public float GetMasterGain() => GetEffectiveBusGain(AudioBusIds.Master);

    /// <summary>Copies registered bus ids into <paramref name="destination"/>; returns count.</summary>
    public int CopyBusIds(Span<string> destination)
    {
        lock (_gate)
        {
            var i = 0;
            foreach (var key in _buses.Keys)
            {
                if (i >= destination.Length)
                    break;
                destination[i++] = key;
            }
            return i;
        }
    }

    /// <summary>Number of registered buses.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _buses.Count;
            }
        }
    }

    /// <summary>Tries to read polyphony settings for a bus.</summary>
    public bool TryGetPolyphony(string busId, out int maxVoices, out VoiceStealMode stealMode)
    {
        maxVoices = 0;
        stealMode = VoiceStealMode.StealLowestPriority;
        if (string.IsNullOrWhiteSpace(busId))
            return false;
        lock (_gate)
        {
            if (!_buses.TryGetValue(busId.Trim(), out var state))
                return false;
            maxVoices = state.MaxVoices;
            stealMode = state.StealMode;
            return true;
        }
    }

    /// <summary>True if the bus id is registered.</summary>
    public bool Contains(string busId)
    {
        if (string.IsNullOrWhiteSpace(busId))
            return false;
        lock (_gate)
        {
            return _buses.ContainsKey(busId.Trim());
        }
    }

    private struct BusState
    {
        public float Gain;
        public bool Muted;
        public int MaxVoices;
        public VoiceStealMode StealMode;
    }
}
