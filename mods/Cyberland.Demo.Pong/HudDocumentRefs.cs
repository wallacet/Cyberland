using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.Pong;

public sealed class HudDocumentRefs
{
    public required UiTextBlock Title { get; init; }
    public required UiTextBlock GameOver { get; init; }
    public required UiTextBlock Hint { get; init; }
    public required UiTextBlock ScoreYou { get; init; }
    public required UiTextBlock ScorePlayer { get; init; }
    public required UiTextBlock ScoreCpuLabel { get; init; }
    public required UiTextBlock ScoreCpu { get; init; }
    public required UiTextBlock Fps { get; init; }
}
