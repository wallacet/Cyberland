using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Oriented sprite occluder in <b>world</b> space (+Y up, pixels), used by the SDF shadow pipeline both on GPU
/// (vertex layout for the occluder-mask rasterizer) and on CPU (test reference for <see cref="ShadowDistanceFieldCpu"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate space (read first):</b> All fields are in <b>world</b> space (+Y up). The renderer projects through
/// <see cref="CameraProjection.WorldToViewportPixel"/> + <see cref="CameraProjection.ViewportPixelToSwapchainPixel"/>
/// when rasterizing the occluder mask — there is exactly one world→swapchain conversion site and it lives in the
/// shared camera helpers. Do not pre-flip Y here and do not pass swapchain pixels into <c>CenterWorld</c>.
/// </para>
/// <para>
/// See <c>.cursor/rules/cyberland-world-screen-space.mdc</c> for the canonical space contract and
/// <c>cyberland-engine-shaders</c> for the GLSL pipeline that consumes these.
/// </para>
/// </remarks>
internal readonly struct ShadowOccluder2D
{
    /// <summary>OBB center in <b>world</b> space (+Y up).</summary>
    public readonly Vector2D<float> CenterWorld;

    /// <summary>OBB half-extents along the box's local axes, in <b>world</b> pixels (always non-negative).</summary>
    public readonly Vector2D<float> HalfExtentsWorld;

    /// <summary>CCW rotation about <see cref="CenterWorld"/>, in radians (world frame).</summary>
    public readonly float RotationRadians;

    /// <summary>Constructs an occluder in <b>world</b> space; all inputs are world (+Y up).</summary>
    public ShadowOccluder2D(Vector2D<float> centerWorld, Vector2D<float> halfExtentsWorld, float rotationRadians)
    {
        CenterWorld = centerWorld;
        HalfExtentsWorld = halfExtentsWorld;
        RotationRadians = rotationRadians;
    }

    /// <summary>
    /// True when <paramref name="pointWorld"/> lies inside the rotated OBB. World-space test on world inputs only.
    /// </summary>
    public bool ContainsPointWorld(Vector2D<float> pointWorld)
    {
        var dx = pointWorld.X - CenterWorld.X;
        var dy = pointWorld.Y - CenterWorld.Y;
        var c = MathF.Cos(-RotationRadians);
        var s = MathF.Sin(-RotationRadians);
        var lx = dx * c - dy * s;
        var ly = dx * s + dy * c;
        return MathF.Abs(lx) <= HalfExtentsWorld.X && MathF.Abs(ly) <= HalfExtentsWorld.Y;
    }
}
