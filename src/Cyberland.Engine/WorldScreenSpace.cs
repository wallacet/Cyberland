using Silk.NET.Maths;

namespace Cyberland.Engine;

/// <summary>
/// <b>World space</b> (gameplay math): origin bottom-left, +X right, +Y up (pixels).
/// <b>Screen space</b> (framebuffer / Vulkan): top-left, +Y down — produced inside <see cref="Rendering.VulkanRenderer.SetSpriteWorld"/> for drawing.
/// </summary>
public static class WorldScreenSpace
{
    /// <summary>World center (+Y up) → sprite center in screen pixels (+Y down).</summary>
    public static Vector2D<float> WorldCenterToScreenPixel(Vector2D<float> worldCenter, Vector2D<int> framebufferSize) =>
        new(worldCenter.X, framebufferSize.Y - worldCenter.Y);

    /// <summary>World velocity (+Y = up) → screen-space velocity (+Y = down) for one frame step.</summary>
    public static Vector2D<float> WorldVelocityToScreenVelocity(Vector2D<float> worldVelocity) =>
        new(worldVelocity.X, -worldVelocity.Y);

    /// <summary>Screen-space sprite center (top-left, +Y down) from a world center; see <see cref="Rendering.VulkanRenderer.SetSpriteWorld"/>.</summary>
    public static Vector2D<float> ScreenPixelToWorldCenter(Vector2D<float> screenCenter, Vector2D<int> framebufferSize) =>
        new(screenCenter.X, framebufferSize.Y - screenCenter.Y);
}
