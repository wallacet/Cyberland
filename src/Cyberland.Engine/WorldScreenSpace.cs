using Silk.NET.Maths;

namespace Cyberland.Engine;

/// <summary>
/// Helpers for converting between <b>world space</b> (gameplay: origin bottom-left, +X right, +Y up, typically pixels)
/// and <b>screen / framebuffer space</b> (top-left origin, +Y down), which is what the renderer uses for submitted sprite centers.
/// </summary>
/// <remarks>
/// Use these when you compute positions in “math Y-up” but submit draws through <see cref="Rendering.IRenderer"/> expecting screen-style coordinates.
/// </remarks>
public static class WorldScreenSpace
{
    /// <summary>Converts a world-space center (Y up) to the sprite center in framebuffer pixels (Y down).</summary>
    /// <param name="worldCenter">Position in world units (pixels), Y increasing upward.</param>
    /// <param name="framebufferSize">Swapchain size in pixels (<see cref="Rendering.IRenderer.SwapchainPixelSize"/>).</param>
    public static Vector2D<float> WorldCenterToScreenPixel(Vector2D<float> worldCenter, Vector2D<int> framebufferSize) =>
        new(worldCenter.X, framebufferSize.Y - worldCenter.Y);

    /// <summary>Flips vertical velocity when mapping world motion to screen-space deltas.</summary>
    /// <param name="worldVelocity">Velocity in world units per second (Y positive = up).</param>
    public static Vector2D<float> WorldVelocityToScreenVelocity(Vector2D<float> worldVelocity) =>
        new(worldVelocity.X, -worldVelocity.Y);

    /// <summary>
    /// Inverse of <see cref="WorldCenterToScreenPixel"/>: maps a screen-space center (Y down) back to world center (Y up).
    /// </summary>
    /// <param name="screenCenter">Pixel coordinates with origin top-left.</param>
    /// <param name="framebufferSize">Current framebuffer size in pixels.</param>
    public static Vector2D<float> ScreenPixelToWorldCenter(Vector2D<float> screenCenter, Vector2D<int> framebufferSize) =>
        new(screenCenter.X, framebufferSize.Y - screenCenter.Y);
}
