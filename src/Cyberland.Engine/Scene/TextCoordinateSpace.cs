namespace Cyberland.Engine.Scene;

/// <summary>
/// How <see cref="BitmapText"/> interprets the paired <see cref="Position"/> for drawing.
/// </summary>
public enum TextCoordinateSpace
{
    /// <summary>
    /// Diegetic / world-space: baseline-left in world pixels (+Y up), same convention as <see cref="Sprite"/> placement.
    /// </summary>
    WorldBaseline,

    /// <summary>
    /// HUD / screen-space: baseline in framebuffer pixels (top-left origin, +Y down), matching <see cref="Rendering.Text.TextRenderer"/> screen draws.
    /// </summary>
    ScreenPixels
}
