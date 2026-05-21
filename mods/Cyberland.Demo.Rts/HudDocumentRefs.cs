using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.Rts;

public sealed class HudDocumentRefs
{
    public required EntityId RootEntity { get; init; }
    public required UiTextBlock Fps { get; init; }
}
