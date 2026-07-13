using System;
using System.Collections.Generic;
using Silk.NET.Maths;

namespace Cyberland.Engine.Audio;

/// <summary>One sparse bus gain multiplier for audio environments.</summary>
public struct AudioBusGainEntry
{
    /// <summary>Target bus id.</summary>
    public string BusId;

    /// <summary>Linear multiplier applied onto the resolved environment.</summary>
    public float Gain;
}

/// <summary>Baseline / resolved audio environment settings.</summary>
public struct AudioEnvironmentSettings
{
    /// <summary>Seconds to blend when the winning volume changes.</summary>
    public float BlendSeconds;

    /// <summary>Low-pass cutoff in Hz (high values ≈ bypass).</summary>
    public float LowPassHz;

    /// <summary>Optional master scale for this environment (1 = unchanged).</summary>
    public float MasterScale;

    /// <summary>Sparse per-bus gain multipliers (null/empty = none).</summary>
    public AudioBusGainEntry[]? BusGains;

    /// <summary>Neutral defaults.</summary>
    public static AudioEnvironmentSettings Default => new()
    {
        BlendSeconds = 1f,
        LowPassHz = 22000f,
        MasterScale = 1f,
        BusGains = null,
    };
}

/// <summary>Sparse overrides applied onto global settings when a volume wins.</summary>
public struct AudioEnvironmentOverrides
{
    /// <summary>When true, <see cref="BlendSeconds"/> replaces the global blend time.</summary>
    public bool HasBlendSeconds;

    /// <summary>Override blend seconds.</summary>
    public float BlendSeconds;

    /// <summary>When true, <see cref="LowPassHz"/> replaces the global cutoff.</summary>
    public bool HasLowPassHz;

    /// <summary>Override low-pass Hz.</summary>
    public float LowPassHz;

    /// <summary>When true, <see cref="MasterScale"/> multiplies the global master scale.</summary>
    public bool HasMasterScale;

    /// <summary>Master scale multiplier.</summary>
    public float MasterScale;

    /// <summary>Sparse bus gains to multiply onto the result.</summary>
    public AudioBusGainEntry[]? BusGains;
}

/// <summary>Spatial audio environment volume (oriented box in world space).</summary>
public struct AudioEnvironmentVolume
{
    /// <summary>Half extents in local space before world scale.</summary>
    public Vector2D<float> HalfExtentsLocal;

    /// <summary>Higher wins on overlap.</summary>
    public int Priority;

    /// <summary>Sparse overrides.</summary>
    public AudioEnvironmentOverrides Overrides;
}

/// <summary>One submitted volume with world transform for merge.</summary>
public readonly struct AudioEnvironmentVolumeSubmission
{
    /// <summary>Creates a submission.</summary>
    public AudioEnvironmentVolumeSubmission(
        in AudioEnvironmentVolume volume,
        Vector2D<float> worldPosition,
        float worldRotationRadians,
        Vector2D<float> worldScale)
    {
        Volume = volume;
        WorldPosition = worldPosition;
        WorldRotationRadians = worldRotationRadians;
        WorldScale = worldScale;
    }

    /// <summary>Authored volume.</summary>
    public AudioEnvironmentVolume Volume { get; }

    /// <summary>World center.</summary>
    public Vector2D<float> WorldPosition { get; }

    /// <summary>World rotation (radians, CCW).</summary>
    public float WorldRotationRadians { get; }

    /// <summary>World scale.</summary>
    public Vector2D<float> WorldScale { get; }
}

/// <summary>
/// Merges global audio environment with the highest-priority volume containing the listener.
/// Reuses oriented-box containment from post-process volumes.
/// </summary>
public static class AudioEnvironmentMerge
{
    /// <summary>
    /// Picks the highest-priority volume containing <paramref name="listenerWorld"/> and applies overrides.
    /// </summary>
    public static AudioEnvironmentSettings ResolveAtPoint(
        in AudioEnvironmentSettings global,
        ReadOnlySpan<AudioEnvironmentVolumeSubmission> volumes,
        Vector2D<float> listenerWorld)
    {
        var bestPri = int.MinValue;
        var bestIdx = -1;

        for (var i = 0; i < volumes.Length; i++)
        {
            ref readonly var submitted = ref volumes[i];
            var v = submitted.Volume;
            var halfExtentsWorld = new Vector2D<float>(
                v.HalfExtentsLocal.X * submitted.WorldScale.X,
                v.HalfExtentsLocal.Y * submitted.WorldScale.Y);
            if (!Rendering.PostProcessVolumeMerge.ContainsPoint(
                    submitted.WorldPosition,
                    halfExtentsWorld,
                    submitted.WorldRotationRadians,
                    listenerWorld))
                continue;

            if (bestIdx >= 0 && v.Priority <= bestPri)
                continue;

            bestPri = v.Priority;
            bestIdx = i;
        }

        if (bestIdx < 0)
            return CloneSettings(global);

        var result = CloneSettings(global);
        ApplyOverrides(ref result, volumes[bestIdx].Volume.Overrides);
        return result;
    }

