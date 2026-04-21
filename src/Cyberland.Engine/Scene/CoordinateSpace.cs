namespace Cyberland.Engine.Scene;

/// <summary>
/// What coordinate space a position or vector is in.
/// </summary>
/// <remarks>
/// This is used to determine how a position or vector is interpreted for drawing. For example, a position in world space is interpreted as a position in world pixels (relative to the world origin), while a position in local space is interpreted as a position in local pixels (relative to the parent transform's position). A position in screen space is interpreted as a position in screen pixels (relative to the screen origin).
/// </remarks>
public enum CoordinateSpace
{
    /// <summary>
    /// Diegetic / world-space: baseline-left in world pixels (+Y up), same convention as <see cref="Sprite"/> placement.
    /// </summary>
    WorldSpace,

    /// <summary>
    /// Local-space: baseline-left in local pixels (+Y up), relative to the parent transform's position.
    /// </summary>
    LocalSpace,

    /// <summary>
    /// HUD / screen-space: baseline in framebuffer pixels (top-left origin, +Y down), matching <see cref="Rendering.Text.TextRenderer"/> screen draws.
    /// </summary>
    ScreenSpace
}
