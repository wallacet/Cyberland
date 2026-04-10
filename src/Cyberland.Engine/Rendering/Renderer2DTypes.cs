using Silk.NET.Maths;
using Silk.NET.Vulkan;

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

/// <summary>Sprite layer: lower draws first (background).</summary>
public enum SpriteLayer : int
{
    Background = 0,
    World = 100,
    Fx = 200,
    Ui = 300
}

/// <summary>Straight alpha: <see cref="Alpha"/> multiplies RGB after sampling.</summary>
public struct SpriteDrawRequest
{
    public Vector2D<float> CenterWorld;
    public Vector2D<float> HalfExtentsWorld;
    /// <summary>Counter-clockwise rotation in radians about the sprite center (world +Y up).</summary>
    public float RotationRadians;
    public int Layer;
    /// <summary>Tie-break within layer (e.g. same layer sort).</summary>
    public float SortKey;

    public int AlbedoTextureId;
    /// <summary>-1 uses default flat normal.</summary>
    public int NormalTextureId;
    /// <summary>-1 = no emissive texture (use tint only).</summary>
    public int EmissiveTextureId;

    public Vector4D<float> ColorMultiply;
    public float Alpha;

    public Vector3D<float> EmissiveTint;
    public float EmissiveIntensity;

    /// <summary>Linear depth-ish for ordering (0 = back).</summary>
    public float DepthHint;

    /// <summary>Atlas UV rectangle (min.xy, max.zw). When all zero, the renderer uses full texture (0,0)-(1,1).</summary>
    public Vector4D<float> UvRect;

    /// <summary>Weighted blended transparency path (glass/crystal); skipped in opaque G-buffer.</summary>
    public bool Transparent;
}

public struct PointLight
{
    public Vector2D<float> PositionWorld;
    public float Radius;
    public Vector3D<float> Color;
    public float Intensity;
    /// <summary>Radial falloff exponent (typical 1.5–3). When 0, renderer uses a default.</summary>
    public float FalloffExponent;
    public bool CastsShadow;
}

public struct SpotLight
{
    public Vector2D<float> PositionWorld;
    public Vector2D<float> DirectionWorld;
    public float Radius;
    public float InnerConeRadians;
    public float OuterConeRadians;
    public Vector3D<float> Color;
    public float Intensity;
    public bool CastsShadow;
}

public struct DirectionalLight
{
    public Vector2D<float> DirectionWorld;
    public Vector3D<float> Color;
    public float Intensity;
    public bool CastsShadow;
}

public struct AmbientLight
{
    public Vector3D<float> Color;
    public float Intensity;
}

/// <summary>World-space AABB volume (+Y up).</summary>
public struct PostProcessVolume
{
    public Vector2D<float> MinWorld;
    public Vector2D<float> MaxWorld;
    public int Priority;
    public PostProcessOverrides Overrides;
}

public struct PostProcessOverrides
{
    public bool HasBloomRadius;
    public float BloomRadius;
    public bool HasBloomGain;
    public float BloomGain;
    public bool HasEmissiveToHdrGain;
    public float EmissiveToHdrGain;
    public bool HasEmissiveToBloomGain;
    public float EmissiveToBloomGain;
    public bool HasExposure;
    public float Exposure;
    public bool HasSaturation;
    public float Saturation;
}

public struct GlobalPostProcessSettings
{
    public bool BloomEnabled;
    public float BloomRadius;
    public float BloomGain;
    public float EmissiveToHdrGain;
    public float EmissiveToBloomGain;
    public float Exposure;
    /// <summary>1 = neutral.</summary>
    public float Saturation;
    public bool TonemapEnabled;
    public Vector3D<float> ColorGradingShadows;
    public Vector3D<float> ColorGradingMidtones;
    public Vector3D<float> ColorGradingHighlights;
}
