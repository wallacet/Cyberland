using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Engine.UI.Controls;

/// <summary>
/// Thin container preset around <see cref="UiTextBlock"/> for captions (stretch child to parent slot).
/// </summary>
public sealed class UiLabel : UiPanel
{
    /// <summary>Wrapped text element.</summary>
    public UiTextBlock Text { get; } = new();

    /// <summary>Creates a label with a stretched text child.</summary>
    public UiLabel()
    {
        Text.VerticalAlignment = UiTextVerticalAlignment.CenterInk;
        AddChild(Text);
        UiLayoutPresets.StretchAll(Text);
    }
}
