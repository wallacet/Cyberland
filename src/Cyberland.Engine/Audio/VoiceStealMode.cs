namespace Cyberland.Engine.Audio;

/// <summary>Policy when a bus or global voice cap is exceeded.</summary>
public enum VoiceStealMode : byte
{
    /// <summary>Drop the new play request.</summary>
    Fail = 0,

    /// <summary>Stop the lowest-priority active voice (ties: oldest).</summary>
    StealLowestPriority = 1,

    /// <summary>Stop the oldest active voice regardless of priority.</summary>
    StealOldest = 2,
}
