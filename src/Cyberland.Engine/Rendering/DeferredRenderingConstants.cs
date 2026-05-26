using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Shared HDR deferred / bloom / SDF-shadow numeric limits and formats for <see cref="VulkanRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BloomDownsampleLevels"/> and <see cref="BloomBlurPingPongs"/> define GPU resource topology (image counts,
/// descriptor array sizes). They are not intended for per-frame toggles; changing them requires offscreen target recreation.
/// Per-frame bloom tuning uses <see cref="GlobalPostProcessSettings"/> (radius, gain, extract threshold/knee).
/// Per-frame shadow tuning uses <see cref="ShadowSettings"/> (SDF scale, cone-trace samples, kSoft).
/// </para>
/// </remarks>
internal static class DeferredRenderingConstants
{
    /// <summary>HDR scene and G-buffer color format.</summary>
    public const Format HdrFormat = Format.R16G16B16A16Sfloat;

    /// <summary>WBOIT reveal/weight storage.</summary>
    public const Format WboitRevealFormat = Format.R16Sfloat;

    /// <summary>Half-res bloom pyramid depth (excluding full half-res bloom0/bloom1).</summary>
    public const int BloomDownsampleLevels = 2;

    /// <summary>
    /// Separable blur passes at the smallest mip before upsample. This is a fixed GPU cost regardless of
    /// <see cref="GlobalPostProcessSettings.BloomRadius"/>; the radius parameter only scales the blur kernel width.
    /// </summary>
    public const int BloomBlurPingPongs = 4;

    /// <summary>Upper bound for point lights in the deferred lighting path. SDF pipeline has no atlas pressure.</summary>
    public const int MaxPointLights = 1024;

    /// <summary>
    /// Upper bound for directional lights in the tiled deferred lighting pass. Generous for 2D — typical use
    /// is 1–3 sun/moon directions plus optional rim lighting; the cap exists to bound SSBO size at
    /// 16 × 3 vec4 = 768 bytes.
    /// </summary>
    public const int MaxDirectionalLights = 16;

    /// <summary>Upper bound for spot lights in the tiled deferred lighting pass.</summary>
    public const int MaxSpotLights = 256;

    /// <summary>Upper bound for ambient lights; exceeding this emits a diagnostic warning.</summary>
    public const int MaxAmbientLights = 32;

    /// <summary>Upper bound for deferred/world sprite submissions consumed by one frame plan.</summary>
    public const int MaxDeferredSprites = 65536;

    /// <summary>Upper bound for viewport/swapchain overlay sprite submissions consumed by one frame plan.</summary>
    public const int MaxViewportOverlaySprites = 16384;

    /// <summary>Upper bound for queued text glyph submissions consumed by one frame plan.</summary>
    public const int MaxTextGlyphs = 65536;

    /// <summary>Tile size in <b>swapchain pixels</b> for the tiled deferred lighting pass and CPU culler.</summary>
    public const int TileSizeSwapchainPx = 64;

    /// <summary>Maximum number of tile-grid cells allocated for the tiled deferred lighting SSBO.</summary>
    public const int MaxTileGridCells = 4096;

    /// <summary>Per-tile point light cap for the tiled deferred lighting pass and CPU culler.</summary>
    public const int MaxLightsPerTile = 128;

    /// <summary>Per-tile spot light cap for the tiled deferred lighting pass.</summary>
    public const int MaxSpotLightsPerTile = 64;

    /// <summary>
    /// Hard iteration cap for the GLSL cone-trace loop in <c>shadow_sdf_sampling.glsl</c>.
    /// <see cref="ShadowSettings.ConeTraceSamples"/> is clamped to this value at UBO upload time.
    /// </summary>
    public const int MaxConeTraceSamples = 64;

    /// <summary>Minimum world-pixel radius for promoted emissive lights, preventing near-zero-size sprites from wasting light slots.</summary>
    public const float MinPromotedLightRadiusWorld = 8f;

    /// <summary>Absolute upper bound on emissive-promoted lights regardless of <see cref="EmissivePromotionSettings.MaxPromotedLightsPerFrame"/>.</summary>
    public const int MaxPromotedLightsCap = 256;

    /// <summary>R8 mask format for the shadow occluder raster pass.</summary>
    public const Format ShadowOccluderMaskFormat = Format.R8Unorm;

    /// <summary>R16G16 SNORM format for the JFA ping-pong "nearest filled texel" images.</summary>
    public const Format ShadowJfaSeedFormat = Format.R16G16SNorm;

    /// <summary>Maximum dimension (width or height) for shadow SDF images. Limits GPU memory at extreme resolutions.</summary>
    public const uint MaxShadowSdfDim = 4096;

    /// <summary>R16F signed-distance-field storage for the final SDF (distances in SDF texels).</summary>
    public const Format ShadowSdfFormat = Format.R16Sfloat;

    /// <summary>
    /// R16F clear value for the SDF when shadows are disabled or no occluders exist. Close to
    /// half-float max (65504); any cone-trace sample returning this distance produces full visibility.
    /// </summary>
    public const float ShadowSdfFullyLitSentinelTexels = 65500f;

    /// <summary>
    /// Minimum alpha to consider a fragment lit (used by sprite_gbuffer, tiled_deferred_lighting, shadow_occluder).
    /// Fragments below this threshold are discarded or treated as empty.
    /// </summary>
    /// <remarks>
    /// G-buffer discards at 0.02 alpha; lighting treats &lt; 0.001 as background. The gap [0.001, 0.02) is
    /// practically unreachable since G-buffer fragments below 0.02 are discarded before write.
    /// </remarks>
    public const float AlphaDiscardThreshold = 0.02f;

    /// <summary>
    /// Below this entity count, light-system chunk updates use a sequential loop instead of
    /// <c>Parallel.For</c> to avoid thread-pool overhead for small chunks already parallelized at
    /// the chunk level by the ECS scheduler.
    /// </summary>
    public const int LightSystemParallelThreshold = 8;

    /// <summary>
    /// Computes disjoint instance-buffer region bases and total capacity for the deferred sprite pipeline.
    /// Each region holds up to <paramref name="deferredSpriteCount"/> instances (except overlay, which has its own cap).
    /// </summary>
    /// <remarks>
    /// Regions: emissive (0), opaque (1), transparent (2), shadow occluder (3), overlay (4).
    /// When <paramref name="deferredSpriteCount"/> is zero the buffer only needs overlay capacity.
    /// </remarks>
    internal static (int EmissiveBase, int OpaqueBase, int TransparentBase, int ShadowOccluderBase, int OverlayBase, int TotalCapacity)
        ComputeSpriteInstanceLayout(int deferredSpriteCount, int overlaySpriteCap)
    {
        if (deferredSpriteCount == 0)
            return (0, 0, 0, 0, 0, overlaySpriteCap);
        var stride = deferredSpriteCount;
        return (0, stride, stride * 2, stride * 3, stride * 4, stride * 4 + overlaySpriteCap);
    }
}
