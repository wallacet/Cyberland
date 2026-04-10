namespace Cyberland.Engine.Scene2D;

/// <summary>Advances atlas UVs on a <see cref="Sprite"/> (grid layout).</summary>
public struct SpriteAnimation
{
    public float ElapsedSeconds;
    public float SecondsPerFrame;
    public int FrameCount;
    /// <summary>Number of atlas columns (rows inferred from <see cref="FrameCount"/>).</summary>
    public int AtlasColumns;
    public bool Loop;
}
