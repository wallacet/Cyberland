using System;

namespace Cyberland.Engine.Audio;

/// <summary>Pure helpers for polyphony steal decisions.</summary>
public static class VoiceLimitLogic
{
    /// <summary>
    /// Candidate voice for steal ranking. <see cref="SlotIndex"/> is an opaque index into the caller's table.
    /// </summary>
    public readonly struct VoiceCandidate
    {
        /// <summary>Creates a candidate.</summary>
        public VoiceCandidate(int slotIndex, int priority, double startedAtSeconds, string? busId = null)
        {
            SlotIndex = slotIndex;
            Priority = priority;
            StartedAtSeconds = startedAtSeconds;
            _ = busId;
        }

        /// <summary>Caller slot index.</summary>
        public int SlotIndex { get; }

        /// <summary>Higher = more important.</summary>
        public int Priority { get; }

        /// <summary>Monotonic start time for oldest-wins ties.</summary>
        public double StartedAtSeconds { get; }
    }

    /// <summary>
    /// Picks a slot to steal, or -1 if the new play should fail / no steal needed.
    /// When <paramref name="activeCount"/> is below <paramref name="maxVoices"/> (or max is unlimited), returns -1 meaning "no steal, allow".
    /// </summary>
    /// <param name="activeOnBus">Active voices on the constrained bus (or all voices for global).</param>
    /// <param name="activeCount">Number of valid entries in <paramref name="activeOnBus"/>.</param>
    /// <param name="maxVoices">Cap; ≤0 means unlimited.</param>
    /// <param name="mode">Steal policy.</param>
    /// <param name="incomingPriority">Priority of the new request.</param>
    /// <returns>Slot index to steal, or -1 if allow without steal, or -2 if fail (drop new).</returns>
    public static int ChooseSteal(
        ReadOnlySpan<VoiceCandidate> activeOnBus,
        int activeCount,
        int maxVoices,
        VoiceStealMode mode,
        int incomingPriority)
    {
        if (maxVoices <= 0 || activeCount < maxVoices)
            return -1;

        if (mode == VoiceStealMode.Fail)
            return -2;

        // activeCount >= maxVoices > 0 here, so the span has at least one candidate.
        var bestIdx = 0;
        var best = activeOnBus[0];

        for (var i = 1; i < activeCount; i++)
        {
            ref readonly var c = ref activeOnBus[i];
            if (mode == VoiceStealMode.StealOldest)
            {
                if (c.StartedAtSeconds < best.StartedAtSeconds)
                {
                    best = c;
                    bestIdx = i;
                }
            }
            else // StealLowestPriority
            {
                if (c.Priority < best.Priority
                    || (c.Priority == best.Priority && c.StartedAtSeconds < best.StartedAtSeconds))
                {
                    best = c;
                    bestIdx = i;
                }
            }
        }

        // StealLowestPriority: do not steal a higher-priority voice for a lower incoming.
        if (mode == VoiceStealMode.StealLowestPriority && incomingPriority < best.Priority)
            return -2;

        return best.SlotIndex;
    }

    /// <summary>True when a cue retrigger is allowed given last play time and cooldown.</summary>
    public static bool CooldownAllows(double nowSeconds, double lastPlaySeconds, float cooldownSeconds)
    {
        if (cooldownSeconds <= 0f)
            return true;
        return nowSeconds - lastPlaySeconds >= cooldownSeconds;
    }
}
