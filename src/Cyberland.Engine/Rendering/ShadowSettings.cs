namespace Cyberland.Engine.Rendering;

/// <summary>
/// Per-frame shadow quality and master toggle for the SDF + tiled deferred lighting pipeline. Copied from
/// <see cref="GlobalPostProcessSettings.Shadows"/> each frame.
/// </summary>
/// <remarks>
/// <para>
/// All distances and biases below are in <b>world pixels</b> unless suffixed with <c>SwapchainPx</c>. The SDF itself
/// stores distances in <b>SDF texels</b>; conversion to swapchain pixels is the single responsibility of
/// <see cref="ShadowSdfCamera.SdfPxDistanceToSwapchainPx(float)"/> at the cone-trace sample site.
/// </para>
/// <para>
/// <b>Thread safety:</b> value type, snapshotted per frame. Copied into <see cref="GlobalPostProcessSettings.Shadows"/>
/// each frame; safe to share across <see cref="Core.Ecs.IParallelSystem"/> workers without locking.
/// </para>
/// </remarks>
public struct ShadowSettings
{
    /// <summary>When false, lighting skips SDF construction and falls back to fully-lit visibility.</summary>
    public bool Enabled;

    /// <summary>
    /// SDF resolution multiplier relative to the swapchain (1.0 = swapchain parity; 0.5 = quarter-area).
    /// Values above 1.0 are clamped to 1.0; the SDF cannot exceed the swapchain extent.
    /// Values below 0.0625 are floored to 0.0625 by <see cref="ShadowSdfCamera"/>.
    /// </summary>
    public float SdfScale;

    /// <summary>Maximum cone-trace samples per shadow visibility test. 16 = fast, 32 = quality.</summary>
    /// <remarks>
    /// <para>
    /// Total per-fragment cost scales with <c>lightsAffectingTile × ConeTraceSamples</c>.
    /// Tiled deferred lighting bounds the multiplier at <see cref="DeferredRenderingConstants.MaxLightsPerTile"/>.
    /// </para>
    /// <para>
    /// The GLSL cone-trace loop has a hard iteration cap of
    /// <see cref="DeferredRenderingConstants.MaxConeTraceSamples"/> (64); the UBO upload path clamps to this
    /// limit so values above 64 have no effect on the GPU.
    /// </para>
    /// </remarks>
    public int ConeTraceSamples;

    /// <summary>
    /// Soft-shadow sharpness. Iñigo Quilez <c>k</c>: larger = sharper, smaller = softer. Default 16 gives a small but
    /// visible penumbra.
    /// </summary>
    public float SoftShadowK;

    /// <summary>World-space bias applied at the cone-trace surface origin to avoid self-shadowing acne.</summary>
    /// <remarks>
    /// Bias is in world pixels; effective swapchain bias is <c>DepthBias * physicalScale</c>.
    /// Increase <see cref="DepthBias"/> if acne appears under heavy letterboxing.
    /// </remarks>
    public float DepthBias;

    /// <summary>
    /// World-space distance directional lights cone-trace from each shaded fragment. 0 = renderer auto-sizes from
    /// camera bounds.
    /// </summary>
    public float DirectionalTraceWorldDist;

    /// <summary>Baseline shadow settings for new renderer instances.</summary>
    public static ShadowSettings Default => new()
    {
        Enabled = true,
        SdfScale = 1f,
        ConeTraceSamples = 32,
        SoftShadowK = 16f,
        DepthBias = 0.5f,
        DirectionalTraceWorldDist = 0f,
    };
}
