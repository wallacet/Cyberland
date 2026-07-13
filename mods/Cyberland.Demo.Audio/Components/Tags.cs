using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Audio.Components;

/// <summary>Player avatar used for listener override and footsteps.</summary>
public struct PlayerTag : IComponent { }

/// <summary>HUD root for status text.</summary>
public struct HudRootTag : IComponent { }

/// <summary>Street ambient environment volume marker.</summary>
public struct StreetEnvTag : IComponent { }

/// <summary>Club ambient environment volume marker.</summary>
public struct ClubEnvTag : IComponent { }
