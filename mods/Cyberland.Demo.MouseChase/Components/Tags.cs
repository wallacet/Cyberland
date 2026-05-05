using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.MouseChase.Components;

public struct PlayerTag : IComponent;
public struct CollectibleTag : IComponent;
public struct EnterZoneTag : IComponent;
public struct StayZoneTag : IComponent;
public struct ExitZoneTag : IComponent;
public struct GateZoneTag : IComponent;

/// <summary>Marks the FPS BitmapText row so <see cref="Systems.FpsOverlaySystem"/> can register as a singleton.</summary>
public struct FpsHudTag : IComponent;

/// <summary>Marks tutorial HUD lines resolved by <see cref="Systems.TutorialHudSystem"/>.</summary>
public struct TutorialTitleHudTag : IComponent;
public struct TutorialDetailHudTag : IComponent;
public struct TutorialStatusHudTag : IComponent;