    /// <summary>Blends numeric fields and bus maps from <paramref name="from"/> toward <paramref name="to"/>.</summary>
    public static AudioEnvironmentSettings Blend(
        in AudioEnvironmentSettings from,
        in AudioEnvironmentSettings to,
        float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var result = new AudioEnvironmentSettings
        {
            BlendSeconds = Lerp(from.BlendSeconds, to.BlendSeconds, t),
            LowPassHz = Lerp(from.LowPassHz, to.LowPassHz, t),
            MasterScale = Lerp(from.MasterScale, to.MasterScale, t),
            BusGains = BlendBusGains(from.BusGains, to.BusGains, t),
        };
        return result;
    }

    /// <summary>Looks up a bus multiplier from sparse gains (default 1).</summary>
    public static float GetBusMultiplier(in AudioEnvironmentSettings settings, string busId)
    {
        if (settings.BusGains is null || string.IsNullOrWhiteSpace(busId))
            return 1f;
        var id = busId.Trim();
        foreach (var e in settings.BusGains)
        {
            if (string.Equals(e.BusId, id, StringComparison.Ordinal))
                return e.Gain;
        }
        return 1f;
    }

    private static void ApplyOverrides(ref AudioEnvironmentSettings g, in AudioEnvironmentOverrides o)
    {
        if (o.HasBlendSeconds)
            g.BlendSeconds = o.BlendSeconds;
        if (o.HasLowPassHz)
            g.LowPassHz = o.LowPassHz;
        if (o.HasMasterScale)
            g.MasterScale *= o.MasterScale;
        if (o.BusGains is { Length: > 0 })
            g.BusGains = MergeBusGains(g.BusGains, o.BusGains);
    }

    private static AudioBusGainEntry[]? MergeBusGains(AudioBusGainEntry[]? baseline, AudioBusGainEntry[] overlay)
    {
        var map = new Dictionary<string, float>(StringComparer.Ordinal);
        if (baseline is not null)
        {
            foreach (var e in baseline)
            {
                if (string.IsNullOrWhiteSpace(e.BusId))
                    continue;
                map[e.BusId.Trim()] = e.Gain;
            }
        }

        foreach (var e in overlay)
        {
            if (string.IsNullOrWhiteSpace(e.BusId))
                continue;
            var id = e.BusId.Trim();
            map[id] = map.TryGetValue(id, out var prev) ? prev * e.Gain : e.Gain;
        }

        if (map.Count == 0)
            return null;

        var arr = new AudioBusGainEntry[map.Count];
        var i = 0;
        foreach (var (k, v) in map)
            arr[i++] = new AudioBusGainEntry { BusId = k, Gain = v };
        return arr;
    }

    private static AudioBusGainEntry[]? BlendBusGains(AudioBusGainEntry[]? a, AudioBusGainEntry[]? b, float t)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (a is not null)
        {
            foreach (var e in a)
                if (!string.IsNullOrWhiteSpace(e.BusId))
                    keys.Add(e.BusId.Trim());
        }

        if (b is not null)
        {
            foreach (var e in b)
                if (!string.IsNullOrWhiteSpace(e.BusId))
                    keys.Add(e.BusId.Trim());
        }

        if (keys.Count == 0)
            return null;

        var arr = new AudioBusGainEntry[keys.Count];
        var i = 0;
        foreach (var k in keys)
        {
            var ga = Lookup(a, k);
            var gb = Lookup(b, k);
            arr[i++] = new AudioBusGainEntry { BusId = k, Gain = Lerp(ga, gb, t) };
        }
        return arr;
    }

    private static float Lookup(AudioBusGainEntry[]? entries, string busId)
    {
        if (entries is null)
            return 1f;
        foreach (var e in entries)
        {
            if (string.Equals(e.BusId, busId, StringComparison.Ordinal))
                return e.Gain;
        }
        return 1f;
    }

    private static AudioEnvironmentSettings CloneSettings(in AudioEnvironmentSettings s) => new()
    {
        BlendSeconds = s.BlendSeconds,
        LowPassHz = s.LowPassHz,
        MasterScale = s.MasterScale,
        BusGains = s.BusGains is null ? null : (AudioBusGainEntry[])s.BusGains.Clone(),
    };

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
