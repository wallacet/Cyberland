using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// CPU mirror of the GLSL <c>sdfSoftShadow</c> cone-trace from <c>shadow_sdf_sampling.glsl</c>. Used by unit tests to
/// validate spotlight / point-light visibility against the world-space scene without invoking Vulkan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate space (READ FIRST).</b> Inputs are <b>world</b> (+Y up). The cone-trace internally calls
/// <see cref="ShadowSdfCamera.WorldToSwapchainPx"/> once at the top, then marches <b>entirely</b> in <b>swapchain</b>
/// pixels (+Y down). SDF samples are read in <b>SDF texels</b> and converted to swapchain px via
/// <see cref="ShadowSdfCamera.SdfPxDistanceToSwapchainPx(float)"/>. There are no other space transitions inside the loop.
/// </para>
/// <para>
/// <b>Algorithm (Iñigo Quilez soft shadow).</b> Visibility = <c>clamp(min(k * d(t) / t), 0, 1)</c>: the smallest ratio
/// of nearest occluder distance to march length defines the penumbra. <c>k</c> larger = sharper shadows; smaller = softer.
/// </para>
/// <para>Pure function; safe to invoke from <see cref="Core.Ecs.IParallelSystem"/> workers.</para>
/// </remarks>
internal static class ShadowSdfSamplingCpu
{
    /// <summary>Default max samples used by tests; matches <see cref="ShadowSettings"/> default.</summary>
    public const int DefaultMaxSamples = 32;

    /// <summary>Default kSoft used by tests; matches <see cref="ShadowSettings"/> default.</summary>
    public const float DefaultKSoft = 16f;

    /// <summary>
    /// Inner cone-trace loop operating entirely in swapchain pixels (+Y down). All public shadow methods
    /// project their inputs into this space and delegate here. This is the single source of truth for
    /// the Iñigo Quilez soft-shadow march: vis = clamp(min(k * d(t) / t), 0, 1).
    /// Mirrors <c>_marchConeSdf</c> in <c>shadow_sdf_sampling.glsl</c>.
    /// </summary>
    private static float MarchConeSdf(
        Vector2D<float> fragSwapchainPx,
        Vector2D<float> lightSwapchainPx,
        ReadOnlySpan<float> sdf,
        in ShadowSdfCamera cam,
        float kSoft,
        int maxSamples,
        float depthBiasWorld)
    {
        var toLightSwapchainPx = lightSwapchainPx - fragSwapchainPx;
        var totalSwapchainPx = MathF.Sqrt(toLightSwapchainPx.X * toLightSwapchainPx.X + toLightSwapchainPx.Y * toLightSwapchainPx.Y);
        if (totalSwapchainPx < 1f)
            return 1f;

        var invTotalSwapchainPx = 1f / totalSwapchainPx;
        var marchDirSwapchainPx = new Vector2D<float>(
            toLightSwapchainPx.X * invTotalSwapchainPx,
            toLightSwapchainPx.Y * invTotalSwapchainPx);

        var sdfSizePx = cam.SdfSizePx;
        var depthBiasSwapchainPx = depthBiasWorld * cam.PhysicalScale;
        var tSwapchainPx = MathF.Max(2f, depthBiasSwapchainPx);
        var vis = 1f;
        for (var i = 0; i < maxSamples && tSwapchainPx < totalSwapchainPx; i++)
        {
            var sampleSwapchainPx = new Vector2D<float>(
                fragSwapchainPx.X + marchDirSwapchainPx.X * tSwapchainPx,
                fragSwapchainPx.Y + marchDirSwapchainPx.Y * tSwapchainPx);
            var sampleSdfPx = cam.SwapchainPxToSdfPx(sampleSwapchainPx);
            var distSdfPx = ShadowDistanceFieldCpu.Sample(sdf, sdfSizePx, sampleSdfPx);
            var distSwapchainPx = cam.SdfPxDistanceToSwapchainPx(distSdfPx);
            if (distSwapchainPx < 0.05f)
                return 0f;
            var ratio = kSoft * distSwapchainPx / tSwapchainPx;
            if (ratio < vis)
                vis = ratio;
            tSwapchainPx += MathF.Max(distSwapchainPx, 1f);
        }

        return vis < 0f ? 0f : (vis > 1f ? 1f : vis);
    }

    /// <summary>
    /// Cone-trace visibility from <paramref name="fragWorld"/> to <paramref name="lightWorld"/> (both world +Y up).
    /// Returns 1 = fully lit, 0 = fully occluded; intermediate values = penumbra.
    /// </summary>
    /// <param name="fragWorld">Fragment position in world space (+Y up).</param>
    /// <param name="lightWorld">Light position in world space (+Y up).</param>
    /// <param name="sdf">Flat SDF texel array built by <see cref="ShadowDistanceFieldCpu"/>.</param>
    /// <param name="cam">Per-frame camera snapshot that converts between world, swapchain, and SDF spaces.</param>
    /// <param name="kSoft">Soft-shadow sharpness (Iñigo Quilez <c>k</c>); larger = sharper.</param>
    /// <param name="maxSamples">Maximum march iterations before early-out.</param>
    /// <param name="depthBiasWorld">
    /// Surface bias in world pixels, matching <see cref="ShadowSettings.DepthBias"/>. The initial march
    /// step is <c>max(2, depthBiasWorld * physicalScale)</c>, mirroring the GLSL cone-trace.
    /// </param>
    public static float SoftShadow(
        Vector2D<float> fragWorld,
        Vector2D<float> lightWorld,
        ReadOnlySpan<float> sdf,
        in ShadowSdfCamera cam,
        float kSoft = DefaultKSoft,
        int maxSamples = DefaultMaxSamples,
        float depthBiasWorld = 0f)
    {
        return MarchConeSdf(
            cam.WorldToSwapchainPx(fragWorld),
            cam.WorldToSwapchainPx(lightWorld),
            sdf, in cam, kSoft, maxSamples, depthBiasWorld);
    }

