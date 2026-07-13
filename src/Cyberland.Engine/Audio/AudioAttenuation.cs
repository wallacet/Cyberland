using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Audio;

/// <summary>
/// 2D distance attenuation and stereo pan helpers for world-space voices.
/// </summary>
/// <remarks>
/// World space is +Y up. Pan uses listener-relative X after optional facing rotation.
/// </remarks>
public static class AudioAttenuation
{
    /// <summary>
    /// Inverse-distance style attenuation clamped to [0,1].
    /// At or below <paramref name="refDistance"/> → 1; at or beyond <paramref name="maxDistance"/> → 0.
    /// </summary>
    public static float DistanceGain(
        float distance,
        float refDistance,
        float maxDistance,
        float rolloff)
    {
        if (maxDistance <= 0f || refDistance < 0f)
            return 0f;
        if (distance <= refDistance)
            return 1f;
        if (distance >= maxDistance)
            return 0f;

        var span = maxDistance - refDistance;
        if (span <= 1e-6f)
            return 0f;

        var t = (distance - refDistance) / span;
        var gain = 1f - t;
        if (rolloff > 0f && MathF.Abs(rolloff - 1f) > 1e-4f)
            gain = MathF.Pow(MathF.Max(gain, 0f), rolloff);
        return Math.Clamp(gain, 0f, 1f);
    }

    /// <summary>
    /// Stereo pan in [-1,1] from listener-relative world offset (right = +X in listener local).
    /// </summary>
    public static float StereoPan(
        Vector2D<float> sourceWorld,
        Vector2D<float> listenerWorld,
        float listenerRotationRadians,
        float maxPanDistance)
    {
        var dx = sourceWorld.X - listenerWorld.X;
        var dy = sourceWorld.Y - listenerWorld.Y;
        var c = MathF.Cos(-listenerRotationRadians);
        var s = MathF.Sin(-listenerRotationRadians);
        var localX = dx * c - dy * s;

        if (maxPanDistance <= 1e-6f)
            return 0f;
        return Math.Clamp(localX / maxPanDistance, -1f, 1f);
    }

    /// <summary>Euclidean distance between two world points.</summary>
    public static float Distance(Vector2D<float> a, Vector2D<float> b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Combined world-space gain (distance only). Returns 0 when culled past max distance.
    /// </summary>
    public static float WorldAudibilityGain(
        Vector2D<float> sourceWorld,
        Vector2D<float> listenerWorld,
        float refDistance,
        float maxDistance,
        float rolloff)
    {
        var d = Distance(sourceWorld, listenerWorld);
        return DistanceGain(d, refDistance, maxDistance, rolloff);
    }
}
