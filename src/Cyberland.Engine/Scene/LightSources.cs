using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene;

/// <summary>ECS ambient fill; gathered by <see cref="Systems.LightingSystem"/>.</summary>
public struct AmbientLightSource
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>GPU payload.</summary>
    public AmbientLight Light;
}

/// <summary>ECS directional light; gathered by <see cref="Systems.LightingSystem"/>.</summary>
public struct DirectionalLightSource
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>GPU payload.</summary>
    public DirectionalLight Light;
}

/// <summary>ECS spot light; gathered by <see cref="Systems.LightingSystem"/>.</summary>
public struct SpotLightSource
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>GPU payload.</summary>
    public SpotLight Light;
}

/// <summary>ECS point light; gathered by <see cref="Systems.LightingSystem"/>.</summary>
public struct PointLightSource
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>GPU payload.</summary>
    public PointLight Light;
}
