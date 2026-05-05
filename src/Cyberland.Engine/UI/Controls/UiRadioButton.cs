using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Controls;

/// <summary>
/// Small selectable tile participating in a <see cref="UiRadioGroup"/>.
/// </summary>
public class UiRadioButton : UiPanel
{
    /// <summary>Owning group.</summary>
    public UiRadioGroup Group { get; }

    /// <summary>Opaque id passed to <see cref="UiRadioGroup.Select"/>.</summary>
    public string OptionId { get; }

    /// <summary>Fill when not selected.</summary>
    public Vector4D<float> NormalTint { get; set; } = new(0.16f, 0.16f, 0.2f, 1f);

    /// <summary>Fill when selected.</summary>
    public Vector4D<float> SelectedTint { get; set; } = new(0.28f, 0.42f, 0.55f, 1f);

    /// <summary>Creates a radio tile registered with <paramref name="group"/>.</summary>
    public UiRadioButton(UiRadioGroup group, string optionId)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(optionId);

        Group = group;
        OptionId = optionId;
        Interactable = true;
        group.Register(this);
        UiLayoutPresets.TopLeftFixed(this, 140f, 32f);
        SyncVisual();
    }

    internal void SyncVisual() =>
        BackgroundColor = Group.SelectedOptionId == OptionId ? SelectedTint : NormalTint;

    internal void SelectFromUiSystem()
    {
        if (!Interactable)
            return;

        Group.Select(OptionId);
    }
}
