using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Cyberland.Engine.Scene;

namespace Cyberland.Engine.Rendering;

/// <summary>Internal GPU texture slot (engine use).</summary>
internal sealed class GpuTexture
{
    public Image Image;
    public DeviceMemory Memory;
    public ImageView View;
    public DescriptorSet DescriptorSet;
    public int Width;
    public int Height;
}

/// <summary>
/// Broad draw bucket for sprites: assign to <see cref="SpriteDrawRequest.Layer"/>; lower numeric values draw before higher bands (within a band, <see cref="SpriteDrawRequest.SortKey"/> orders draws).
/// </summary>
public enum SpriteLayer : int
{
    /// <summary>Backdrops and fullscreen fills.</summary>
    Background = 0,
    /// <summary>Gameplay sprites (characters, projectiles).</summary>
    World = 100,
    /// <summary>Effects layered above world.</summary>
    Fx = 200,
    /// <summary>HUD and menus (topmost band by default).</summary>
    Ui = 300
}

/// <summary>
/// Immediate-mode sprite submit (used by <see cref="IRenderer.SubmitSprite"/>): axis-aligned quad with optional rotation, materials, and transparency mode.
/// </summary>
/// <remarks>Straight alpha: <see cref="Alpha"/> multiplies RGB after texture sampling.</remarks>
public struct SpriteDrawRequest
{
    /// <summary>Center position in either <see cref="CoordinateSpace.WorldSpace"/> or <see cref="CoordinateSpace.ViewportSpace"/>.</summary>
    public Vector2D<float> CenterWorld;
    /// <summary>Half-width / half-height in world / viewport units before rotation (same pixel unit as <see cref="CenterWorld"/>).</summary>
    public Vector2D<float> HalfExtentsWorld;
    /// <summary>Counter-clockwise rotation in radians about the sprite center.</summary>
    public float RotationRadians;
    /// <summary>Cast to <see cref="SpriteLayer"/> for bucketed draws.</summary>
    public int Layer;
    /// <summary>Tie-break within layer (larger draws later).</summary>
    public float SortKey;

    /// <summary>Albedo slot from <see cref="IRenderer.RegisterTextureRgba"/>.</summary>
    public TextureId AlbedoTextureId;
    /// <summary><see cref="TextureId.MaxValue"/> uses default flat normal.</summary>
    public TextureId NormalTextureId;
    /// <summary><see cref="TextureId.MaxValue"/> = no emissive texture (use tint only).</summary>
    public TextureId EmissiveTextureId;

    /// <summary>Multiplies sampled RGBA.</summary>
    public Vector4D<float> ColorMultiply;
    /// <summary>Straight-alpha multiplier.</summary>
    public float Alpha;

    /// <summary>Emissive color tint (HDR-ish contribution).</summary>
    public Vector3D<float> EmissiveTint;
    /// <summary>Scales emissive into bloom/HDR paths.</summary>
    public float EmissiveIntensity;

    /// <summary>Linear depth-ish for ordering (0 = back).</summary>
    public float DepthHint;

    /// <summary>Atlas UV rectangle (min.xy, max.zw). When all zero, the renderer uses full texture (0,0)-(1,1).</summary>
    public Vector4D<float> UvRect;

    /// <summary>Weighted blended transparency path (glass/crystal); skipped in opaque G-buffer.</summary>
    public bool Transparent;

    /// <summary>
    /// Coordinate space for <see cref="CenterWorld"/> and <see cref="HalfExtentsWorld"/>:
    /// <see cref="CoordinateSpace.WorldSpace"/> or <see cref="CoordinateSpace.ViewportSpace"/>.
    /// </summary>
    public CoordinateSpace Space;
}

/// <summary>
/// Camera snapshot submitted by <see cref="IRenderer.SubmitCamera"/>. The renderer picks the highest-priority
/// enabled entry per frame; lights, world sprites, and post-volume containment are evaluated against the winner.
/// </summary>
public struct CameraViewRequest
{
    /// <summary>Camera world position (from the owning <see cref="Scene.Transform"/>).</summary>
    public Vector2D<float> PositionWorld;
    /// <summary>Camera CCW rotation in radians about its center.</summary>
    public float RotationRadians;
    /// <summary>Virtual viewport size in world pixels (must be positive for the camera to be eligible).</summary>
    public Vector2D<int> ViewportSizeWorld;
    /// <summary>Higher wins; ties broken by submit order.</summary>
    public int Priority;
    /// <summary>When <c>false</c>, the camera is ignored even if submitted.</summary>
    public bool Enabled;
    /// <summary>Scene clear / letterbox bar color (linear RGBA).</summary>
    public Vector4D<float> BackgroundColor;
}

/// <summary>Radial point light evaluated in the deferred lighting pass (world pixels, +Y up).</summary>
public struct PointLight
{
    /// <summary>Light center in world space.</summary>
    public Vector2D<float> PositionWorld;
    /// <summary>World-space radius where influence falls off (see <see cref="FalloffExponent"/>).</summary>
    public float Radius;
    /// <summary>Linear RGB tint.</summary>
    public Vector3D<float> Color;
    /// <summary>Brightness scaler (artist-tunable).</summary>
    public float Intensity;
    /// <summary>Radial falloff exponent (typical 1.5–3). When 0, renderer uses a default.</summary>
    public float FalloffExponent;
    /// <summary>Reserved; 2D path does not cast shadows from sprites.</summary>
    public bool CastsShadow;
}

