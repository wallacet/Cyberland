using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Pure 2D math used to project world / viewport / swapchain coordinates through the active camera and
/// letterbox mapping. All helpers are allocation-free and safe to call from parallel systems.
/// </summary>
/// <remarks>
/// <para>
/// <b>World space</b>: gameplay units, +X right, +Y up, origin at world zero.
/// </para>
/// <para>
/// <b>Virtual viewport space</b>: the camera's fixed-size canvas in pixels, top-left origin, +Y down, extent
/// <c>[0, ViewportSizeWorld]</c>. HUD / UI are authored in this space so they stay aligned to the camera
/// regardless of camera position or rotation.
/// </para>
/// <para>
/// <b>Swapchain space</b>: the physical window in pixels, top-left origin, +Y down. The virtual viewport is
/// scaled uniformly into the swapchain (aspect-preserving letterbox / pillarbox) so resizing the window never
/// shows more or less world; bars fill the unused area around the virtual viewport.
/// </para>
/// </remarks>
public static class CameraProjection
{
    /// <summary>
    /// Projects a world-space point (+Y up) through the camera into virtual viewport pixels (+Y down,
    /// top-left origin). Applies translation (by <paramref name="cameraPositionWorld"/>) and inverse rotation
    /// (by <paramref name="cameraRotationRadians"/>) so a point at the camera position maps to the viewport
    /// center, and content rotates opposite the camera in the view frame.
    /// </summary>
    public static Vector2D<float> WorldToViewportPixel(
        Vector2D<float> worldPoint,
        Vector2D<float> cameraPositionWorld,
        float cameraRotationRadians,
        Vector2D<float> viewportSizeWorld)
    {
        var dx = worldPoint.X - cameraPositionWorld.X;
        var dy = worldPoint.Y - cameraPositionWorld.Y;
        // Rotate by -cameraRotationRadians: (cos, sin; -sin, cos) ⋅ (dx, dy)
        var c = MathF.Cos(-cameraRotationRadians);
        var s = MathF.Sin(-cameraRotationRadians);
        var rx = dx * c - dy * s;
        var ry = dx * s + dy * c;
        // (rx, ry) is in +Y up centered at viewport center; flip Y for viewport pixel space (+Y down, top-left origin).
        return new Vector2D<float>(rx + viewportSizeWorld.X * 0.5f, viewportSizeWorld.Y * 0.5f - ry);
    }

    /// <summary>
    /// Inverse of <see cref="WorldToViewportPixel"/>: maps a virtual viewport pixel (+Y down, top-left origin)
    /// back to world coordinates (+Y up).
    /// </summary>
    public static Vector2D<float> ViewportPixelToWorld(
        Vector2D<float> viewportPoint,
        Vector2D<float> cameraPositionWorld,
        float cameraRotationRadians,
        Vector2D<float> viewportSizeWorld)
    {
        // Undo viewport-pixel framing: recover (+Y up) coordinates centered at the camera.
        var rx = viewportPoint.X - viewportSizeWorld.X * 0.5f;
        var ry = viewportSizeWorld.Y * 0.5f - viewportPoint.Y;
        // Rotate by +cameraRotationRadians.
        var c = MathF.Cos(cameraRotationRadians);
        var s = MathF.Sin(cameraRotationRadians);
        var dx = rx * c - ry * s;
        var dy = rx * s + ry * c;
        return new Vector2D<float>(dx + cameraPositionWorld.X, dy + cameraPositionWorld.Y);
    }

    /// <summary>
    /// Aspect-preserving letterbox mapping: returns the physical rectangle inside
    /// <paramref name="swapchainPixelSize"/> that holds the virtual viewport plus the uniform scale factor.
    /// Bars (letterbox / pillarbox) fill the remaining swapchain area.
    /// </summary>
    /// <param name="viewportSizeWorld">Virtual viewport size in world pixels (width, height).</param>
    /// <param name="swapchainPixelSize">Physical window size in pixels.</param>
    /// <returns>
    /// Offset (top-left, pixels), size (pixels), and uniform scale factor (<c>physicalSize / viewportSize</c>).
    /// Returns a zero-size rect if any input extent is non-positive.
    /// </returns>
    public static PhysicalViewport ComputePhysicalViewport(
        Vector2D<int> viewportSizeWorld,
        Vector2D<int> swapchainPixelSize)
    {
        if (viewportSizeWorld.X <= 0 || viewportSizeWorld.Y <= 0 ||
            swapchainPixelSize.X <= 0 || swapchainPixelSize.Y <= 0)
        {
            return new PhysicalViewport(new Vector2D<int>(0, 0), new Vector2D<int>(0, 0), 1f);
        }

        var scaleX = (float)swapchainPixelSize.X / viewportSizeWorld.X;
        var scaleY = (float)swapchainPixelSize.Y / viewportSizeWorld.Y;
        // Uniform min keeps aspect and fits entirely inside the window (bars on the longer axis).
        var scale = MathF.Min(scaleX, scaleY);
        var w = (int)MathF.Round(viewportSizeWorld.X * scale);
        var h = (int)MathF.Round(viewportSizeWorld.Y * scale);
        // Centered letterbox: half of the leftover on each side; integer floor keeps the rect inside the swapchain.
        var ox = (swapchainPixelSize.X - w) / 2;
        var oy = (swapchainPixelSize.Y - h) / 2;
        return new PhysicalViewport(new Vector2D<int>(ox, oy), new Vector2D<int>(w, h), scale);
    }

    /// <summary>
    /// Maps a virtual viewport pixel (+Y down, top-left origin) into swapchain pixels using
    /// <paramref name="physical"/>'s letterbox offset and scale.
    /// </summary>
    public static Vector2D<float> ViewportPixelToSwapchainPixel(
        Vector2D<float> viewportPixel,
        in PhysicalViewport physical) =>
        new(physical.OffsetPixels.X + viewportPixel.X * physical.Scale,
            physical.OffsetPixels.Y + viewportPixel.Y * physical.Scale);

    /// <summary>
    /// Maps a swapchain pixel back into the virtual viewport (inverse of
    /// <see cref="ViewportPixelToSwapchainPixel"/>). When <see cref="PhysicalViewport.Scale"/> is zero the
    /// swapchain point is returned unchanged.
    /// </summary>
    public static Vector2D<float> SwapchainPixelToViewportPixel(
        Vector2D<float> swapchainPixel,
        in PhysicalViewport physical)
    {
        if (physical.Scale <= 0f)
            return swapchainPixel;
        var inv = 1f / physical.Scale;
        return new Vector2D<float>(
            (swapchainPixel.X - physical.OffsetPixels.X) * inv,
            (swapchainPixel.Y - physical.OffsetPixels.Y) * inv);
    }
}

/// <summary>
/// Letterbox mapping from the active camera's virtual viewport to the physical swapchain.
/// </summary>
public readonly struct PhysicalViewport
{
    /// <summary>Top-left of the letterboxed rect in swapchain pixels (+Y down).</summary>
    public readonly Vector2D<int> OffsetPixels;
    /// <summary>Width and height of the letterboxed rect in swapchain pixels.</summary>
    public readonly Vector2D<int> SizePixels;
    /// <summary>Uniform scale factor: physical pixels per virtual viewport pixel.</summary>
    public readonly float Scale;

    /// <summary>Creates the mapping directly (tests and <see cref="CameraProjection.ComputePhysicalViewport"/>).</summary>
    public PhysicalViewport(Vector2D<int> offsetPixels, Vector2D<int> sizePixels, float scale)
    {
        OffsetPixels = offsetPixels;
        SizePixels = sizePixels;
        Scale = scale;
    }
}