    /// <summary>
    /// Cone-trace visibility from <paramref name="fragSwapchainPx"/> (already projected) to <paramref name="lightWorld"/>
    /// (world +Y up). Only projects the light; avoids the redundant per-light fragment projection that
    /// <see cref="SoftShadow"/> performs. Returns 1 = fully lit, 0 = fully occluded.
    /// </summary>
    public static float SoftShadowSwapchainFrag(
        Vector2D<float> fragSwapchainPx,
        Vector2D<float> lightWorld,
        ReadOnlySpan<float> sdf,
        in ShadowSdfCamera cam,
        float kSoft = DefaultKSoft,
        int maxSamples = DefaultMaxSamples,
        float depthBiasWorld = 0f)
    {
        return MarchConeSdf(
            fragSwapchainPx,
            cam.WorldToSwapchainPx(lightWorld),
            sdf, in cam, kSoft, maxSamples, depthBiasWorld);
    }

    /// <summary>
    /// Cone-trace visibility with both fragment and light positions already in swapchain pixels. Avoids
    /// <see cref="ShadowSdfCamera.WorldToSwapchainPx"/> entirely — use when the light's swapchain position
    /// is pre-computed (e.g. spot lights whose SSBO row 0 carries the projected position).
    /// </summary>
    public static float SoftShadowSwapchainFragSwapchainLight(
        Vector2D<float> fragSwapchainPx,
        Vector2D<float> lightSwapchainPx,
        ReadOnlySpan<float> sdf,
        in ShadowSdfCamera cam,
        float kSoft = DefaultKSoft,
        int maxSamples = DefaultMaxSamples,
        float depthBiasWorld = 0f)
    {
        return MarchConeSdf(
            fragSwapchainPx, lightSwapchainPx,
            sdf, in cam, kSoft, maxSamples, depthBiasWorld);
    }

    /// <summary>
    /// Directional cone-trace with pre-computed fragment swapchain position. Marches from
    /// <paramref name="fragSwapchainPx"/> in <paramref name="lightDirWorld"/> for a fixed world distance.
    /// </summary>
    public static float DirectionalSoftShadowSwapchainFrag(
        Vector2D<float> fragSwapchainPx,
        Vector2D<float> fragWorld,
        Vector2D<float> lightDirWorld,
        float traceWorldDist,
        ReadOnlySpan<float> sdf,
        in ShadowSdfCamera cam,
        float kSoft = DefaultKSoft,
        int maxSamples = DefaultMaxSamples,
        float depthBiasWorld = 0f)
    {
        var len = MathF.Sqrt(lightDirWorld.X * lightDirWorld.X + lightDirWorld.Y * lightDirWorld.Y);
        if (len < 1e-6f)
            return 1f;
        var dirWorld = new Vector2D<float>(lightDirWorld.X / len, lightDirWorld.Y / len);
        var virtualLightWorld = new Vector2D<float>(
            fragWorld.X + dirWorld.X * MathF.Max(traceWorldDist, 1f),
            fragWorld.Y + dirWorld.Y * MathF.Max(traceWorldDist, 1f));
        return SoftShadowSwapchainFrag(fragSwapchainPx, virtualLightWorld, sdf, in cam, kSoft, maxSamples, depthBiasWorld);
    }

    /// <summary>
    /// Cone-trace visibility for a <b>directional</b> light: march opposite the light's world direction for a fixed
    /// world distance. <paramref name="lightDirWorld"/> is the unit direction toward the light (world +Y up).
    /// </summary>
    public static float DirectionalSoftShadow(
        Vector2D<float> fragWorld,
        Vector2D<float> lightDirWorld,
        float traceWorldDist,
        ReadOnlySpan<float> sdf,
        in ShadowSdfCamera cam,
        float kSoft = DefaultKSoft,
        int maxSamples = DefaultMaxSamples,
        float depthBiasWorld = 0f)
    {
        var len = MathF.Sqrt(lightDirWorld.X * lightDirWorld.X + lightDirWorld.Y * lightDirWorld.Y);
        if (len < 1e-6f)
            return 1f;
        var dirWorld = new Vector2D<float>(lightDirWorld.X / len, lightDirWorld.Y / len);
        var virtualLightWorld = new Vector2D<float>(
            fragWorld.X + dirWorld.X * MathF.Max(traceWorldDist, 1f),
            fragWorld.Y + dirWorld.Y * MathF.Max(traceWorldDist, 1f));
        return SoftShadow(fragWorld, virtualLightWorld, sdf, in cam, kSoft, maxSamples, depthBiasWorld);
    }
}
