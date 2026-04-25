namespace Cyberland.Engine.Scene;

/// <summary>
/// What coordinate space a position or vector is in.
/// </summary>
/// <remarks>
/// This is used to determine how a position or vector is interpreted for drawing. For example, a position in world space is interpreted as a position in world pixels (relative to the world origin), while a position in local space is interpreted as a position in local pixels (relative to the parent transform's position). A position in viewport space is interpreted as a position in virtual viewport pixels (relative to the viewport origin).
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
    /// Virtual viewport space: top-left origin, +Y down, extent of the active camera viewport.
    /// Preferred for HUD and pointer interactions that should stay stable across letterboxing.
    /// </summary>
    ViewportSpace,

    /// <summary>
    /// Physical swapchain / window pixels: top-left origin, +Y down.
    /// This includes letterbox / pillarbox bars around the active virtual viewport.
    /// </summary>
    SwapchainSpace,

    // Intentionally no compatibility aliases; migrate call sites to canonical names.
}
