namespace Cyberland.Engine.Audio;

/// <summary>
/// Sidechain-style duck: while any voice is active on <see cref="TriggerBusId"/>,
/// blend <see cref="TargetBusId"/> gain toward <see cref="DuckGain"/>.
/// </summary>
public readonly struct AudioDuckRule
{
    /// <summary>Creates a duck rule.</summary>
    public AudioDuckRule(
        string triggerBusId,
        string targetBusId,
        float duckGain,
        float attackSeconds,
        float releaseSeconds)
    {
        TriggerBusId = triggerBusId ?? string.Empty;
        TargetBusId = targetBusId ?? string.Empty;
        DuckGain = duckGain;
        AttackSeconds = attackSeconds;
        ReleaseSeconds = releaseSeconds;
    }

    /// <summary>Bus whose activity triggers ducking.</summary>
    public string TriggerBusId { get; }

    /// <summary>Bus whose effective gain is reduced.</summary>
    public string TargetBusId { get; }

    /// <summary>Linear gain multiplier while ducked (e.g. 0.35).</summary>
    public float DuckGain { get; }

    /// <summary>Seconds to blend toward <see cref="DuckGain"/>.</summary>
    public float AttackSeconds { get; }

    /// <summary>Seconds to blend back to 1 when the trigger bus is idle.</summary>
    public float ReleaseSeconds { get; }
}
