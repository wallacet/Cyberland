using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo;

/// <summary>Retained HUD elements resolved after <c>Scenes/hdr.json</c> spawn.</summary>
public sealed class HudDocumentRefs
{
    public required EntityId RootEntity { get; init; }
    public required UiTextBlock Fps { get; init; }
}
