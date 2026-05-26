using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo;

/// <summary>Singleton gameplay sprite driven by <see cref="InputSystem"/> / <see cref="IntegrateSystem"/>.</summary>
public struct PlayerTag : IComponent;

/// <summary>Backdrop quad stretched via <see cref="ViewportAnchor2D"/>.</summary>
public struct BackgroundTag : IComponent;

/// <summary>Decorative column sprite used only for HDR/emissive teaching—not queried by gameplay systems.</summary>
public struct NeonStripTag : IComponent;

/// <summary>Wide floor quad that receives shadows from scene lights (no attached light).</summary>
public struct ShadowFloorTag : IComponent;

/// <summary>Retained HUD root (<c>Content/Ui/hdr_hud.json</c> via <c>ui-document-root</c>).</summary>
public struct HudRootTag : IComponent;
