using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.MouseChase;

/// <summary>
/// Retained HUD nodes resolved at scene setup so runtime systems can update copy without tree traversal.
/// </summary>
public sealed class HudDocumentRefs
{
    public required EntityId RootEntity { get; init; }
    public required UiTextBlock Title { get; init; }
    public required UiTextBlock Detail { get; init; }
    public required UiTextBlock Status { get; init; }
    public required UiTextBlock Fps { get; init; }
}
