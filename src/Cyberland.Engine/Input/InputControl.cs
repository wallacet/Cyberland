using Silk.NET.Input;

namespace Cyberland.Engine.Input;

/// <summary>
/// A physical input control used by action/axis bindings.
/// </summary>
public readonly record struct InputControl
{
    private InputControl(InputControlKind kind, Key key, MouseButton mouseButton, MouseAxis mouseAxis)
    {
        Kind = kind;
        Key = key;
        MouseButton = mouseButton;
        MouseAxis = mouseAxis;
    }

    /// <summary>Kind discriminator for this control.</summary>
    public InputControlKind Kind { get; }

    /// <summary>Keyboard key payload when <see cref="Kind"/> is <see cref="InputControlKind.KeyboardKey"/>.</summary>
    public Key Key { get; }

    /// <summary>Mouse button payload when <see cref="Kind"/> is <see cref="InputControlKind.MouseButton"/>.</summary>
    public MouseButton MouseButton { get; }

    /// <summary>Mouse axis payload when <see cref="Kind"/> is <see cref="InputControlKind.MouseAxis"/>.</summary>
    public MouseAxis MouseAxis { get; }

    /// <summary>Create a keyboard key control.</summary>
    public static InputControl Keyboard(Key key) => new(InputControlKind.KeyboardKey, key, default, default);

    /// <summary>Create a mouse button control.</summary>
    public static InputControl MouseButtonControl(MouseButton button) => new(InputControlKind.MouseButton, default, button, default);

    /// <summary>Create a mouse axis control.</summary>
    public static InputControl MouseAxisControl(MouseAxis axis) => new(InputControlKind.MouseAxis, default, default, axis);

    /// <summary>
    /// Parses a persisted control string such as <c>keyboard:W</c>, <c>mouse:Left</c>, or <c>mouseAxis:DeltaX</c>.
    /// </summary>
    public static bool TryParse(string value, out InputControl control)
    {
        control = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var split = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (split.Length != 2)
            return false;

        if (split[0].Equals("keyboard", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<Key>(split[1], true, out var key))
        {
            control = Keyboard(key);
            return true;
        }

        if (split[0].Equals("mouse", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<MouseButton>(split[1], true, out var button))
        {
            control = MouseButtonControl(button);
            return true;
        }

        if (split[0].Equals("mouseAxis", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<MouseAxis>(split[1], true, out var axis))
        {
            control = MouseAxisControl(axis);
            return true;
        }

        return false;
    }

    /// <summary>Converts this control to a stable persisted string.</summary>
    public string ToPersistedString() =>
        Kind switch
        {
            InputControlKind.KeyboardKey => $"keyboard:{Key}",
            InputControlKind.MouseButton => $"mouse:{MouseButton}",
            InputControlKind.MouseAxis => $"mouseAxis:{MouseAxis}",
            _ => throw new InvalidOperationException($"Unsupported control kind '{Kind}'.")
        };
}

/// <summary>Control categories for persisted and runtime binding dispatch.</summary>
public enum InputControlKind
{
    /// <summary>A keyboard key.</summary>
    KeyboardKey = 0,
    /// <summary>A mouse button.</summary>
    MouseButton = 1,
    /// <summary>A mouse axis (position, delta, or wheel).</summary>
    MouseAxis = 2
}

/// <summary>Mouse axis sources used for axis-style bindings and raw reads.</summary>
public enum MouseAxis
{
    /// <summary>Absolute X cursor position in window pixels.</summary>
    PositionX = 0,
    /// <summary>Absolute Y cursor position in window pixels.</summary>
    PositionY = 1,
    /// <summary>Per-frame cursor delta on X in window pixels.</summary>
    DeltaX = 2,
    /// <summary>Per-frame cursor delta on Y in window pixels.</summary>
    DeltaY = 3,
    /// <summary>Per-frame wheel delta on X.</summary>
    WheelX = 4,
    /// <summary>Per-frame wheel delta on Y.</summary>
    WheelY = 5
}
