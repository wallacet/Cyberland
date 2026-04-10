using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene2D;

/// <summary>
/// Drawable sprite payload; world pose comes from <see cref="Position"/> / <see cref="Rotation"/> / <see cref="Scale"/>.
/// Submitted by <see cref="Systems.SpriteRenderSystem"/>.
/// </summary>
public struct Sprite
{
    public Vector2D<float> HalfExtents;
    public int AlbedoTextureId;
    public int NormalTextureId;
    public int EmissiveTextureId;
    public int Layer;
    public float SortKey;
    public Vector4D<float> ColorMultiply;
    public float Alpha = 1f;
    public Vector3D<float> EmissiveTint;
    public float EmissiveIntensity;
    public float DepthHint;
    /// <summary>Atlas UV (min.xy, max.zw). Zero = full texture.</summary>
    public Vector4D<float> UvRect;

    /// <summary>When true, drawn with WBOIT over opaque HDR instead of the deferred G-buffer.</summary>
    public bool Transparent;

    public bool Visible;

    /// <summary>Struct field initializers require an explicit constructor; <see cref="Alpha"/> defaults to 1.</summary>
    public Sprite()
    {
        this = default;
        Alpha = 1f;
    }

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