/// <summary>Cone light: position, aim direction, and inner/outer angles in radians.</summary>
public struct SpotLight
{
    /// <summary>Cone apex in world space.</summary>
    public Vector2D<float> PositionWorld;
    /// <summary>Normalized direction the cone opens toward (world x/y).</summary>
    public Vector2D<float> DirectionWorld;
    /// <summary>Radial reach of the cone in world units.</summary>
    public float Radius;
    /// <summary>Full cone angle where intensity is full (radians).</summary>
    public float InnerConeRadians;
    /// <summary>Outer cone angle with smooth falloff (radians).</summary>
    public float OuterConeRadians;
    /// <summary>Linear RGB color.</summary>
    public Vector3D<float> Color;
    /// <summary>Brightness scaler.</summary>
    public float Intensity;
    /// <summary>Reserved for future shadowing.</summary>
    public bool CastsShadow;
}

/// <summary>Directional sun/key light: angle-only; no position (infinite distance).</summary>
public struct DirectionalLight
{
    /// <summary>Normalized direction **to** the light (shading uses opposite for incoming light).</summary>
    public Vector2D<float> DirectionWorld;
    /// <summary>Linear RGB tint.</summary>
    public Vector3D<float> Color;
    /// <summary>Brightness scaler.</summary>
    public float Intensity;
    /// <summary>Reserved.</summary>
    public bool CastsShadow;
}

/// <summary>Uniform hemispheric fill (no direction).</summary>
public struct AmbientLight
{
    /// <summary>Linear RGB tint.</summary>
    public Vector3D<float> Color;
    /// <summary>Overall intensity scaler.</summary>
    public float Intensity;
}

/// <summary>Post-volume authoring data; world placement comes from the owning entity <c>Transform</c>.</summary>
public struct PostProcessVolume
{
    /// <summary>Half-width / half-height in local volume space before world transform scale.</summary>
    public Vector2D<float> HalfExtentsLocal;
    /// <summary>Larger values override lower on intersections.</summary>
    public int Priority;
    /// <summary>Optional per-field overrides merged when the camera/player is inside the volume.</summary>
    public PostProcessOverrides Overrides;
}

/// <summary>
/// Sparse override set: each <c>Has*</c> flag gates whether the paired float replaces the current global/post chain value.
/// </summary>
public struct PostProcessOverrides
{
    /// <summary>When true, <see cref="BloomRadius"/> applies.</summary>
    public bool HasBloomRadius;
    /// <summary>Bloom blur kernel radius (engine-defined units).</summary>
    public float BloomRadius;
    /// <summary>When true, <see cref="BloomGain"/> applies.</summary>
    public bool HasBloomGain;
    /// <summary>Bloom mix into HDR.</summary>
    public float BloomGain;
    /// <summary>When true, <see cref="EmissiveToHdrGain"/> applies.</summary>
    public bool HasEmissiveToHdrGain;
    /// <summary>Scales emissive into the HDR buffer.</summary>
    public float EmissiveToHdrGain;
    /// <summary>When true, <see cref="EmissiveToBloomGain"/> applies.</summary>
    public bool HasEmissiveToBloomGain;
    /// <summary>Feeds emissive into bloom extraction.</summary>
    public float EmissiveToBloomGain;
    /// <summary>When true, <see cref="Exposure"/> applies.</summary>
    public bool HasExposure;
    /// <summary>Scene exposure multiplier.</summary>
    public float Exposure;
    /// <summary>When true, <see cref="Saturation"/> applies.</summary>
    public bool HasSaturation;
    /// <summary>Color saturation (1 = neutral).</summary>
    public float Saturation;
}

/// <summary>
/// Full-frame post settings for <see cref="IRenderer.SetGlobalPostProcess"/>; persists until the next call (not cleared each frame).
/// </summary>
public struct GlobalPostProcessSettings
{
    /// <summary>Master toggle for bloom chain.</summary>
    public bool BloomEnabled;
    /// <summary>Base bloom blur radius.</summary>
    public float BloomRadius;
    /// <summary>Strength of bloom add into the composite.</summary>
    public float BloomGain;
    /// <summary>
    /// HDR luminance threshold for bloom extraction (scene-linear). Below this, contribution is suppressed; knee softens the transition.
    /// </summary>
    public float BloomExtractThreshold;
    /// <summary>
    /// Soft knee width for the bloom threshold (same units as <see cref="BloomExtractThreshold"/>); higher values reduce harsh cutoffs.
    /// </summary>
    public float BloomExtractKnee;
    /// <summary>How much emissive feeds the HDR scene color.</summary>
    public float EmissiveToHdrGain;
    /// <summary>How much emissive feeds bloom extraction.</summary>
    public float EmissiveToBloomGain;
    /// <summary>Exposure multiplier before tonemap.</summary>
    public float Exposure;
    /// <summary>1 = neutral saturation.</summary>
    public float Saturation;
    /// <summary>When false, skips tonemapping (debug/HDR displays).</summary>
    public bool TonemapEnabled;
    /// <summary>RGB multipliers for shadow tones (color grading).</summary>
    public Vector3D<float> ColorGradingShadows;
    /// <summary>RGB multipliers for mid tones.</summary>
    public Vector3D<float> ColorGradingMidtones;
    /// <summary>RGB multipliers for highlights.</summary>
    public Vector3D<float> ColorGradingHighlights;
}
