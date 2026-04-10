using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Drawable sprite payload; world pose comes from <see cref="Position"/> / <see cref="Rotation"/> / <see cref="Scale"/>.
/// Submitted by <see cref="Systems.SpriteRenderSystem"/>.
/// </summary>
public struct Sprite
{
    /// <summary>Half-size in world units (rectangle from center).</summary>
    public Vector2D<float> HalfExtents;
    /// <summary>Slot from <see cref="Rendering.IRenderer.RegisterTextureRgba"/>; albedo/base color.</summary>
    public int AlbedoTextureId;
    /// <summary>Normal map slot (tangent space); use renderer default for flat shading.</summary>
    public int NormalTextureId;
    /// <summary>Emissive texture slot, or -1 for tint-only emissive.</summary>
    public int EmissiveTextureId;
    /// <summary>Draw bucket; lower <see cref="Rendering.SpriteLayer"/> values draw first within the main sprite pass.</summary>
    public int Layer;
    /// <summary>Tie-breaker when <see cref="Layer"/> matches (larger = draw on top).</summary>
    public float SortKey;
    /// <summary>Multiplies sampled albedo RGBA (straight alpha).</summary>
    public Vector4D<float> ColorMultiply;
    /// <summary>Straight-alpha multiplier on color after sampling.</summary>
    public float Alpha = 1f;
    /// <summary>Emissive tint when no emissive texture or as multiply.</summary>
    public Vector3D<float> EmissiveTint;
    /// <summary>Scales emissive contribution into HDR / bloom.</summary>
    public float EmissiveIntensity;
    /// <summary>Ordering hint for depth-like sorting among sprites.</summary>
    public float DepthHint;
    /// <summary>Atlas UV (min.xy, max.zw). Zero = full texture.</summary>
    public Vector4D<float> UvRect;

    /// <summary>When true, drawn with WBOIT over opaque HDR instead of the deferred G-buffer.</summary>
    public bool Transparent;

    /// <summary>When false, the sprite submitter skips this drawable.</summary>
    public bool Visible;

    /// <summary>Struct field initializers require an explicit constructor; <see cref="Alpha"/> defaults to 1.</summary>
    public Sprite()
    {
        this = default;
        Alpha = 1f;
    }

    /// <summary>Convenience preset: white quad, default layer, fully opaque, no emissive.</summary>
    public static Sprite DefaultWhiteUnlit(int whiteTextureId, int normalTextureId, Vector2D<float> halfExtents)
    {
        Sprite s;
        s.HalfExtents = halfExtents;
        s.AlbedoTextureId = whiteTextureId;
        s.NormalTextureId = normalTextureId;
        s.EmissiveTextureId = -1;
        s.Layer = (int)SpriteLayer.World;
        s.SortKey = 0f;
        s.ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f);
        s.Alpha = 1f;
        s.EmissiveTint = default;
        s.EmissiveIntensity = 0f;
        s.DepthHint = 0f;
        s.UvRect = default;
        s.Transparent = false;
        s.Visible = true;
        return s;
    }
}
