using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.MouseChase.Components;

public struct PlayerTag : IComponent;
public struct CollectibleTag : IComponent;
public struct EnterZoneTag : IComponent;
public struct StayZoneTag : IComponent;
public struct ExitZoneTag : IComponent;
public struct GateZoneTag : IComponent;

/// <summary>Marks the retained HUD document entity for <see cref="Systems.HudUiSystem"/> startup lookup.</summary>
public struct HudRootTag : IComponent;
