namespace Cyberland.Engine.Assets;

/// <summary>
/// Border insets for 9-slice / nine-patch UI and sprite quads, in source-pixel units.
/// </summary>
/// <param name="Left">Pixels from the left edge that stay unstretched.</param>
/// <param name="Top">Pixels from the top edge that stay unstretched.</param>
/// <param name="Right">Pixels from the right edge that stay unstretched.</param>
/// <param name="Bottom">Pixels from the bottom edge that stay unstretched.</param>
public readonly record struct NineSliceInsets(int Left, int Top, int Right, int Bottom)
{
    /// <summary>True when all insets are zero (single-quad path).</summary>
    public bool IsEmpty => Left <= 0 && Top <= 0 && Right <= 0 && Bottom <= 0;

    /// <summary>Validates insets against source pixel dimensions.</summary>
    public bool FitsSource(int sourceWidth, int sourceHeight) =>
        Left >= 0 && Top >= 0 && Right >= 0 && Bottom >= 0 &&
        Left + Right <= sourceWidth && Top + Bottom <= sourceHeight;
}
