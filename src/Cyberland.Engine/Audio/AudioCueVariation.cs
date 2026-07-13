using System;

namespace Cyberland.Engine.Audio;

/// <summary>Picks clip variations and pitch/gain jitter for sound cues.</summary>
public static class AudioCueVariation
{
    /// <summary>Selection strategy for multi-clip cues.</summary>
    public enum PickMode : byte
    {
        /// <summary>Uniform random among clips.</summary>
        Random = 0,

        /// <summary>Cycle through clips in order.</summary>
        RoundRobin = 1,
    }

    /// <summary>Picks a clip index in [0, count).</summary>
    public static int PickIndex(int count, PickMode mode, ref int roundRobinCursor, Random rng)
    {
        if (count <= 0)
            return -1;
        if (count == 1)
            return 0;

        if (mode == PickMode.RoundRobin)
        {
            var i = roundRobinCursor % count;
            if (i < 0)
                i += count;
            roundRobinCursor = (i + 1) % count;
            return i;
        }

        ArgumentNullException.ThrowIfNull(rng);
        return rng.Next(count);
    }

    /// <summary>Samples a float in [min, max] inclusive.</summary>
    public static float SampleRange(float min, float max, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (max < min)
            (min, max) = (max, min);
        if (MathF.Abs(max - min) < 1e-6f)
            return min;
        return min + (float)rng.NextDouble() * (max - min);
    }
}
