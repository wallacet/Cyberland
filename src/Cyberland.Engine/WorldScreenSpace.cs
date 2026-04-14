using Silk.NET.Maths;

namespace Cyberland.Engine;

/// <summary>
/// Helpers for converting between <b>world space</b> (gameplay: origin bottom-left, +X right, +Y up, typically pixels)
/// and <b>screen / framebuffer space</b> (top-left origin, +Y down).
/// </summary>
/// <remarks>
/// <see cref="Rendering.SpriteDrawRequest.CenterWorld"/> and lighting positions passed to <see cref="Rendering.IRenderer"/> are expressed in
/// <b>world space</b>; <see cref="Rendering.VulkanRenderer"/> converts to framebuffer pixels internally. Use these helpers when you have UI or
/// input in screen pixels and need world-space values (or the reverse) for layout and debugging.
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
