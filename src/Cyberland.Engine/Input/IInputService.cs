using System.Numerics;

namespace Cyberland.Engine.Input;

/// <summary>
/// Frame-stable input service for action/axis queries, raw controls, and mouse movement.
/// </summary>
/// <remarks>
/// Call <see cref="BeginFrame"/> once per presented frame on the window thread before ECS updates run.
/// Query methods are then stable for the rest of that frame. The stock <see cref="SilkInputService"/> also merges
/// keyboard <c>KeyDown</c> edges into that snapshot so brief taps are not lost when the game loop runs at present
/// rate rather than input poll rate.
/// </remarks>
public interface IInputService
{
    /// <summary>Mutable action/axis bindings used by this service.</summary>
    InputBindings Bindings { get; }

    /// <summary>Current mouse position in window pixel coordinates.</summary>
    Vector2 MousePosition { get; }

    /// <summary>Mouse position delta since the previous <see cref="BeginFrame"/>.</summary>
    Vector2 MouseDelta { get; }

    /// <summary>Aggregated wheel delta since the previous <see cref="BeginFrame"/>.</summary>
    Vector2 MouseWheelDelta { get; }

    /// <summary>Samples physical devices and recomputes per-frame action/axis state.</summary>
    void BeginFrame();

    /// <summary>True if any binding for <paramref name="actionId"/> is currently active.</summary>
    bool IsDown(string actionId);

    /// <summary>True only on the frame where <paramref name="actionId"/> transitions from up to down.</summary>
    bool WasPressed(string actionId);

    /// <summary>True only on the frame where <paramref name="actionId"/> transitions from down to up.</summary>
    bool WasReleased(string actionId);

    /// <summary>Reads the composed axis value for <paramref name="axisId"/> in [-1, 1].</summary>
    float ReadAxis(string axisId);

    /// <summary>Reads whether a raw physical <paramref name="control"/> is active in the current frame.</summary>
    bool IsControlDown(InputControl control);

    /// <summary>Reads the scalar value of a raw physical <paramref name="control"/>.</summary>
    float ReadControlValue(InputControl control);
}
