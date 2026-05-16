using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
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

    /// <summary>
    /// Virtual canvas for <see cref="CoordinateSpace.PresentationViewportSpace"/> HUD: uses
    /// <see cref="Camera2D.PresentationViewportSizeWorld"/> when set, otherwise matches the active world viewport extent.
    /// Returns <c>(0,0)</c> when <see cref="CameraRuntimeState.Valid"/> is false or extents are non-positive.
    /// </summary>
    public static Vector2D<int> VirtualSizeForHudLayout(GameHostServices host)
    {
        var crs = host.CameraRuntimeState;
        if (!crs.Valid || crs.ViewportSizeWorld.X <= 0 || crs.ViewportSizeWorld.Y <= 0)
            return default;
        return CameraPresentationLayout.ResolvePresentationViewportSize(crs);
    }

    /// <summary>
    /// Virtual extent for a retained UI root: presentation canvas when <paramref name="rootSpace"/> is
    /// <see cref="CoordinateSpace.PresentationViewportSpace"/>, otherwise the same contract as the legacy viewport branch
    /// in the engine UI document frame system.
    /// </summary>
    public static Vector2D<int> VirtualCanvasForUiDocument(GameHostServices host, IRenderer renderer, CoordinateSpace rootSpace)
    {
        if (rootSpace == CoordinateSpace.PresentationViewportSpace)
            return VirtualSizeForHudLayout(host);
        var vpRuntime = host.CameraRuntimeState.ViewportSizeWorld;
        return vpRuntime.X > 0 && vpRuntime.Y > 0 ? vpRuntime : renderer.ActiveCameraViewportSize;
    }
}
