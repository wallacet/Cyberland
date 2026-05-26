using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Computes the tight axis-aligned bounding box of a spot light cone in a given coordinate space.
/// <see cref="ComputeConeAabb"/> operates in a single flat space (caller guarantees inputs are consistent).
/// <see cref="ComputeProjectedConeAabb"/> projects the cone's key world-space vertices through a
/// <see cref="ShadowSdfCamera"/> so the resulting AABB is correct in swapchain space regardless of
/// camera rotation — avoiding the space mismatch that occurs when mixing swapchain-space position
/// with world-space direction.
/// </summary>
/// <remarks>
/// The swept AABB encloses: (1) the cone apex, (2) both edge-ray endpoints at distance = radius,
/// and (3) the arc midpoint (center direction at radius) for wide cones (outerAngle &gt; π/4).
/// This is conservative but cheap — no transcendental calls beyond sin/cos of the outer angle.
/// </remarks>
internal static class SpotLightBounds
{
    /// <summary>
    /// Returns the tight AABB (min, max) for a spot cone in whatever coordinate space the inputs are expressed in.
    /// </summary>
    /// <param name="posWorld">Cone apex position.</param>
    /// <param name="dirWorld">Unit direction the cone opens toward.</param>
    /// <param name="radius">Radial reach (world units or swapchain px, depending on caller).</param>
    /// <param name="outerAngle">Half-angle at the outer falloff boundary (radians).</param>
    /// <param name="aabbMin">Output: minimum corner of the axis-aligned bounding box.</param>
    /// <param name="aabbMax">Output: maximum corner of the axis-aligned bounding box.</param>
    public static void ComputeConeAabb(
        Vector2D<float> posWorld,
        Vector2D<float> dirWorld,
        float radius,
        float outerAngle,
        out Vector2D<float> aabbMin,
        out Vector2D<float> aabbMax)
    {
        var cosA = MathF.Cos(outerAngle);
        var sinA = MathF.Sin(outerAngle);

        // Edge ray rotated +outerAngle from dirWorld
        var edgePlusX = dirWorld.X * cosA - dirWorld.Y * sinA;
        var edgePlusY = dirWorld.X * sinA + dirWorld.Y * cosA;

        // Edge ray rotated -outerAngle from dirWorld
        var edgeMinusX = dirWorld.X * cosA + dirWorld.Y * sinA;
        var edgeMinusY = -dirWorld.X * sinA + dirWorld.Y * cosA;

        var ep0 = new Vector2D<float>(posWorld.X + edgePlusX * radius, posWorld.Y + edgePlusY * radius);
        var ep1 = new Vector2D<float>(posWorld.X + edgeMinusX * radius, posWorld.Y + edgeMinusY * radius);

        aabbMin = new Vector2D<float>(
            MathF.Min(posWorld.X, MathF.Min(ep0.X, ep1.X)),
            MathF.Min(posWorld.Y, MathF.Min(ep0.Y, ep1.Y)));
        aabbMax = new Vector2D<float>(
            MathF.Max(posWorld.X, MathF.Max(ep0.X, ep1.X)),
            MathF.Max(posWorld.Y, MathF.Max(ep0.Y, ep1.Y)));

        // For wide cones the arc between edge rays can bulge beyond the two endpoints.
        // Include the midpoint (center direction at radius) to keep the AABB conservative.
        if (outerAngle > MathF.PI / 4f)
        {
            var midX = posWorld.X + dirWorld.X * radius;
            var midY = posWorld.Y + dirWorld.Y * radius;
            aabbMin = new Vector2D<float>(MathF.Min(aabbMin.X, midX), MathF.Min(aabbMin.Y, midY));
            aabbMax = new Vector2D<float>(MathF.Max(aabbMax.X, midX), MathF.Max(aabbMax.Y, midY));
        }
    }

    /// <summary>
    /// Projects the cone's key world-space vertices through <paramref name="cam"/> and returns their
    /// swapchain-pixel AABB. This avoids the space mismatch of mixing a swapchain-space apex with a
    /// world-space direction by performing all geometry in world space first, then projecting.
    /// </summary>
    /// <remarks>
    /// Key vertices: apex, two edge-ray endpoints (±outerAngle), and the arc midpoint for wide cones.
    /// Each is projected via <see cref="ShadowSdfCamera.WorldToSwapchainPx"/> so camera rotation is
    /// handled correctly.
    /// </remarks>
    public static void ComputeProjectedConeAabb(
        in SpotLight light,
        in ShadowSdfCamera cam,
        out float minX, out float minY, out float maxX, out float maxY)
    {
        var apexSwapchainPx = cam.WorldToSwapchainPx(light.PositionWorld);
        minX = apexSwapchainPx.X;
        minY = apexSwapchainPx.Y;
        maxX = apexSwapchainPx.X;
        maxY = apexSwapchainPx.Y;

        var cosA = MathF.Cos(light.OuterConeRadians);
        var sinA = MathF.Sin(light.OuterConeRadians);
        var d = light.DirectionWorld;
        var r = MathF.Max(light.Radius, 0f);

        // Edge ray rotated +outerAngle from direction
        var ep0World = new Vector2D<float>(
            light.PositionWorld.X + (d.X * cosA - d.Y * sinA) * r,
            light.PositionWorld.Y + (d.X * sinA + d.Y * cosA) * r);
        var ep0Sw = cam.WorldToSwapchainPx(ep0World);
        Expand(ref minX, ref minY, ref maxX, ref maxY, ep0Sw);

        // Edge ray rotated -outerAngle from direction
        var ep1World = new Vector2D<float>(
            light.PositionWorld.X + (d.X * cosA + d.Y * sinA) * r,
            light.PositionWorld.Y + (-d.X * sinA + d.Y * cosA) * r);
        var ep1Sw = cam.WorldToSwapchainPx(ep1World);
        Expand(ref minX, ref minY, ref maxX, ref maxY, ep1Sw);

        // Wide cones: the arc between edge rays can bulge past the endpoints.
        if (light.OuterConeRadians > MathF.PI / 4f)
        {
            var midWorld = new Vector2D<float>(
                light.PositionWorld.X + d.X * r,
                light.PositionWorld.Y + d.Y * r);
            var midSw = cam.WorldToSwapchainPx(midWorld);
            Expand(ref minX, ref minY, ref maxX, ref maxY, midSw);
        }
    }

    private static void Expand(ref float minX, ref float minY, ref float maxX, ref float maxY,
                                Vector2D<float> pt)
    {
        if (pt.X < minX) minX = pt.X;
        if (pt.Y < minY) minY = pt.Y;
        if (pt.X > maxX) maxX = pt.X;
        if (pt.Y > maxY) maxY = pt.Y;
    }
}
