using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Shared HDR deferred / bloom numeric limits and formats for <see cref="VulkanRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BloomDownsampleLevels"/> and <see cref="BloomBlurPingPongs"/> define GPU resource topology (image counts,
/// descriptor array sizes). They are not intended for per-frame toggles; changing them requires offscreen target recreation.
/// Per-frame bloom tuning uses <see cref="GlobalPostProcessSettings"/> (radius, gain, extract threshold/knee).
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

    /// <summary>Separable blur passes at the smallest mip before upsample.</summary>
    public const int BloomBlurPingPongs = 4;

    /// <summary>Upper bound for point lights in the deferred lighting path.</summary>
    public const int MaxPointLights = 256;
}
