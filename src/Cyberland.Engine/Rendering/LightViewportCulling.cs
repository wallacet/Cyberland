using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Rejects lights whose swapchain-pixel AABB does not intersect the visible viewport. Applied after emissive
/// promotion and <see cref="LightSubmissionOrdering"/> sort so the renderer skips SSBO upload and GPU draw
/// calls for lights that cannot contribute to the final image.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate flow.</b> Each light's world position is projected to swapchain pixels via
/// <see cref="ShadowSdfCamera.WorldToSwapchainPx"/>. The world-space radius is scaled by
/// <see cref="ShadowSdfCamera.PhysicalScale"/> to obtain a swapchain-pixel extent; the accessor-specific
/// AABB (circle for points, projected cone for spots) is tested against the physical viewport letterbox rect.
/// </para>
/// <para>Pure functions; safe to invoke from any thread.</para>
/// </remarks>
internal static class LightViewportCulling
{
    /// <summary>
    /// Compacts <paramref name="lights"/> in-place, removing entries whose swapchain-pixel AABB is entirely outside
    /// the physical viewport (letterbox rect). Returns the new count.
    /// </summary>
    /// <param name="lights">Sorted/clamped point light array (modified in-place).</param>
    /// <param name="count">Valid entries in <paramref name="lights"/>.</param>
    /// <param name="cam">Per-frame camera snapshot for world → swapchain projection.</param>
    public static int CullPointLights(
        PointLight[] lights,
        int count,
        in ShadowSdfCamera cam)
        => CullLights<PointLight, PointLightAccessor>(lights, count, in cam);

    /// <summary>
    /// Compacts <paramref name="lights"/> in-place, removing spot lights whose projected cone AABB (in swapchain
    /// pixels) is entirely outside the physical viewport. Uses <see cref="SpotLightBounds.ComputeProjectedConeAabb"/>
    /// (via <see cref="SpotLightAccessor.GetSwapchainAabb"/>) so the bounding box accounts for camera rotation,
    /// rejecting narrow cones whose full-circle AABB would overlap but whose actual illumination area does not.
    /// </summary>
    public static int CullSpotLights(
        SpotLight[] lights,
        int count,
        in ShadowSdfCamera cam)
        => CullLights<SpotLight, SpotLightAccessor>(lights, count, in cam);

    /// <summary>
    /// Generic viewport cull: compacts <paramref name="lights"/> in-place, removing entries whose swapchain-pixel
    /// AABB lies entirely outside the physical viewport. <typeparamref name="TAccessor"/> extracts position,
    /// radius, and computes the AABB (circle for points, projected cone for spots); the struct constraint lets the
    /// JIT devirtualize every call.
    /// </summary>
    /// <remarks>
    /// When <see cref="ShadowSdfCamera.PhysicalScale"/> is zero or negative (default/invalid camera), all lights
    /// are dropped because nothing can be lit correctly — world → swapchain projection is degenerate.
    /// </remarks>
    private static int CullLights<TLight, TAccessor>(
        TLight[] lights,
        int count,
        in ShadowSdfCamera cam)
        where TAccessor : struct, ILightAccessor<TLight>
    {
        if (count <= 0)
            return count;
        if (cam.PhysicalScale <= 0f)
            return 0;

        var accessor = default(TAccessor);
        var physMinX = cam.PhysicalOffsetSwapchainPx.X;
        var physMinY = cam.PhysicalOffsetSwapchainPx.Y;
        var physMaxX = physMinX + cam.PhysicalSizeSwapchainPx.X;
        var physMaxY = physMinY + cam.PhysicalSizeSwapchainPx.Y;

        var writeIdx = 0;
        for (var i = 0; i < count; i++)
        {
            ref readonly var light = ref lights[i];
            var swPx = cam.WorldToSwapchainPx(accessor.GetPositionWorld(in light));
            var radiusPx = MathF.Max(accessor.GetRadius(in light), 0f) * cam.PhysicalScale;

            accessor.GetSwapchainAabb(in light, swPx, radiusPx, in cam,
                out var lMinX, out var lMinY, out var lMaxX, out var lMaxY);

            if (lMaxX < physMinX || lMinX > physMaxX ||
                lMaxY < physMinY || lMinY > physMaxY)
                continue;

            if (writeIdx != i)
                lights[writeIdx] = lights[i];
            writeIdx++;
        }

        return writeIdx;
    }
}
