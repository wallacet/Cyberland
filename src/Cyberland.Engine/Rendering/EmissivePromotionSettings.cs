namespace Cyberland.Engine.Rendering;

/// <summary>
/// Settings controlling automatic promotion of bright emissive sprites into synthetic
/// <see cref="PointLight"/> entries, used by <see cref="EmissiveLightPromotionCpu"/>.
/// </summary>
/// <remarks>
/// These are CPU-side light-creation knobs, not shadow quality controls. They live alongside
/// <see cref="ShadowSettings"/> on <see cref="GlobalPostProcessSettings"/> because promoted
/// lights default to <c>CastsShadow = true</c> and thus feed the shadow pipeline.
/// </remarks>
public struct EmissivePromotionSettings
{
    /// <summary>Sprites with <see cref="SpriteDrawRequest.EmissiveIntensity"/> ≥ this become point lights automatically.</summary>
    public float EmissiveLightThreshold;

    /// <summary>Hard cap on promoted lights per frame (deterministic truncation in sprite submit order).</summary>
    public int MaxPromotedLightsPerFrame;

    /// <summary>Multiplier on a promoted sprite's world diagonal to derive its <see cref="PointLight.Radius"/>.</summary>
    /// <remarks>
    /// The resulting radius is floored at 8 world-px by <see cref="EmissiveLightPromotionCpu"/> to prevent
    /// near-zero-size sprites from consuming light slots with negligible illumination.
    /// </remarks>
    public float EmissivePromotionRadiusGain;

    /// <summary>Multiplier on <see cref="SpriteDrawRequest.EmissiveIntensity"/> to derive promoted <see cref="PointLight.Intensity"/>.</summary>
    public float EmissivePromotionIntensityGain;

    /// <summary>Baseline emissive promotion settings for new renderer instances.</summary>
    public static EmissivePromotionSettings Default => new()
    {
        EmissiveLightThreshold = 1.5f,
        MaxPromotedLightsPerFrame = 64,
        EmissivePromotionRadiusGain = 3f,
        EmissivePromotionIntensityGain = 1f,
    };
}
