using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo;

/// <summary>Stationary warm accent light entity—tag keeps singleton lookups readable in lighting/post tutorials.</summary>
public struct HdrWarmPointTag : IComponent
{
}

/// <summary>Marks the player-follow point light child entity (<see cref="Transform.Parent"/> set to the player).</summary>
public struct HdrPlayerPointTag : IComponent
{
}

/// <summary>
/// Marks the fullscreen bloom <see cref="PostProcessVolumeSource"/> row adjusted by <see cref="HdrPostVolumeFillSystem"/>.
/// </summary>
public struct HdrBloomVolumeTag : IComponent
{
}
