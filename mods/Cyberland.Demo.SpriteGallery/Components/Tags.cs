using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.SpriteGallery.Components;

/// <summary>Singleton row for HUD refresh (FPS + locale hint).</summary>
public struct GalleryState : IComponent;

/// <summary>Marks the entity that owns the retained HUD document.</summary>
public struct HudRootTag : IComponent;
