using System.Collections.Generic;
using System.Numerics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Cyberland.Engine.Input;

/// <summary>
/// Silk.NET-backed implementation of <see cref="IInputService"/> used by the host window.
/// </summary>
/// <remarks>
/// ECS runs on the <strong>Render</strong> tick (see <see cref="GameApplication"/>), so <see cref="BeginFrame"/> samples
/// keyboard state at ~present rate. Short key taps can fall entirely between two polls if only
/// <see cref="IKeyboard.IsKeyPressed"/> is used — the key is down and released before the next snapshot.
/// We therefore latch <see cref="IKeyboard.KeyDown"/> edges into <see cref="_keyboardPulseDown"/> and OR them into
/// the same-frame poll so <see cref="WasPressed"/> and <see cref="IsDown"/> still observe the press.
/// Frame edges are also appended to <see cref="FrameGameplayCommands"/> each <see cref="BeginFrame"/> (parallel to the pending
/// counters backing <see cref="ConsumePressed"/> / <see cref="ConsumeReleased"/>). Demos can scan that list or use
/// <see cref="InputGameplayCommandExtensions"/> for consistent reads across early/fixed/late within the same tick.
/// Event/delta consumers should still use <see cref="ConsumePressed"/>, <see cref="ConsumeReleased"/>, and
/// <see cref="ConsumeAxisDelta"/> when intent must survive a render tick with zero fixed substeps unless gameplay latches it earlier.
/// </remarks>
public sealed class SilkInputService : IInputService, IDisposable
{
    private static readonly MouseButton[] TrackedMouseButtons = Enum.GetValues<MouseButton>();
    private readonly IInputContext _input;
    private readonly IRenderer? _renderer;
    private readonly GameHostServices? _host;
    private readonly Dictionary<string, bool> _actionDown = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _prevActionDown = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _axisValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pendingPressCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pendingReleaseCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<MouseButton, bool> _mouseButtonDown = new();
    private readonly Dictionary<MouseButton, bool> _prevMouseButtonDown = new();
    private readonly Dictionary<MouseButton, bool> _mouseButtonPressedThisFrame = new();
    private readonly Dictionary<MouseButton, bool> _mouseButtonReleasedThisFrame = new();
    private readonly Dictionary<MouseButton, int> _pendingMousePressCounts = new();
    private readonly Dictionary<MouseButton, int> _pendingMouseReleaseCounts = new();
    private readonly Dictionary<string, float> _pendingAxisDelta = new(StringComparer.Ordinal);
    private readonly object _keyboardPulseLock = new();
    private readonly HashSet<Key> _keyboardPulseDown = new();
    private readonly List<(IKeyboard Keyboard, Action<IKeyboard, Key, int> Handler)> _keyboardSubscriptions = new();
    private readonly object _mousePulseLock = new();
    private readonly HashSet<MouseButton> _mousePulseDown = new();
    private readonly HashSet<MouseButton> _mousePulseUp = new();
    private readonly List<(IMouse Mouse, Action<IMouse, MouseButton> Down, Action<IMouse, MouseButton> Up)> _mouseSubscriptions = new();
    private Vector2 _mousePosition;
    private Vector2 _mouseDelta;
    private Vector2 _mouseWheelDelta;
    private bool _hasMousePosition;
    private readonly List<InputGameplayCommand> _frameGameplayCommands = new();
    private bool _anyInputPressedThisFrame;

    /// <summary>Create a service backed by the provided Silk input context.</summary>
    /// <param name="input">Silk input context (keyboard/mouse).</param>
    /// <param name="renderer">Optional renderer for swapchain size and camera mapping fallbacks.</param>
    /// <param name="host">
    /// When non-null and <see cref="GameHostServices.CameraRuntimeState"/> is valid with a positive viewport,
    /// world-space mouse mapping matches the ECS-published camera that <see cref="Scene.Systems.SpriteRenderSystem"/>
    /// uses for frustum tests — avoiding divergence from <see cref="IRenderer.ActiveCameraView"/> tie-break or ordering.
    /// </param>
    public SilkInputService(IInputContext input, IRenderer? renderer = null, GameHostServices? host = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _renderer = renderer;
        _host = host;
        Bindings = new InputBindings();
        SubscribeExistingKeyboards();
        SubscribeExistingMice();
    }

