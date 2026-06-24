using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.SpriteGallery;

/// <summary>Retained HUD nodes resolved at scene setup; static copy lives in JSON via <c>locKey</c>.</summary>
public sealed class HudDocumentRefs
{
    public required EntityId RootEntity { get; init; }
    public required UiTextBlock Fps { get; init; }
}
