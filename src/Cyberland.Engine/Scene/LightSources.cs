using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>ECS ambient fill; submitted by <see cref="Systems.AmbientLightSystem"/>.</summary>
/// <remarks>
/// <para>Global scene fill only; no <see cref="RequiresComponentAttribute{TRequired}"/> for <see cref="Transform"/>.</para>
/// <para>Multiple active rows sum linearly (<c>color * intensity</c> per row); the renderer accumulates all submitted ambient rows into a single fill term.</para>
/// </remarks>
public struct AmbientLightSource : IComponent
{
    /// <summary>When false, this row is ignored for submission. Scene JSON defaults <c>active</c> to <c>true</c>;
    /// programmatic <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> zero-initializes (false) so callers must set it explicitly.</summary>
    public bool Active;

    /// <summary>Linear RGB tint.</summary>
    public Vector3D<float> Color;

    /// <summary>Overall intensity scaler.</summary>
    public float Intensity;
}

/// <summary>ECS directional light; submitted by <see cref="Systems.DirectionalLightSystem"/>.</summary>
/// <remarks>
/// <para>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </para>
/// <para>
/// The directional lighting model uses N·L (normal dot light direction) with a synthetic Z component of 0.8 for
/// the 3D light vector (<c>normalize(vec3(dir.xy, 0.8))</c>). This produces roughly 38° default elevation so
/// surfaces with different normals respond visually without a full 3D light rig. Spot and point lights use a
/// simplified model without normals.
/// </para>
/// <para>Only light type that uses G-buffer normals for N·L shading with a synthetic Z = 0.8.</para>
/// </remarks>
[RequiresComponent<Transform>]
public struct DirectionalLightSource : IComponent
{
    /// <summary>When false, this row is ignored for submission. Scene JSON defaults <c>active</c> to <c>true</c>;
    /// programmatic <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> zero-initializes (false) so callers must set it explicitly.</summary>
    public bool Active;

    /// <summary>Linear RGB tint.</summary>
    public Vector3D<float> Color;

    /// <summary>Brightness scaler.</summary>
    public float Intensity;

    /// <summary>When false, the light still contributes but skips SDF cone-trace shadow sampling.</summary>
    public bool CastsShadow;

    /// <summary>Defaults <see cref="CastsShadow"/> to true for new lights.</summary>
    public DirectionalLightSource() => CastsShadow = true;
}

/// <summary>ECS spot light; submitted by <see cref="Systems.SpotLightSystem"/>.</summary>
/// <remarks>
/// <para>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </para>
/// <para>Radius scales by <c>max(|scaleX|, |scaleY|)</c> from the owning <see cref="Transform"/>. Uniform scale on the light entity is recommended.</para>
/// <para>
/// The spot lighting model uses cone + world-space distance attenuation (no normals). Inner/outer cone
/// half-angles control the angular falloff.
/// </para>
/// <para>Uses radial distance and cone angle falloff; does not interact with G-buffer normals.</para>
/// </remarks>
[RequiresComponent<Transform>]
public struct SpotLightSource : IComponent
{
    /// <summary>When false, this row is ignored for submission. Scene JSON defaults <c>active</c> to <c>true</c>;
    /// programmatic <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> zero-initializes (false) so callers must set it explicitly.</summary>
    public bool Active;

    /// <summary>Radial reach of the cone in world units before transform scale is applied.</summary>
    public float Radius;

    /// <summary>Half-angle from the spotlight axis where intensity is full (radians).</summary>
    public float InnerConeRadians;

    /// <summary>Half-angle from the spotlight axis at the outer falloff boundary (radians).</summary>
    public float OuterConeRadians;

    /// <summary>Linear RGB color.</summary>
    public Vector3D<float> Color;

    /// <summary>Brightness scaler.</summary>
    public float Intensity;

    /// <summary>
    /// Radial falloff exponent (typical 1.5–3). When &lt;= 0, the CPU upload substitutes 2.0.
    /// The shader applies <c>pow(max(1-d/r,0), exponent)</c> for smooth radial decay.
    /// </summary>
    public float FalloffExponent;

    /// <summary>When false, the light still contributes but skips SDF cone-trace shadow sampling.</summary>
    public bool CastsShadow;

    /// <summary>Defaults <see cref="CastsShadow"/> to true for new lights.</summary>
    public SpotLightSource() => CastsShadow = true;
}

/// <summary>ECS point light; submitted by <see cref="Systems.PointLightSystem"/>.</summary>
/// <remarks>
/// <para>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </para>
/// <para>Radius scales by <c>max(|scaleX|, |scaleY|)</c> from the owning <see cref="Transform"/>. Uniform scale on the light entity is recommended.</para>
/// <para>
/// The point lighting model uses radial falloff (no normals). See <see cref="FalloffExponent"/> for attenuation control.
/// </para>
/// <para>Uses radial distance falloff only; does not interact with G-buffer normals.</para>
/// </remarks>
[RequiresComponent<Transform>]
public struct PointLightSource : IComponent
{
    /// <summary>When false, this row is ignored for submission. Scene JSON defaults <c>active</c> to <c>true</c>;
    /// programmatic <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> zero-initializes (false) so callers must set it explicitly.</summary>
    public bool Active;

    /// <summary>World-space radius before transform scale is applied.</summary>
    public float Radius;

    /// <summary>Linear RGB tint.</summary>
    public Vector3D<float> Color;

    /// <summary>Brightness scaler (artist-tunable).</summary>
    public float Intensity;

    /// <summary>
    /// Radial falloff exponent (typical 1.5–3). When &lt;= 0, the CPU upload substitutes 2.0.
    /// The shader applies <c>max(exponent, 0.1)</c> as a floor to avoid degenerate fragment math.
    /// </summary>
    public float FalloffExponent;

    /// <summary>When false, the light still contributes but skips SDF cone-trace shadow sampling.</summary>
    public bool CastsShadow;

    /// <summary>Defaults <see cref="CastsShadow"/> to true for new lights.</summary>
    public PointLightSource() => CastsShadow = true;
}
