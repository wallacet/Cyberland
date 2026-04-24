using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo;

/// <summary>Singleton HDR demo: stationary warm point light row.</summary>
public struct HdrWarmPointTag : IComponent
{
}

/// <summary>Singleton HDR demo: point light that follows the player.</summary>
public struct HdrPlayerPointTag : IComponent
{
}

/// <summary>Singleton HDR demo: fullscreen post-process volume driven by player X.</summary>
public struct HdrBloomVolumeTag : IComponent
{
}
