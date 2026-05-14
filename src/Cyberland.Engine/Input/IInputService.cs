using System.Collections.Generic;
using System.Numerics;
using Cyberland.Engine.Scene;
using Silk.NET.Input;

namespace Cyberland.Engine.Input;

/// <summary>
/// Frame-stable input service for action/axis queries, raw controls, and mouse movement.
/// </summary>
/// <remarks>
/// Call <see cref="BeginFrame"/> once per presented frame on the window thread before ECS updates run.
/// Query methods are then stable for the rest of that frame.
/// </remarks>
/// <remarks>
/// Input semantics split into three categories:
/// <list type="bullet">
/// <item><description><b>Level reads</b> (<see cref="IsDown"/>, <see cref="ReadAxis"/>, mouse position/delta properties) are frame snapshots and may be read in early/fixed/late without consuming state.</description></item>
/// <item><description><b>Frame gameplay commands</b> (<see cref="FrameGameplayCommands"/> and <see cref="InputGameplayCommandExtensions"/>) list logical press/release edges for the current render tick; the collection is stable across early/fixed/late until the next <see cref="BeginFrame"/>.</description></item>
/// <item><description><b>Event/delta reads</b> (<see cref="ConsumePressed"/>, <see cref="ConsumeReleased"/>, <see cref="ConsumeAxisDelta"/>) buffer counts/deltas across frames until consumed — useful when fixed update may not run on the same render tick as the edge.</description></item>
/// </list>
/// The stock <see cref="SilkInputService"/> merges keyboard <c>KeyDown</c> pulses into the sampled state so brief taps are not
/// lost when ECS runs at present rate. Mouse button pulses are merged with poll state similarly so actions bound to
/// <c>mouse:*</c> controls stay reliable even for very short clicks.
/// <para>
/// Action ids are string keys intended for JSON-backed rebinding (see <see cref="InputBindings"/> and
/// <see cref="InputControl.TryParse(string, out InputControl)"/>). Mods should query gameplay intent through those ids
/// and keep physical controls as defaults.
/// </para>
/// </remarks>
public interface IInputService
{
    /// <summary>Mutable action/axis bindings used by this service.</summary>
    InputBindings Bindings { get; }

    /// <summary>Current mouse position in swapchain/window pixel coordinates.</summary>
    Vector2 MousePosition { get; }

    /// <summary>Current mouse position in swapchain/window pixel coordinates (+Y down).</summary>
    Vector2 MousePositionScreen { get; }

    /// <summary>Current mouse position in world coordinates (+Y up).</summary>
    Vector2 MousePositionWorld { get; }

    /// <summary>Mouse position delta since the previous <see cref="BeginFrame"/>.</summary>
    Vector2 MouseDelta { get; }

    /// <summary>
    /// Gets current mouse position in the requested <paramref name="space"/> (defaults to virtual viewport space).
    /// </summary>
    Vector2 GetMousePosition(CoordinateSpace space = CoordinateSpace.ViewportSpace);

    /// <summary>
    /// Gets mouse movement delta since the previous <see cref="BeginFrame"/> in the requested <paramref name="space"/> (defaults to virtual viewport space).
    /// </summary>
    Vector2 GetMouseDelta(CoordinateSpace space = CoordinateSpace.ViewportSpace);

    /// <summary>Aggregated wheel delta since the previous <see cref="BeginFrame"/>.</summary>
    Vector2 MouseWheelDelta { get; }

    /// <summary>
    /// True when <paramref name="button"/> is considered held during this frame snapshot.
    /// </summary>
    /// <remarks>
    /// This includes sampled poll state and same-frame pulse latches captured before <see cref="BeginFrame"/>.
    /// </remarks>
    bool MouseButton(MouseButton button);

    /// <summary>
    /// True when <paramref name="button"/> has a press edge this frame.
    /// </summary>
    /// <remarks>
    /// Non-consuming frame-edge helper. For fixed-step intent handling across zero-substep render ticks, prefer
    /// <see cref="ConsumeMouseButtonPressed"/>.
    /// </remarks>
    bool MouseButtonDown(MouseButton button);

