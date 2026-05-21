using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.WhackAMole;

public sealed class HudDocumentRefs
{
    public required UiTextBlock Score { get; init; }
    public required UiTextBlock Timer { get; init; }
    public required UiTextBlock Overlay { get; init; }
}
