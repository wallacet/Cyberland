using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo;

/// <summary>Marks the controllable demo sprite entity.</summary>
public struct PlayerTag : IComponent;

/// <summary>Marks the full-screen background sprite entity.</summary>
public struct BackgroundTag : IComponent;

/// <summary>Marks the decorative neon strip sprite entity.</summary>
public struct NeonStripTag : IComponent;

/// <summary>Marks the HUD title text entity.</summary>
public struct HudTitleTag : IComponent;

/// <summary>Marks the HUD hint text entity.</summary>
public struct HudHintTag : IComponent;
