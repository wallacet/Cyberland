using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>ECS ambient fill; submitted by <see cref="Systems.AmbientLightSystem"/>.</summary>
/// <remarks>
/// Global scene fill only; no <see cref="RequiresComponentAttribute{TRequired}"/> for <see cref="Transform"/>.
/// </remarks>
public struct AmbientLightSource : IComponent
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>Linear RGB tint.</summary>
    public Vector3D<float> Color;

    /// <summary>Overall intensity scaler.</summary>
    public float Intensity;
}

/// <summary>ECS directional light; submitted by <see cref="Systems.DirectionalLightSystem"/>.</summary>
/// <remarks>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
public struct DirectionalLightSource : IComponent
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>Linear RGB tint.</summary>
    public Vector3D<float> Color;

    /// <summary>Brightness scaler.</summary>
    public float Intensity;

    /// <summary>Reserved.</summary>
    public bool CastsShadow;
}

/// <summary>ECS spot light; submitted by <see cref="Systems.SpotLightSystem"/>.</summary>
/// <remarks>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
public struct SpotLightSource : IComponent
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>Radial reach of the cone in world units before transform scale is applied.</summary>
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

/// <summary>ECS point light; submitted by <see cref="Systems.PointLightSystem"/>.</summary>
/// <remarks>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
public struct PointLightSource : IComponent
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>World-space radius before transform scale is applied.</summary>
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
