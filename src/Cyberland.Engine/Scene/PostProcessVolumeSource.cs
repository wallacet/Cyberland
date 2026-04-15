using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene;

/// <summary>
/// ECS post-process volume; gathered by <see cref="Systems.PostProcessVolumeSystem"/>.
/// </summary>
public struct PostProcessVolumeSource
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>GPU payload (priority, AABB, overrides).</summary>
    public PostProcessVolume Volume;
}
