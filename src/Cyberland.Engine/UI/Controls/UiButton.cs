using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Controls;

/// <summary>
/// Simple clickable panel with distinct normal/pressed fills and a managed <see cref="Clicked"/> event.
/// </summary>
public class UiButton : UiPanel
{
    /// <summary>Raised on the frame’s primary release when the press started on this button and the release hits it.</summary>
    public event EventHandler? Clicked;

    private bool _pressed;

    /// <summary>Fill when the button is not pressed.</summary>
    public Vector4D<float> NormalBackground { get; set; } = new(0.22f, 0.22f, 0.28f, 1f);

    /// <summary>Fill while the pointer-held interaction is active.</summary>
    public Vector4D<float> PressedBackground { get; set; } = new(0.34f, 0.34f, 0.42f, 1f);

    /// <summary>Creates a button with HUD defaults.</summary>
    public UiButton()
    {
        Interactable = true;
        BackgroundColor = NormalBackground;
    }

    internal void NotifyPressStarted()
    {
        if (!Interactable)
            return;

        _pressed = true;
        BackgroundColor = PressedBackground;
    }

    internal void NotifyPressEnded(bool releasedOnSelf)
    {
        if (_pressed && releasedOnSelf && Interactable)
            Clicked?.Invoke(this, EventArgs.Empty);

        _pressed = false;
        BackgroundColor = NormalBackground;
    }

    internal void NotifyCancelPress()
    {
        _pressed = false;
        BackgroundColor = NormalBackground;
    }
}
