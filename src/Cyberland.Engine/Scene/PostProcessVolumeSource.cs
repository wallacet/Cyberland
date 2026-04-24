using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene;

/// <summary>
/// ECS post-process volume row; <see cref="Systems.PostProcessVolumeSystem"/> submits <see cref="Volume"/>
/// plus the entity's world transform.
/// </summary>
[RequiresComponent<Transform>]
public struct PostProcessVolumeSource : IComponent
{
    /// <summary>When false, this row is ignored for submission.</summary>
    public bool Active;

    /// <summary>Volume authoring settings (local extents + priority + overrides).</summary>
    public PostProcessVolume Volume;
}
