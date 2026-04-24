using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Flipbook state for <see cref="Systems.SpriteAnimationSystem"/>: advances <see cref="Sprite.UvRect"/> from a uniform grid in the albedo atlas.
/// </summary>
/// <remarks>
/// Pair with <see cref="Sprite"/> (and typically <see cref="Transform"/>) on the same entity. This type does not declare
/// <see cref="RequiresComponentAttribute{TRequired}"/> for <see cref="Sprite"/> so the animation system can no-op when the sprite row is missing.
/// </remarks>
public struct SpriteAnimation : IComponent
{
    /// <summary>Total time accumulated for this clip.</summary>
    public float ElapsedSeconds;
    /// <summary>Duration of one frame in seconds (0 = frozen).</summary>
    public float SecondsPerFrame;
    /// <summary>Total frames in the clip.</summary>
    public int FrameCount;
    /// <summary>Number of atlas columns (rows inferred from <see cref="FrameCount"/>).</summary>
    public int AtlasColumns;
    /// <summary>If true, wraps back to frame 0; if false, clamps on the last frame.</summary>
    public bool Loop;
}
