using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Pure helper used by the frame plan builder to pick the active camera. Ties on
/// <see cref="CameraViewRequest.Priority"/> are broken by submit order (first-wins) so selection is stable
/// across frames when priorities do not change.
/// </summary>
public static class CameraSelection
{
    /// <summary>Default camera used when no enabled cameras are submitted.</summary>
    /// <remarks>
    /// Centers the camera on the swapchain and matches the viewport size to the swapchain, which reproduces the
    /// pre-camera 1:1 world ↔ swapchain pixel mapping so bootstrapping code (empty tests, mods that forget to
    /// create a camera) still renders. Background color is the engine-default dark bluish tone.
    /// </remarks>
    public static CameraViewRequest Default(Vector2D<int> swapchainPixelSize)
    {
        var w = swapchainPixelSize.X > 0 ? swapchainPixelSize.X : 1;
        var h = swapchainPixelSize.Y > 0 ? swapchainPixelSize.Y : 1;
        return new CameraViewRequest
        {
            PositionWorld = new Vector2D<float>(w * 0.5f, h * 0.5f),
            RotationRadians = 0f,
            ViewportSizeWorld = new Vector2D<int>(w, h),
            Priority = int.MinValue,
            Enabled = true,
            BackgroundColor = new Vector4D<float>(0.02f, 0.02f, 0.06f, 1f)
        };
    }

    /// <summary>
    /// Returns the highest-priority enabled camera from <paramref name="cameras"/> (first-wins on ties) or the
    /// <see cref="Default"/> fallback if none is eligible. A camera is eligible when
    /// <see cref="CameraViewRequest.Enabled"/> is <c>true</c> and both viewport extents are positive.
    /// </summary>
    public static CameraViewRequest PickActive(
        ReadOnlySpan<CameraViewRequest> cameras,
        Vector2D<int> swapchainPixelSize)
    {
        var best = -1;
        for (var i = 0; i < cameras.Length; i++)
        {
            ref readonly var c = ref cameras[i];
            if (!c.Enabled || c.ViewportSizeWorld.X <= 0 || c.ViewportSizeWorld.Y <= 0)
                continue;
            if (best < 0 || c.Priority > cameras[best].Priority)
                best = i;
        }

        return best < 0 ? Default(swapchainPixelSize) : cameras[best];
    }
}
