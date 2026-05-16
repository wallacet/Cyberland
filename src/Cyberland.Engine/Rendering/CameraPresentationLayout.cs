using Cyberland.Engine.Hosting;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Resolves the virtual canvas used for <see cref="Scene.CoordinateSpace.PresentationViewportSpace"/> HUD when the active
/// camera’s <see cref="Scene.Camera2D.ViewportSizeWorld"/> changes independently (e.g. RTS zoom).
/// </summary>
public static class CameraPresentationLayout
{
    /// <summary>
    /// When both axes of <paramref name="presentationConfigured"/> are positive, returns that size; otherwise returns
    /// <paramref name="viewExtent"/> (presentation follows the world viewport).
    /// </summary>
    public static Vector2D<int> ResolvePresentationViewportSize(Vector2D<int> viewExtent, Vector2D<int> presentationConfigured) =>
        presentationConfigured.X > 0 && presentationConfigured.Y > 0 ? presentationConfigured : viewExtent;

    /// <summary>Resolves presentation size from a submitted camera snapshot.</summary>
    public static Vector2D<int> ResolvePresentationViewportSize(CameraViewRequest view) =>
        ResolvePresentationViewportSize(view.ViewportSizeWorld, view.PresentationViewportSizeWorld);

    /// <summary>Resolves presentation size from the ECS-published runtime camera state.</summary>
    public static Vector2D<int> ResolvePresentationViewportSize(CameraRuntimeState state) =>
        ResolvePresentationViewportSize(state.ViewportSizeWorld, state.PresentationViewportSizeWorld);
}
