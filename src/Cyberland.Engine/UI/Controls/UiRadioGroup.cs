namespace Cyberland.Engine.UI.Controls;

/// <summary>
/// Managed exclusive selection for <see cref="UiRadioButton"/> peers.
/// </summary>
public sealed class UiRadioGroup
{
    private readonly List<UiRadioButton> _buttons = new();

    /// <summary>Currently selected option id, if any.</summary>
    public string? SelectedOptionId { get; private set; }

    /// <summary>Raised when <see cref="SelectedOptionId"/> changes.</summary>
    public event EventHandler<string?>? SelectionChanged;

    /// <summary>Registers a button created for this group.</summary>
    public void Register(UiRadioButton button)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (!_buttons.Contains(button))
            _buttons.Add(button);
    }

    /// <summary>Selects one option and refreshes peer visuals.</summary>
    public void Select(string optionId)
    {
        if (SelectedOptionId == optionId)
            return;

        SelectedOptionId = optionId;
        SelectionChanged?.Invoke(this, optionId);
        RefreshAll();
    }

    internal void RefreshAll()
    {
        foreach (var b in _buttons)
            b.SyncVisual();
    }
}
