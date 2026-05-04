using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo;

/// <summary>Singleton gameplay sprite driven by <see cref="InputSystem"/> / <see cref="IntegrateSystem"/>.</summary>
public struct PlayerTag : IComponent;

/// <summary>Backdrop quad stretched via <see cref="ViewportAnchor2D"/>.</summary>
public struct BackgroundTag : IComponent;

/// <summary>Decorative column sprite used only for HDR/emissive teaching—not queried by gameplay systems.</summary>
public struct NeonStripTag : IComponent;

/// <summary>Localized HUD title row (<c>demo.hdr.title</c>).</summary>
public struct HudTitleTag : IComponent;

/// <summary>Localized HUD hint row (<c>demo.hdr.hint</c>).</summary>
public struct HudHintTag : IComponent;

/// <summary>FPS counter updated by <see cref="FpsDisplaySystem"/>.</summary>
public struct HudFpsTag : IComponent;
