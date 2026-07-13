using System;

namespace Cyberland.Engine.Audio;

/// <summary>Linear gain ramp helpers for anti-click fades.</summary>
public static class AudioFade
{
    /// <summary>
    /// Advances a fade-in from 0→1. Returns the new envelope (0..1) and whether the fade completed.
    /// </summary>
    public static float AdvanceFadeIn(float current, float fadeInSeconds, float deltaSeconds, out bool completed)
    {
        if (fadeInSeconds <= 1e-6f)
        {
            completed = true;
            return 1f;
        }

        var next = current + deltaSeconds / fadeInSeconds;
        if (next >= 1f)
        {
            completed = true;
            return 1f;
        }

        completed = false;
        return next;
    }

    /// <summary>
    /// Advances a fade-out from current→0. Returns the new envelope and whether the fade completed (should stop).
    /// </summary>
    public static float AdvanceFadeOut(float current, float fadeOutSeconds, float deltaSeconds, out bool completed)
    {
        if (fadeOutSeconds <= 1e-6f)
        {
            completed = true;
            return 0f;
        }

        var next = current - deltaSeconds / fadeOutSeconds;
        if (next <= 0f)
        {
            completed = true;
            return 0f;
        }

        completed = false;
        return next;
    }

    /// <summary>
    /// Blends <paramref name="current"/> toward <paramref name="target"/> over <paramref name="seconds"/> using <paramref name="deltaSeconds"/>.
    /// </summary>
    public static float BlendToward(float current, float target, float seconds, float deltaSeconds)
    {
        if (seconds <= 1e-6f)
            return target;
        var t = Math.Clamp(deltaSeconds / seconds, 0f, 1f);
        return current + (target - current) * t;
    }
}
