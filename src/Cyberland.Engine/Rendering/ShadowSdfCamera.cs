using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Immutable snapshot of every quantity the SDF shadow pipeline needs to convert between world, swapchain, and SDF spaces
/// — assembled per frame from <see cref="FramePlan"/> and reused by every shadow-related stage (occluder mask raster,
/// JFA SDF build, lighting cone-trace).
/// </summary>
/// <remarks>
/// <para>Immutable struct; safe to share across threads.</para>
/// <para>
/// <b>Coordinate-space contract (READ FIRST).</b> Every field's name carries its space suffix:
/// <list type="bullet">
/// <item><b><c>cameraPosWorld</c></b> — world (+Y up), pixels.</item>
/// <item><b><c>cameraRotRadians</c></b> — world-frame rotation, radians.</item>
/// <item><b><c>viewportSizeWorld</c></b> — virtual viewport extent in world pixels.</item>
/// <item><b><c>physicalOffsetSwapchainPx</c>, <c>physicalSizeSwapchainPx</c>, <c>physicalScale</c></b> — letterbox mapping
/// from virtual viewport to swapchain (+Y down).</item>
/// <item><b><c>swapchainSizePx</c></b> — physical window size (+Y down).</item>
/// <item><b><c>SdfScale</c></b> — SDF resolution multiplier; SDF texel size = 1/<c>SdfScale</c> swapchain pixels.</item>
/// </list>
/// </para>
/// <para>
/// <b>One-conversion-site rule.</b> <see cref="WorldToSwapchainPx"/> and <see cref="SwapchainPxToWorld"/> are the
/// <i>only</i> entry points that change spaces. Tests assert that they round-trip and agree with
/// <see cref="CameraProjection.WorldToViewportPixel"/> + <see cref="CameraProjection.ViewportPixelToSwapchainPixel"/>.
/// </para>
/// </remarks>
internal readonly struct ShadowSdfCamera
{
    /// <summary>Camera world position (+Y up), pixels.</summary>
    public readonly Vector2D<float> CameraPosWorld;
    /// <summary>Camera world-frame rotation, radians.</summary>
    public readonly float CameraRotRadians;
    /// <summary>Virtual viewport extent in world pixels (width, height).</summary>
    public readonly Vector2D<float> ViewportSizeWorld;
    /// <summary>Letterbox top-left offset in swapchain pixels (+Y down).</summary>
    public readonly Vector2D<float> PhysicalOffsetSwapchainPx;
    /// <summary>Letterbox width/height in swapchain pixels (+Y down).</summary>
    public readonly Vector2D<float> PhysicalSizeSwapchainPx;
    /// <summary>Uniform letterbox scale (swapchain px per virtual viewport px).</summary>
    public readonly float PhysicalScale;
    /// <summary>Physical window extent (+Y down), pixels.</summary>
    public readonly Vector2D<float> SwapchainSizePx;
    /// <summary>SDF resolution scale (1.0 = SDF tracks swapchain texels 1:1; 0.5 = half-res). Clamped to [0.0625, 1.0].</summary>
    public readonly float SdfScale;

    /// <summary>SDF image dimensions in <b>SDF texels</b> (+Y down).</summary>
    public Vector2D<int> SdfSizePx =>
        new(
            System.Math.Max(1, (int)MathF.Round(SwapchainSizePx.X * SdfScale)),
            System.Math.Max(1, (int)MathF.Round(SwapchainSizePx.Y * SdfScale)));

    /// <summary>Constructs a <see cref="ShadowSdfCamera"/> from explicit, fully-labeled inputs.</summary>
    public ShadowSdfCamera(
        Vector2D<float> cameraPosWorld,
        float cameraRotRadians,
        Vector2D<float> viewportSizeWorld,
        Vector2D<float> physicalOffsetSwapchainPx,
        Vector2D<float> physicalSizeSwapchainPx,
        float physicalScale,
        Vector2D<float> swapchainSizePx,
        float sdfScale)
    {
        CameraPosWorld = cameraPosWorld;
        CameraRotRadians = cameraRotRadians;
        ViewportSizeWorld = viewportSizeWorld;
        PhysicalOffsetSwapchainPx = physicalOffsetSwapchainPx;
        PhysicalSizeSwapchainPx = physicalSizeSwapchainPx;
        PhysicalScale = MathF.Max(physicalScale, 1e-4f);
        SwapchainSizePx = swapchainSizePx;
        SdfScale = sdfScale <= 0f ? 1f : MathF.Min(MathF.Max(sdfScale, 0.0625f), 1f);
    }

    /// <summary>
    /// Builds a synthetic test camera. All inputs labeled by space; mirrors what <see cref="FramePlan"/> exposes at runtime.
    /// </summary>
    public static ShadowSdfCamera SyntheticCamera(
        Vector2D<float> cameraPosWorld,
        float cameraRotRadians,
        Vector2D<int> viewportSizeWorld,
        Vector2D<int> swapchainSizePx,
        float sdfScale)
    {
        var physical = CameraProjection.ComputePhysicalViewport(viewportSizeWorld, swapchainSizePx);
        return new ShadowSdfCamera(
            cameraPosWorld,
            cameraRotRadians,
            new Vector2D<float>(viewportSizeWorld.X, viewportSizeWorld.Y),
            new Vector2D<float>(physical.OffsetPixels.X, physical.OffsetPixels.Y),
            new Vector2D<float>(physical.SizePixels.X, physical.SizePixels.Y),
            physical.Scale,
            new Vector2D<float>(swapchainSizePx.X, swapchainSizePx.Y),
            sdfScale);
    }

    /// <summary>
    /// Single canonical <b>world (+Y up) → swapchain (+Y down)</b> projection. Equivalent to
    /// <see cref="CameraProjection.WorldToViewportPixel"/> followed by <see cref="CameraProjection.ViewportPixelToSwapchainPixel"/>.
    /// Tests assert byte-for-byte agreement (≤ 1e-4 px) for the same inputs.
    /// </summary>
    public Vector2D<float> WorldToSwapchainPx(Vector2D<float> pointWorld)
    {
        var dx = pointWorld.X - CameraPosWorld.X;
        var dy = pointWorld.Y - CameraPosWorld.Y;
        var c = MathF.Cos(-CameraRotRadians);
        var s = MathF.Sin(-CameraRotRadians);
        var rx = dx * c - dy * s;
        var ry = dx * s + dy * c;
        var vpX = rx + ViewportSizeWorld.X * 0.5f;
        var vpY = ViewportSizeWorld.Y * 0.5f - ry;
        return new Vector2D<float>(
            PhysicalOffsetSwapchainPx.X + vpX * PhysicalScale,
            PhysicalOffsetSwapchainPx.Y + vpY * PhysicalScale);
    }

    /// <summary>Inverse of <see cref="WorldToSwapchainPx"/>; +Y down → +Y up. Single conversion site.</summary>
    /// <remarks>
    /// Floors <see cref="PhysicalScale"/> at 1e-4 (matching the GLSL <c>swapchainPixelToWorld</c> guard) to avoid
    /// division-by-zero when a default-constructed camera with <c>PhysicalScale == 0</c> is used.
    /// </remarks>
    public Vector2D<float> SwapchainPxToWorld(Vector2D<float> pointSwapchainPx)
    {
        var invScale = 1f / MathF.Max(PhysicalScale, 1e-4f);
        var vpX = (pointSwapchainPx.X - PhysicalOffsetSwapchainPx.X) * invScale;
        var vpY = (pointSwapchainPx.Y - PhysicalOffsetSwapchainPx.Y) * invScale;
        var rx = vpX - ViewportSizeWorld.X * 0.5f;
        var ry = ViewportSizeWorld.Y * 0.5f - vpY;
        var c = MathF.Cos(CameraRotRadians);
        var s = MathF.Sin(CameraRotRadians);
        var dx = rx * c - ry * s;
        var dy = rx * s + ry * c;
        return new Vector2D<float>(dx + CameraPosWorld.X, dy + CameraPosWorld.Y);
    }

    /// <summary>Maps swapchain pixel coordinates to SDF texel coordinates. Both spaces are +Y down — no flip.</summary>
    public Vector2D<float> SwapchainPxToSdfPx(Vector2D<float> pointSwapchainPx) =>
        pointSwapchainPx * SdfScale;

    /// <summary>Convert an SDF-texel distance back to swapchain-pixel distance. Used at the cone-trace SDF tap.</summary>
    public float SdfPxDistanceToSwapchainPx(float distSdfPx) =>
        SdfScale <= 0f ? distSdfPx : distSdfPx / SdfScale;
}
