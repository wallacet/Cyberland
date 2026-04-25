using System.Collections.Generic;
using System.Numerics;
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
/// </remarks>
public sealed class SilkInputService : IInputService, IDisposable
{
    private readonly IInputContext _input;
    private readonly IRenderer? _renderer;
    private readonly Dictionary<string, bool> _actionDown = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _prevActionDown = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _axisValues = new(StringComparer.Ordinal);
    private readonly object _keyboardPulseLock = new();
    private readonly HashSet<Key> _keyboardPulseDown = new();
    private readonly List<(IKeyboard Keyboard, Action<IKeyboard, Key, int> Handler)> _keyboardSubscriptions = new();
    private Vector2 _mousePosition;
    private Vector2 _mouseDelta;
    private Vector2 _mouseWheelDelta;
    private bool _hasMousePosition;

    /// <summary>Create a service backed by the provided Silk input context.</summary>
    public SilkInputService(IInputContext input, IRenderer? renderer = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _renderer = renderer;
        Bindings = new InputBindings();
        SubscribeExistingKeyboards();
    }

    /// <inheritdoc />
    public InputBindings Bindings { get; }

    /// <inheritdoc />
    public Vector2 MousePosition => _mousePosition;

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
    public void BeginFrame()
    {
        _prevActionDown.Clear();
        foreach (var (id, isDown) in _actionDown)
            _prevActionDown[id] = isDown;

        _actionDown.Clear();
        _axisValues.Clear();
        UpdateMouseSnapshot();

        foreach (var actionId in Bindings.ActionIds)
        {
            if (!Bindings.TryGetBindings(actionId, out var bindings) || bindings.Count == 0)
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
        }

        lock (_keyboardPulseLock)
            _keyboardPulseDown.Clear();
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
    public bool IsControlDown(InputControl control) => ReadControlValue(control) > 0f;

    /// <inheritdoc />
    public float ReadControlValue(InputControl control)
    {
        return control.Kind switch
        {
            InputControlKind.KeyboardKey => IsAnyKeyboardPressed(control.Key) ? 1f : 0f,
            InputControlKind.MouseButton => IsAnyMouseButtonPressed(control.MouseButton) ? 1f : 0f,
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

    private void OnKeyboardKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        _ = keyboard;
        _ = scancode;
        lock (_keyboardPulseLock)
            _keyboardPulseDown.Add(key);
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

        _mouseWheelDelta = Vector2.Zero;
    }

    private Vector2 ConvertMousePosition(Vector2 swapchainPosition, CoordinateSpace space)
    {
        return space switch
        {
            CoordinateSpace.SwapchainSpace => swapchainPosition,
            CoordinateSpace.ViewportSpace => ConvertSwapchainToViewport(swapchainPosition),
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

        var camera = _renderer.ActiveCameraView;
        var swapchain = _renderer.SwapchainPixelSize;
        var physical = CameraProjection.ComputePhysicalViewport(camera.ViewportSizeWorld, swapchain);
        return (camera, physical);
    }
}