    /// <inheritdoc />
    public InputBindings Bindings { get; }

    /// <inheritdoc />
    public IReadOnlyList<InputGameplayCommand> FrameGameplayCommands => _frameGameplayCommands;

    /// <summary>
    /// True when any keyboard key, mouse button, or bound action edge was pressed this frame.
    /// Useful for startup "press anything" prompts.
    /// </summary>
    public bool AnyInputPressedThisFrame => _anyInputPressedThisFrame;

    /// <inheritdoc />
    public Vector2 MousePosition => _mousePosition;

    /// <inheritdoc />
    public Vector2 MousePositionScreen => _mousePosition;

    /// <inheritdoc />
    public Vector2 MousePositionWorld => GetMousePosition(CoordinateSpace.WorldSpace);

    /// <inheritdoc />
    public Vector2 MouseDelta => _mouseDelta;

    /// <inheritdoc />
    public Vector2 GetMousePosition(CoordinateSpace space = CoordinateSpace.ViewportSpace) =>
        ConvertMousePosition(_mousePosition, space);

    /// <inheritdoc />
    public Vector2 GetMouseDelta(CoordinateSpace space = CoordinateSpace.ViewportSpace) =>
        ConvertMouseDelta(_mouseDelta, space);

    /// <inheritdoc />
    public Vector2 MouseWheelDelta => _mouseWheelDelta;

    /// <inheritdoc />
    public bool MouseButton(MouseButton button) => _mouseButtonDown.TryGetValue(button, out var down) && down;

    /// <inheritdoc />
    public bool MouseButtonDown(MouseButton button)
        => _mouseButtonPressedThisFrame.TryGetValue(button, out var pressed) && pressed;

    /// <inheritdoc />
    public bool MouseButtonUp(MouseButton button)
        => _mouseButtonReleasedThisFrame.TryGetValue(button, out var released) && released;

    /// <inheritdoc />
    public bool ConsumeMouseButtonPressed(MouseButton button) => ConsumeCounter(_pendingMousePressCounts, button);

    /// <inheritdoc />
    public bool ConsumeMouseButtonReleased(MouseButton button) => ConsumeCounter(_pendingMouseReleaseCounts, button);

    /// <inheritdoc />
    public void BeginFrame()
    {
        var keyboardPulseCount = 0;
        var mousePulseCount = 0;
        lock (_keyboardPulseLock)
            keyboardPulseCount = _keyboardPulseDown.Count;
        lock (_mousePulseLock)
            mousePulseCount = _mousePulseDown.Count;
        _anyInputPressedThisFrame = keyboardPulseCount > 0 || mousePulseCount > 0;

        _prevActionDown.Clear();
        foreach (var (id, isDown) in _actionDown)
            _prevActionDown[id] = isDown;
        _prevMouseButtonDown.Clear();
        foreach (var (button, isDown) in _mouseButtonDown)
            _prevMouseButtonDown[button] = isDown;

        _frameGameplayCommands.Clear();
        _mouseButtonPressedThisFrame.Clear();
        _mouseButtonReleasedThisFrame.Clear();

        _actionDown.Clear();
        _axisValues.Clear();
        UpdateMouseSnapshot();
        SampleMouseButtons();

        foreach (var actionId in Bindings.ActionIds)
        {
            if (!Bindings.TryGetBindingsList(actionId, out var bindings) || bindings is null || bindings.Count == 0)
                continue;

            bool down = false;
            float axis = 0f;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                down |= IsControlDown(binding.Control);
                axis += ReadControlValue(binding.Control) * binding.Scale;
            }

            _actionDown[actionId] = down;
            _axisValues[actionId] = Math.Clamp(axis, -1f, 1f);

            var before = _prevActionDown.TryGetValue(actionId, out var prevDown) && prevDown;
            if (down && !before)
            {
                IncrementCounter(_pendingPressCounts, actionId);
                _frameGameplayCommands.Add(new InputGameplayCommand(InputGameplayCommandKind.ActionPressed, actionId));
                _anyInputPressedThisFrame = true;
            }
            else if (!down && before)
            {
                IncrementCounter(_pendingReleaseCounts, actionId);
                _frameGameplayCommands.Add(new InputGameplayCommand(InputGameplayCommandKind.ActionReleased, actionId));
            }

            var axisDelta = AccumulateDeltaAxis(bindings);
            if (axisDelta != 0f)
            {
                _pendingAxisDelta.TryGetValue(actionId, out var pending);
                _pendingAxisDelta[actionId] = pending + axisDelta;
            }
        }

