namespace Cyberland.Demo;

/// <summary>Shared gameplay numbers for this mod. Keep these few and obvious so the sample stays easy to read.</summary>
public static class Constants
{
    /// <summary>Sprite half-size in world units; also used to keep the player away from the playfield AABB edges.</summary>
    public const float SpriteHalfExtent = 48f;

    /// <summary>World units per second when movement input is held (after <see cref="InputSystem"/> normalizes axes).</summary>
    public const float MoveSpeed = 320f;
}

/// <summary>
/// Bloom post-volume tuning shared by <c>Scenes/hdr.json</c> (initial state) and <see cref="PostVolumeFillSystem"/> (per-frame).
/// Player horizontal position maps to a bloom mix so the rig feels reactive without extra entities.
/// </summary>
public static class BloomTuning
{
    /// <summary>Bloom override when the player is at the left side of the screen (<c>tNorm ≈ 0</c>).</summary>
    public const float GainAtPlayerLeft = 2.35f;

    /// <summary>How much bloom drops from left to right across the playfield; gain at right is <see cref="GainAtPlayerLeft"/> minus this.</summary>
    public const float GainSpanAcrossPlayfield = 1.85f;
}
