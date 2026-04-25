using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene;

/// <summary>
/// ECS source for global post-process settings. Highest priority active source wins each frame.
/// </summary>
[ExcludeFromCodeCoverage]
public struct GlobalPostProcessSource : IComponent
{
    /// <summary>When false, this source is ignored for the current frame.</summary>
    public bool Active;
    /// <summary>Higher priority wins; ties resolve by first-seen row order.</summary>
    public int Priority;
    /// <summary>Global post-process settings contributed by this source.</summary>
    public GlobalPostProcessSettings Settings;
}
