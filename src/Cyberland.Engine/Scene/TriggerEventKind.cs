namespace Cyberland.Engine.Scene;

/// <summary>
/// Per-fixed-tick trigger transition event kinds produced by <see cref="Systems.TriggerSystem"/>.
/// </summary>
public enum TriggerEventKind
{
    /// <summary>
    /// The pair overlapped this tick but did not overlap in the previous fixed tick.
    /// </summary>
    OnTriggerEnter = 0,

    /// <summary>
    /// The pair overlapped in both the previous and current fixed ticks.
    /// </summary>
    OnTriggerStay = 1,

    /// <summary>
    /// The pair overlapped in the previous fixed tick but not in the current fixed tick.
    /// </summary>
    OnTriggerExit = 2,
}
