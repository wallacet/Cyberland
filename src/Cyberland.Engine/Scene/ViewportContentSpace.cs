namespace Cyberland.Engine.Scene;

/// <summary>
/// Which space <see cref="ViewportAnchor2D"/> writes to <see cref="Position"/> (and optional <see cref="Sprite"/> half extents).
/// </summary>
public enum ViewportContentSpace
{
    /// <summary>Gameplay world pixels: origin bottom-left, +Y up (same as sprites and <see cref="TextCoordinateSpace.WorldBaseline"/>).</summary>
    WorldPixels,

    /// <summary>Framebuffer pixels: top-left origin, +Y down (same as <see cref="TextCoordinateSpace.ScreenPixels"/> text).</summary>
    ScreenPixels
}
