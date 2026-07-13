using System;

namespace Cyberland.Engine.Audio;

/// <summary>Composes final linear voice gain from mix stages.</summary>
public static class AudioGainMath
{
    /// <summary>
    /// clip × voice × cueJitter × bus × master × duck × envBus × envMaster × focus.
    /// </summary>
    public static float Compose(
        float clipGain,
        float voiceGain,
        float cueJitter,
        float busGain,
        float masterGain,
        float duckMultiplier,
        float envBusMultiplier,
        float envMaster,
        float focusFactor)
    {
        var g = clipGain * voiceGain * cueJitter * busGain * masterGain
                * duckMultiplier * envBusMultiplier * envMaster * focusFactor;
        if (float.IsNaN(g) || float.IsInfinity(g) || g < 0f)
            return 0f;
        return Math.Clamp(g, 0f, 4f);
    }

    /// <summary>Focus factor from policy and whether the window is focused.</summary>
    public static float FocusFactor(in AudioFocusPolicy policy, bool windowFocused)
    {
        if (windowFocused)
            return 1f;
        return policy.Kind switch
        {
            AudioFocusPolicy.FocusKind.MuteMaster => 0f,
            AudioFocusPolicy.FocusKind.DuckMaster => Math.Clamp(policy.DuckGain, 0f, 1f),
            _ => 1f,
        };
    }
}
