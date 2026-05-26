using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Drawable sprite payload; world pose comes from <see cref="Transform"/> world cache fields (world space, +Y up).
/// Submitted by the stock <see cref="Systems.SpriteRenderSystem"/>; prefer adding components over calling <see cref="Rendering.IRenderer.SubmitSprite"/> from mod code.
/// </summary>
/// <remarks>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
public struct Sprite : IComponent
{
    /// <summary>Half-size in world units (rectangle from center).</summary>
    public Vector2D<float> HalfExtents;
    /// <summary>Slot from <see cref="Rendering.IRenderer.RegisterTextureRgba"/>; albedo/base color.</summary>
    public TextureId AlbedoTextureId;
    /// <summary>Normal map slot (tangent space); use renderer default for flat shading.</summary>
    public TextureId NormalTextureId;
    /// <summary>Emissive texture slot, or <see cref="TextureId.MaxValue"/> for tint-only emissive.</summary>
    public TextureId EmissiveTextureId;
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

    /// <summary>
    /// When true and <see cref="Transparent"/> is false, the sprite is rasterized into the SDF occluder mask and
    /// influences cone-trace shadow visibility. Transparent sprites are excluded from the occluder mask regardless
    /// of this flag.
    /// </summary>
    /// <remarks>
    /// The shadow silhouette on GPU is the alpha-tested sprite quad footprint (not a pure OBB) — fragments below
    /// <see cref="Rendering.DeferredRenderingConstants.AlphaDiscardThreshold"/> (0.02) are discarded from the
    /// occluder mask. Sprites without an <see cref="AlbedoTextureId"/> are skipped (the occluder pass requires
    /// a texture to sample alpha). CPU test oracles (<see cref="Rendering.ShadowDistanceFieldCpu"/>) approximate
    /// occluders as axis-aligned OBBs and may diverge for rotated sprites or irregular alpha shapes.
    /// Shadows use a 2D screen-space SDF; there is no depth or normal-aware bias beyond
    /// <see cref="Rendering.ShadowSettings.DepthBias"/>.
    /// </remarks>
    public bool CastsShadow;

    /// <summary>
    /// Whether <see cref="Transform.WorldPosition"/> is interpreted as world (camera-transformed) or viewport
    /// pixels (+Y down, locked to the camera's virtual viewport / HUD). Defaults to
    /// <see cref="CoordinateSpace.WorldSpace"/>.
    /// </summary>
    public CoordinateSpace Space;

    /// <summary>Struct field initializers require an explicit constructor; <see cref="Alpha"/> defaults to 1.</summary>
    public Sprite()
    {
        this = default;
        Alpha = 1f;
        // Match <see cref="DefaultWhiteUnlit"/>: MaxValue means no emissive map. Default(uint)=0 collides with a valid texture slot.
        EmissiveTextureId = TextureId.MaxValue;
    }

    /// <summary>Convenience preset: white quad, default layer, fully opaque, no emissive.</summary>
    public static Sprite DefaultWhiteUnlit(TextureId whiteTextureId, TextureId normalTextureId, Vector2D<float> halfExtents)
    {
        Sprite s = default;
        s.HalfExtents = halfExtents;
        s.AlbedoTextureId = whiteTextureId;
        s.NormalTextureId = normalTextureId;
        s.EmissiveTextureId = TextureId.MaxValue;
        s.Layer = (int)SpriteLayer.World;
        s.SortKey = 0f;
        s.ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f);
        s.Alpha = 1f;
        s.EmissiveTint = default;
        s.EmissiveIntensity = 0f;
        s.DepthHint = 0f;
        // Same interpretation as TryBuildSpriteInstance: all zeros mean full UV; explicit (0,0,1,1) avoids "default" ambiguity.
        s.UvRect = new Vector4D<float>(0f, 0f, 1f, 1f);
        s.Transparent = false;
        s.Visible = true;
        s.Space = CoordinateSpace.WorldSpace;
        return s;
    }
}
