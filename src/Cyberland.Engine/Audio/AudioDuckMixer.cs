using System;
using System.Collections.Generic;

namespace Cyberland.Engine.Audio;

/// <summary>
/// Tracks duck rules and per-target blend state. Pure logic; call <see cref="Tick"/> each mixer frame.
/// </summary>
public sealed class AudioDuckMixer
{
    private readonly List<AudioDuckRule> _rules = new();
    private readonly Dictionary<string, float> _current = new(StringComparer.Ordinal);

    /// <summary>Adds a duck rule (multipliers stack by multiply across rules for the same target).</summary>
    public void Register(in AudioDuckRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.TriggerBusId) || string.IsNullOrWhiteSpace(rule.TargetBusId))
            return;
        _rules.Add(rule);
        // Blend state is seeded on first Tick for this target (see cur = 1f path).
    }

    /// <summary>Removes all duck rules and resets blend state.</summary>
    public void Clear()
    {
        _rules.Clear();
        _current.Clear();
    }

    /// <summary>
    /// Advances duck envelopes. <paramref name="isTriggerBusActive"/> reports whether any voice is active on a bus.
    /// </summary>
    public void Tick(float deltaSeconds, Func<string, bool> isTriggerBusActive)
    {
        ArgumentNullException.ThrowIfNull(isTriggerBusActive);
        if (_rules.Count == 0)
            return;

        // Target → desired product of all rules for that target.
        var desired = new Dictionary<string, float>(StringComparer.Ordinal);
        var attack = new Dictionary<string, float>(StringComparer.Ordinal);
        var release = new Dictionary<string, float>(StringComparer.Ordinal);

        foreach (var rule in _rules)
        {
            var active = isTriggerBusActive(rule.TriggerBusId);
            var contrib = active ? Math.Clamp(rule.DuckGain, 0f, 1f) : 1f;
            if (!desired.TryGetValue(rule.TargetBusId, out var prod))
                prod = 1f;
            desired[rule.TargetBusId] = prod * contrib;

            // Use the fastest attack / slowest release among rules targeting this bus.
            if (!attack.TryGetValue(rule.TargetBusId, out var a) || rule.AttackSeconds < a)
                attack[rule.TargetBusId] = Math.Max(0f, rule.AttackSeconds);
            if (!release.TryGetValue(rule.TargetBusId, out var r) || rule.ReleaseSeconds > r)
                release[rule.TargetBusId] = Math.Max(0f, rule.ReleaseSeconds);
        }

        foreach (var (target, targetGain) in desired)
        {
            if (!_current.TryGetValue(target, out var cur))
                cur = 1f;

            var goingDown = targetGain < cur - 1e-4f;
            var seconds = goingDown
                ? attack.GetValueOrDefault(target, 0f)
                : release.GetValueOrDefault(target, 0f);
            _current[target] = AudioFade.BlendToward(cur, targetGain, seconds, deltaSeconds);
        }
    }

    /// <summary>Current duck multiplier for a bus (1 if no rules).</summary>
    public float GetMultiplier(string busId)
    {
        if (string.IsNullOrWhiteSpace(busId))
            return 1f;
        return _current.TryGetValue(busId.Trim(), out var m) ? m : 1f;
    }
}