    /// <summary>
    /// True when <paramref name="button"/> has a release edge this frame.
    /// </summary>
    /// <remarks>
    /// Non-consuming frame-edge helper. For fixed-step intent handling across zero-substep render ticks, prefer
    /// <see cref="ConsumeMouseButtonReleased"/>.
    /// </remarks>
    bool MouseButtonUp(MouseButton button);

    /// <summary>
    /// Consumes one buffered mouse press edge for <paramref name="button"/>.
    /// </summary>
    bool ConsumeMouseButtonPressed(MouseButton button);

    /// <summary>
    /// Consumes one buffered mouse release edge for <paramref name="button"/>.
    /// </summary>
    bool ConsumeMouseButtonReleased(MouseButton button);

    /// <summary>Samples physical devices and recomputes per-frame action/axis state.</summary>
    void BeginFrame();

    /// <summary>
    /// Press and release edges sampled during the most recent <see cref="BeginFrame"/>, in binding iteration order.
    /// </summary>
    /// <remarks>
    /// Parallel to pending counters used by <see cref="ConsumePressed"/> / <see cref="ConsumeReleased"/>: each edge here also
    /// increments the corresponding pending count. Demos can read this list (or extension helpers) from any ECS phase in the
    /// same frame without ordering surprises relative to <see cref="WasPressed"/>.
    /// </remarks>
    IReadOnlyList<InputGameplayCommand> FrameGameplayCommands { get; }

    /// <summary>True if any binding for <paramref name="actionId"/> is currently active.</summary>
    bool IsDown(string actionId);

    /// <summary>True only on the frame where <paramref name="actionId"/> transitions from up to down.</summary>
    /// <remarks>
    /// This is a frame-edge snapshot helper (non-consuming). For fixed-phase one-shot intent handling, prefer
    /// <see cref="ConsumePressed"/> so zero-fixed-substep frames cannot drop the event.
    /// </remarks>
    bool WasPressed(string actionId);

    /// <summary>True only on the frame where <paramref name="actionId"/> transitions from down to up.</summary>
    /// <remarks>
    /// This is a frame-edge snapshot helper (non-consuming). For fixed-phase one-shot intent handling, prefer
    /// <see cref="ConsumeReleased"/> so zero-fixed-substep frames cannot drop the event.
    /// </remarks>
    bool WasReleased(string actionId);

    /// <summary>Reads the composed axis value for <paramref name="axisId"/> in [-1, 1].</summary>
    /// <remarks>
    /// Returns the frame-stable level value. For event-style axes (for example wheel/delta bindings), use
    /// <see cref="ConsumeAxisDelta"/> when your simulation should apply each delta once.
    /// </remarks>
    float ReadAxis(string axisId);

    /// <summary>
    /// Consumes one buffered press event for <paramref name="actionId"/>. Returns <see langword="true"/> when an unconsumed
    /// press exists.
    /// </summary>
    bool ConsumePressed(string actionId);

    /// <summary>
    /// Consumes one buffered release event for <paramref name="actionId"/>. Returns <see langword="true"/> when an
    /// unconsumed release exists.
    /// </summary>
    bool ConsumeReleased(string actionId);

    /// <summary>
    /// Returns and clears buffered axis delta for <paramref name="axisId"/>.
    /// </summary>
    /// <remarks>
    /// Delta buffering currently applies to bindings backed by mouse delta/wheel controls. Position-like axis bindings
    /// return 0 here and should be read via <see cref="ReadAxis"/>.
    /// </remarks>
    float ConsumeAxisDelta(string axisId);

    /// <summary>Reads whether a raw physical <paramref name="control"/> is active in the current frame.</summary>
    bool IsControlDown(InputControl control);

    /// <summary>Reads the scalar value of a raw physical <paramref name="control"/>.</summary>
    float ReadControlValue(InputControl control);
}
