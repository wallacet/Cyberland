using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Hosting;

/// <summary>
/// Helpers for reading the virtual camera canvas size in world pixels so mods do not mix conventions across phases.
/// </summary>
/// <remarks>
/// <see cref="IRenderer.ActiveCameraViewportSize"/> and <see cref="CameraRuntimeState.ViewportSizeWorld"/> match
/// for a single active 2D camera, but the runtime snapshot and renderer publish at different points in the frame. Use
/// <see cref="VirtualSizeForSimulation"/> for <see cref="Core.Ecs.IFixedUpdate"/> / <see cref="Core.Ecs.IEarlyUpdate"/>
/// (after the first <c>SystemScheduler.RunFrame</c> call, the snapshot matches the last resolved camera), and
/// <see cref="VirtualSizeForPresentation"/> for <see cref="Core.Ecs.ILateUpdate"/> when laying out <see cref="IRenderer"/>-backed
/// viewports. See <c>cyberland-world-screen-space</c> for space conventions.
/// </remarks>
public static class ModLayoutViewport
{
    /// <summary>Virtual camera extent in world pixels; prefer in simulation phases.</summary>
    public static Vector2D<int> VirtualSizeForSimulation(GameHostServices host) => host.CameraRuntimeState.ViewportSizeWorld;

    /// <summary>Active camera’s virtual extent (top-left, +Y down) from the renderer; prefer for late presentation.</summary>
    public static Vector2D<int> VirtualSizeForPresentation(IRenderer renderer) => renderer.ActiveCameraViewportSize;
}
