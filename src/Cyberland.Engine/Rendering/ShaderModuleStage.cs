namespace Cyberland.Engine.Rendering;

/// <summary>
/// Shader stage identifier used by mod-facing shader module creation APIs.
/// </summary>
public enum ShaderModuleStage
{
    /// <summary>Vertex stage.</summary>
    Vertex = 0,

    /// <summary>Fragment stage.</summary>
    Fragment = 1,

    /// <summary>Compute stage.</summary>
    Compute = 2
}