        lock (_keyboardPulseLock)
            _keyboardPulseDown.Clear();
        lock (_mousePulseLock)
        {
            _mousePulseDown.Clear();
            _mousePulseUp.Clear();
        }
    }

    /// <inheritdoc />
    public bool IsDown(string actionId) => _actionDown.TryGetValue(actionId, out var down) && down;

    /// <inheritdoc />
    public bool WasPressed(string actionId)
    {
        var now = IsDown(actionId);
        var before = _prevActionDown.TryGetValue(actionId, out var prev) && prev;
        return now && !before;
    }

    /// <inheritdoc />
    public bool WasReleased(string actionId)
    {
        var now = IsDown(actionId);
        var before = _prevActionDown.TryGetValue(actionId, out var prev) && prev;
        return !now && before;
    }

    /// <inheritdoc />
    public float ReadAxis(string axisId) => _axisValues.TryGetValue(axisId, out var value) ? value : 0f;

    /// <inheritdoc />
    public bool ConsumePressed(string actionId) => ConsumeCounter(_pendingPressCounts, actionId);

    /// <inheritdoc />
    public bool ConsumeReleased(string actionId) => ConsumeCounter(_pendingReleaseCounts, actionId);

    /// <inheritdoc />
    public float ConsumeAxisDelta(string axisId)
    {
        if (!_pendingAxisDelta.TryGetValue(axisId, out var pending))
            return 0f;
        _pendingAxisDelta.Remove(axisId);
        return pending;
    }

    /// <inheritdoc />
    public bool IsControlDown(InputControl control) => ReadControlValue(control) > 0f;

    /// <inheritdoc />
    public float ReadControlValue(InputControl control)
    {
        return control.Kind switch
        {
            InputControlKind.KeyboardKey => IsAnyKeyboardPressed(control.Key) ? 1f : 0f,
            InputControlKind.MouseButton => MouseButton(control.MouseButton) ? 1f : 0f,
            InputControlKind.MouseAxis => ReadMouseAxis(control.MouseAxis),
            _ => 0f
        };
    }

    /// <summary>Disposes the owned Silk input context and devices.</summary>
    public void Dispose()
    {
        for (var i = 0; i < _keyboardSubscriptions.Count; i++)
        {
            var (kb, h) = _keyboardSubscriptions[i];
            kb.KeyDown -= h;
        }

        _keyboardSubscriptions.Clear();
        for (var i = 0; i < _mouseSubscriptions.Count; i++)
        {
            var (mouse, onDown, onUp) = _mouseSubscriptions[i];
            mouse.MouseDown -= onDown;
            mouse.MouseUp -= onUp;
        }

        _mouseSubscriptions.Clear();
        _input.Dispose();
    }

    private void SubscribeExistingKeyboards()
    {
        for (var i = 0; i < _input.Keyboards.Count; i++)
        {
            var kb = _input.Keyboards[i];
            Action<IKeyboard, Key, int> h = OnKeyboardKeyDown;
            kb.KeyDown += h;
            _keyboardSubscriptions.Add((kb, h));
        }
    }

    private void SubscribeExistingMice()
    {
        for (var i = 0; i < _input.Mice.Count; i++)
        {
            var mouse = _input.Mice[i];
            Action<IMouse, MouseButton> onDown = OnMouseButtonDown;
            Action<IMouse, MouseButton> onUp = OnMouseButtonUp;
            mouse.MouseDown += onDown;
            mouse.MouseUp += onUp;
            _mouseSubscriptions.Add((mouse, onDown, onUp));
        }
    }

    private void OnKeyboardKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        _ = keyboard;
        _ = scancode;
        lock (_keyboardPulseLock)
            _keyboardPulseDown.Add(key);
    }

    private void OnMouseButtonDown(IMouse mouse, MouseButton button)
    {
        _ = mouse;
        lock (_mousePulseLock)
            _mousePulseDown.Add(button);
    }

    private void OnMouseButtonUp(IMouse mouse, MouseButton button)
    {
        _ = mouse;
        lock (_mousePulseLock)
            _mousePulseUp.Add(button);
    }

    private bool IsAnyKeyboardPressed(Key key)
    {
        lock (_keyboardPulseLock)
        {
            if (_keyboardPulseDown.Contains(key))
                return true;
        }

        for (int i = 0; i < _input.Keyboards.Count; i++)
        {
            if (_input.Keyboards[i].IsKeyPressed(key))
                return true;
        }

        return false;
    }

    private bool IsAnyMouseButtonPressed(MouseButton button)
    {
        for (int i = 0; i < _input.Mice.Count; i++)
        {
            if (_input.Mice[i].IsButtonPressed(button))
                return true;
        }

        return false;
    }

    private void SampleMouseButtons()
    {
        for (var i = 0; i < TrackedMouseButtons.Length; i++)
        {
            var button = TrackedMouseButtons[i];
            var pulseDown = WasMouseButtonPulsedDown(button);
            var pulseUp = WasMouseButtonPulsedUp(button);
            var down = IsAnyMouseButtonPressed(button) || pulseDown;
            _mouseButtonDown[button] = down;

            var before = _prevMouseButtonDown.TryGetValue(button, out var prevDown) && prevDown;
            var pressed = down && !before;
            var released = (!down && before) || (pulseUp && (before || pulseDown));
            _mouseButtonPressedThisFrame[button] = pressed;
            _mouseButtonReleasedThisFrame[button] = released;
            if (pressed)
                IncrementCounter(_pendingMousePressCounts, button);
            if (released)
                IncrementCounter(_pendingMouseReleaseCounts, button);
        }
    }

    private bool WasMouseButtonPulsedDown(MouseButton button)
    {
        lock (_mousePulseLock)
            return _mousePulseDown.Contains(button);
    }

    private bool WasMouseButtonPulsedUp(MouseButton button)
    {
        lock (_mousePulseLock)
            return _mousePulseUp.Contains(button);
    }

    private float ReadMouseAxis(MouseAxis axis)
    {
        return axis switch
        {
            MouseAxis.PositionX => _mousePosition.X,
            MouseAxis.PositionY => _mousePosition.Y,
            MouseAxis.DeltaX => _mouseDelta.X,
            MouseAxis.DeltaY => _mouseDelta.Y,
            MouseAxis.WheelX => _mouseWheelDelta.X,
            MouseAxis.WheelY => _mouseWheelDelta.Y,
            _ => 0f
        };
    }

    private void UpdateMouseSnapshot()
    {
        if (_input.Mice.Count == 0)
        {
            _mouseDelta = Vector2.Zero;
            _mouseWheelDelta = Vector2.Zero;
            return;
        }

        var mouse = _input.Mice[0];
        var now = new Vector2(mouse.Position.X, mouse.Position.Y);
        _mouseDelta = _hasMousePosition ? now - _mousePosition : Vector2.Zero;
        _mousePosition = now;
        _hasMousePosition = true;
        var wheels = mouse.ScrollWheels;
        _mouseWheelDelta = wheels.Count > 0
            ? new Vector2(wheels[0].X, wheels[0].Y)
            : Vector2.Zero;
    }

    private float AccumulateDeltaAxis(IReadOnlyList<InputBinding> bindings)
    {
        var sum = 0f;
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding.Control.Kind is not InputControlKind.MouseAxis)
                continue;

            var axis = binding.Control.MouseAxis;
            if (!IsDeltaMouseAxis(axis))
                continue;

            sum += ReadMouseAxis(axis) * binding.Scale;
        }

        return sum;
    }

    private static bool IsDeltaMouseAxis(MouseAxis axis) =>
        axis is MouseAxis.DeltaX or MouseAxis.DeltaY or MouseAxis.WheelX or MouseAxis.WheelY;

    private static void IncrementCounter<TKey>(Dictionary<TKey, int> table, TKey key)
        where TKey : notnull
    {
        table.TryGetValue(key, out var count);
        table[key] = count + 1;
    }

    private static bool ConsumeCounter<TKey>(Dictionary<TKey, int> table, TKey key)
        where TKey : notnull
    {
        if (!table.TryGetValue(key, out var count) || count <= 0)
            return false;
        if (count == 1)
            table.Remove(key);
        else
            table[key] = count - 1;
        return true;
    }

    private Vector2 ConvertMousePosition(Vector2 swapchainPosition, CoordinateSpace space)
    {
        return space switch
        {
            CoordinateSpace.SwapchainSpace => swapchainPosition,
            CoordinateSpace.ViewportSpace => ConvertSwapchainToViewport(swapchainPosition),
            CoordinateSpace.PresentationViewportSpace => ConvertSwapchainToPresentationViewport(swapchainPosition),
            CoordinateSpace.WorldSpace => ConvertSwapchainToWorld(swapchainPosition),
            CoordinateSpace.LocalSpace => throw new NotSupportedException("Mouse coordinates are not defined in LocalSpace without an entity transform."),
            _ => throw new ArgumentOutOfRangeException(nameof(space), space, "Unsupported coordinate space.")
        };
    }

    private Vector2 ConvertMouseDelta(Vector2 swapchainDelta, CoordinateSpace space)
    {
        return space switch
        {
            CoordinateSpace.SwapchainSpace => swapchainDelta,
            CoordinateSpace.ViewportSpace => ConvertSwapchainDeltaToViewportDelta(swapchainDelta),
            CoordinateSpace.PresentationViewportSpace => ConvertSwapchainDeltaToPresentationViewportDelta(swapchainDelta),
            CoordinateSpace.WorldSpace => ConvertViewportDeltaToWorldDelta(ConvertSwapchainDeltaToViewportDelta(swapchainDelta)),
            CoordinateSpace.LocalSpace => throw new NotSupportedException("Mouse delta is not defined in LocalSpace without an entity transform."),
            _ => throw new ArgumentOutOfRangeException(nameof(space), space, "Unsupported coordinate space.")
        };
    }

    private Vector2 ConvertSwapchainToViewport(Vector2 swapchainPosition)
    {
        var mapping = ResolveCameraMapping();
        var viewport = CameraProjection.SwapchainPixelToViewportPixel(
            new Vector2D<float>(swapchainPosition.X, swapchainPosition.Y),
            in mapping.Physical);
        return new Vector2(viewport.X, viewport.Y);
    }

    private Vector2 ConvertSwapchainToPresentationViewport(Vector2 swapchainPosition)
    {
        var physical = ResolvePresentationPhysical();
        var viewport = CameraProjection.SwapchainPixelToViewportPixel(
            new Vector2D<float>(swapchainPosition.X, swapchainPosition.Y),
            in physical);
        return new Vector2(viewport.X, viewport.Y);
    }

    private Vector2 ConvertSwapchainToWorld(Vector2 swapchainPosition)
    {
        var mapping = ResolveCameraMapping();
        var viewport = CameraProjection.SwapchainPixelToViewportPixel(
            new Vector2D<float>(swapchainPosition.X, swapchainPosition.Y),
            in mapping.Physical);
        var world = CameraProjection.ViewportPixelToWorld(
            viewport,
            mapping.Camera.PositionWorld,
            mapping.Camera.RotationRadians,
            new Vector2D<float>(mapping.Camera.ViewportSizeWorld.X, mapping.Camera.ViewportSizeWorld.Y));
        return new Vector2(world.X, world.Y);
    }

    private Vector2 ConvertSwapchainDeltaToViewportDelta(Vector2 swapchainDelta)
    {
        var mapping = ResolveCameraMapping();
        var invScale = 1f / mapping.Physical.Scale;
        return swapchainDelta * invScale;
    }

    private Vector2 ConvertSwapchainDeltaToPresentationViewportDelta(Vector2 swapchainDelta)
    {
        var physical = ResolvePresentationPhysical();
        var invScale = 1f / physical.Scale;
        return swapchainDelta * invScale;
    }

    private Vector2 ConvertViewportDeltaToWorldDelta(Vector2 viewportDelta)
    {
        var mapping = ResolveCameraMapping();
        var c = MathF.Cos(mapping.Camera.RotationRadians);
        var s = MathF.Sin(mapping.Camera.RotationRadians);
        // Viewport deltas are +Y down; convert to +Y up camera-centered deltas first.
        var rx = viewportDelta.X;
        var ry = -viewportDelta.Y;
        return new Vector2(
            rx * c - ry * s,
            rx * s + ry * c);
    }

    private (CameraViewRequest Camera, PhysicalViewport Physical) ResolveCameraMapping()
    {
        if (_renderer is null)
        {
            var fallbackSwap = new Vector2D<int>(1, 1);
            var fallbackCamera = CameraSelection.Default(fallbackSwap);
            var fallbackPhysical = CameraProjection.ComputePhysicalViewport(fallbackCamera.ViewportSizeWorld, fallbackSwap);
            return (fallbackCamera, fallbackPhysical);
        }

        var swapchain = _renderer.SwapchainPixelSize;
        if (_host is not null)
        {
            var crs = _host.CameraRuntimeState;
            if (crs.Valid && crs.ViewportSizeWorld.X > 0 && crs.ViewportSizeWorld.Y > 0)
            {
                var camera = new CameraViewRequest
                {
                    PositionWorld = crs.PositionWorld,
                    RotationRadians = crs.RotationRadians,
                    ViewportSizeWorld = crs.ViewportSizeWorld,
                    PresentationViewportSizeWorld = crs.PresentationViewportSizeWorld,
                    Priority = crs.Priority,
                    Enabled = true,
                    BackgroundColor = crs.BackgroundColor
                };
                var physical = CameraProjection.ComputePhysicalViewport(crs.ViewportSizeWorld, swapchain);
                return (camera, physical);
            }
        }

        var cameraFromRenderer = _renderer.ActiveCameraView;
        var physicalFromRenderer = CameraProjection.ComputePhysicalViewport(cameraFromRenderer.ViewportSizeWorld, swapchain);
        return (cameraFromRenderer, physicalFromRenderer);
    }

    private PhysicalViewport ResolvePresentationPhysical()
    {
        if (_renderer is null)
        {
            var fallbackSwap = new Vector2D<int>(1, 1);
            var fallbackCamera = CameraSelection.Default(fallbackSwap);
            var pres = CameraPresentationLayout.ResolvePresentationViewportSize(fallbackCamera);
            return CameraProjection.ComputePhysicalViewport(pres, fallbackSwap);
        }

        var swapchain = _renderer.SwapchainPixelSize;
        if (_host is not null)
        {
            var crs = _host.CameraRuntimeState;
            if (crs.Valid && crs.ViewportSizeWorld.X > 0 && crs.ViewportSizeWorld.Y > 0)
            {
                var pres = CameraPresentationLayout.ResolvePresentationViewportSize(crs);
                return CameraProjection.ComputePhysicalViewport(pres, swapchain);
            }
        }

        var cam = _renderer.ActiveCameraView;
        var presSize = CameraPresentationLayout.ResolvePresentationViewportSize(cam);
        return CameraProjection.ComputePhysicalViewport(presSize, swapchain);
    }
}
