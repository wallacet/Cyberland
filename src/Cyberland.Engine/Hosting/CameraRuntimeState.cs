using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Hosting;

/// <summary>
/// Frame-stable camera snapshot for simulation/layout systems.
/// Published by ECS camera runtime systems, not by renderer queue state.
/// </summary>
[ExcludeFromCodeCoverage]
public readonly record struct CameraRuntimeState(
    Vector2D<int> ViewportSizeWorld,
    Vector2D<float> PositionWorld,
    float RotationRadians,
    Vector4D<float> BackgroundColor,
    int Priority,
    bool Valid)
{
    /// <summary>
    /// Creates a runtime snapshot from a resolved camera view request.
    /// </summary>
    public static CameraRuntimeState FromView(in CameraViewRequest view) =>
        new(
            view.ViewportSizeWorld,
            view.PositionWorld,
            view.RotationRadians,
            view.BackgroundColor,
            view.Priority,
            true);

    /// <summary>
    /// Creates the default runtime camera snapshot based on swapchain size.
    /// </summary>
    public static CameraRuntimeState CreateDefault(Vector2D<int> swapchainSize) =>
        FromView(CameraSelection.Default(swapchainSize));
}
