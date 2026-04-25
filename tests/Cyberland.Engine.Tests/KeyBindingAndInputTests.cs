using System.Numerics;
using System.Reflection;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Moq;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class KeyBindingAndInputTests
{
    [Fact]
    public void InputActionId_stores_id()
    {
        var a = new InputActionId("jump");
        Assert.Equal("jump", a.Id);
        string asString = a;
        Assert.Equal("jump", asString);
    }

    [Fact]
    public void InputControl_parse_roundtrip_for_keyboard_mouse_and_axis()
    {
        Assert.True(InputControl.TryParse("keyboard:Space", out var key));
        Assert.Equal(InputControlKind.KeyboardKey, key.Kind);
        Assert.Equal("keyboard:Space", key.ToPersistedString());

        Assert.True(InputControl.TryParse("mouse:Left", out var button));
        Assert.Equal(InputControlKind.MouseButton, button.Kind);
        Assert.Equal("mouse:Left", button.ToPersistedString());

        Assert.True(InputControl.TryParse("mouseAxis:DeltaX", out var axis));
        Assert.Equal(InputControlKind.MouseAxis, axis.Kind);
        Assert.Equal("mouseAxis:DeltaX", axis.ToPersistedString());

        var invalid = CreateControl((InputControlKind)99, default, default, default);
        Assert.Throws<InvalidOperationException>(() => invalid.ToPersistedString());
    }

    [Fact]
    public void InputBindings_supports_multiple_bindings_and_removal()
    {
        var bindings = new InputBindings();
        var left = new InputBinding(InputControl.Keyboard(Key.A), -1f);
        var right = new InputBinding(InputControl.Keyboard(Key.D), +1f);
        bindings.AddBinding("move_x", left);
        bindings.AddBinding("move_x", right);

        Assert.True(bindings.TryGetBindings("move_x", out var current));
        Assert.Equal(2, current.Count);
        Assert.True(bindings.RemoveBinding("move_x", left));
        Assert.True(bindings.TryGetBindings("move_x", out current));
        Assert.Single(current);
    }

    [Fact]
    public async Task InputBindings_load_or_create_writes_defaults_when_file_absent()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb input " + Guid.NewGuid() + ".json");
        try
        {
            var bindings = new InputBindings();
            await bindings.LoadOrCreateUserFileAsync(path);
            Assert.True(File.Exists(path));
            Assert.True(bindings.TryGetBindings("cyberland.common/quit", out var quit));
            Assert.NotEmpty(quit);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task InputBindings_save_and_load_roundtrip_preserves_mouse_binding()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb input rt " + Guid.NewGuid() + ".json");
        try
        {
            var a = new InputBindings();
            a.SetBindings(
                "fire",
                new[]
                {
                    new InputBinding(InputControl.Keyboard(Key.Space)),
                    new InputBinding(InputControl.MouseButtonControl(MouseButton.Left))
                });
            await a.SaveAsync(path);

            var b = new InputBindings();
            await b.LoadOrCreateUserFileAsync(path);
            Assert.True(b.TryGetBindings("fire", out var loaded));
            Assert.Equal(2, loaded.Count);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void InputBindings_clear_and_missing_paths_are_safe()
    {
        var bindings = new InputBindings();
        var left = new InputBinding(InputControl.Keyboard(Key.A), -1f);
        bindings.AddBinding("move_x", left);

        Assert.False(bindings.RemoveBinding("missing", left));
        Assert.True(bindings.ClearBindings("move_x"));
        Assert.False(bindings.TryGetBindings("move_x", out var afterClear));
        Assert.Empty(afterClear);

        bindings.AddBinding("move_x", left);
        bindings.Clear();
        Assert.False(bindings.TryGetBindings("move_x", out _));
    }

    [Fact]
    public async Task InputBindings_load_skips_invalid_entries_and_null_payload_uses_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb input invalid " + Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllTextAsync(
                path,
                """
                {
                  "version": 1,
                  "bindings": {
                    "": [{ "control": "keyboard:W" }],
                    "ok": null,
                    "mixed": [{ "control": null }, { "control": "bad:foo" }, { "control": "keyboard:D", "scale": 0.5 }]
                  }
                }
                """);

            var bindings = new InputBindings();
            await bindings.LoadOrCreateUserFileAsync(path);
            Assert.True(bindings.TryGetBindings("mixed", out var mixed));
            Assert.Single(mixed);
            Assert.Equal(0.5f, mixed[0].Scale);

            await File.WriteAllTextAsync(path, "null");
            await bindings.LoadOrCreateUserFileAsync(path);
            Assert.True(bindings.TryGetBindings("cyberland.common/quit", out _));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void SilkInputService_supports_edges_and_axis_composition()
    {
        var pressedKeys = new HashSet<Key>();
        var pressedButtons = new HashSet<MouseButton>();
        var service = CreateService(pressedKeys, pressedButtons, () => Vector2.Zero);
        service.Bindings.SetBindings(
            "jump",
            new[]
            {
                new InputBinding(InputControl.Keyboard(Key.Space)),
                new InputBinding(InputControl.MouseButtonControl(MouseButton.Left))
            });
        service.Bindings.SetBindings(
            "move_x",
            new[]
            {
                new InputBinding(InputControl.Keyboard(Key.A), -1f),
                new InputBinding(InputControl.Keyboard(Key.D), +1f),
                new InputBinding(InputControl.MouseAxisControl(MouseAxis.DeltaX), +10f)
            });

        pressedKeys.Add(Key.Space);
        pressedKeys.Add(Key.D);
        pressedButtons.Add(MouseButton.Left);
        service.BeginFrame();
        Assert.True(service.IsDown("jump"));
        Assert.True(service.WasPressed("jump"));
        Assert.Equal(1f, service.ReadAxis("move_x"));
        Assert.False(service.IsControlDown(InputControl.Keyboard(Key.A)));
        Assert.Equal(0f, service.ReadAxis("unknown"));

        service.BeginFrame();
        Assert.True(service.IsDown("jump"));
        Assert.False(service.WasPressed("jump"));

        pressedKeys.Clear();
        pressedButtons.Clear();
        service.BeginFrame();
        Assert.False(service.IsDown("jump"));
        Assert.True(service.WasReleased("jump"));

        service.Bindings.SetBindings("empty", Array.Empty<InputBinding>());
        service.BeginFrame();
        Assert.False(service.IsDown("empty"));
    }

    [Fact]
    public void SilkInputService_reports_mouse_position_and_movement()
    {
        var pressedKeys = new HashSet<Key>();
        var pressedButtons = new HashSet<MouseButton>();
        var pos = new Vector2(100f, 200f);
        var renderer = CreateRendererForInput(
            swapchain: new Vector2D<int>(1920, 1080),
            camera: new CameraViewRequest
            {
                PositionWorld = new Vector2D<float>(640f, 360f),
                RotationRadians = 0f,
                ViewportSizeWorld = new Vector2D<int>(1280, 720),
                Priority = 0,
                Enabled = true,
                BackgroundColor = new Vector4D<float>(0f, 0f, 0f, 1f)
            });
        var service = CreateService(pressedKeys, pressedButtons, () => pos, renderer.Object);
        service.Bindings.SetBindings("look_x", new[] { new InputBinding(InputControl.MouseAxisControl(MouseAxis.DeltaX)) });

        service.BeginFrame();
        Assert.Equal(new Vector2(100f, 200f), service.MousePosition);
        Assert.Equal(Vector2.Zero, service.MouseDelta);
        Assert.Equal(Vector2.Zero, service.MouseWheelDelta);
        AssertVectorNear(new Vector2(66.666664f, 133.33333f), service.GetMousePosition(CoordinateSpace.ViewportSpace), 0.0001f);
        AssertVectorNear(new Vector2(66.666664f, 586.6667f), service.GetMousePosition(CoordinateSpace.WorldSpace), 0.0001f);
        Assert.Equal(new Vector2(100f, 200f), service.GetMousePosition(CoordinateSpace.SwapchainSpace));

        pos = new Vector2(112f, 195f);
        service.BeginFrame();
        Assert.Equal(new Vector2(12f, -5f), service.MouseDelta);
        Assert.Equal(Vector2.Zero, service.MouseWheelDelta);
        AssertVectorNear(new Vector2(8f, -3.3333333f), service.GetMouseDelta(CoordinateSpace.ViewportSpace), 0.0001f);
        AssertVectorNear(new Vector2(8f, 3.3333333f), service.GetMouseDelta(CoordinateSpace.WorldSpace), 0.0001f);
        Assert.Equal(new Vector2(12f, -5f), service.GetMouseDelta(CoordinateSpace.SwapchainSpace));
        Assert.Equal(12f, service.ReadControlValue(InputControl.MouseAxisControl(MouseAxis.DeltaX)));
        Assert.Equal(112f, service.ReadControlValue(InputControl.MouseAxisControl(MouseAxis.PositionX)));
        Assert.Equal(195f, service.ReadControlValue(InputControl.MouseAxisControl(MouseAxis.PositionY)));
        Assert.Equal(-5f, service.ReadControlValue(InputControl.MouseAxisControl(MouseAxis.DeltaY)));
        Assert.Equal(0f, service.ReadControlValue(InputControl.MouseAxisControl(MouseAxis.WheelX)));
    }

    [Fact]
    public void SilkInputService_handles_missing_devices_and_dispose()
    {
        var input = new Mock<IInputContext>(MockBehavior.Strict);
        input.SetupGet(x => x.Keyboards).Returns(Array.Empty<IKeyboard>());
        input.SetupGet(x => x.Mice).Returns(Array.Empty<IMouse>());
        input.Setup(x => x.Dispose());

        var service = new SilkInputService(input.Object);
        service.Bindings.SetBindings("jump", new[] { new InputBinding(InputControl.Keyboard(Key.Space)) });
        service.BeginFrame();

        Assert.Equal(Vector2.Zero, service.MouseDelta);
        Assert.Equal(0f, service.ReadControlValue(InputControl.Keyboard(Key.Space)));
        Assert.Equal(0f, service.ReadControlValue(InputControl.MouseButtonControl(MouseButton.Left)));
        Assert.Equal(0f, service.ReadControlValue(InputControl.MouseAxisControl(MouseAxis.WheelY)));
        var invalidKind = CreateControl((InputControlKind)42, default, default, default);
        var invalidAxis = CreateControl(InputControlKind.MouseAxis, default, default, (MouseAxis)42);
        Assert.Equal(0f, service.ReadControlValue(invalidKind));
        Assert.Equal(0f, service.ReadControlValue(invalidAxis));
        service.Dispose();
        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void SilkInputService_mouse_space_queries_cover_fallback_and_invalid_spaces()
    {
        var pressedKeys = new HashSet<Key>();
        var pressedButtons = new HashSet<MouseButton>();
        var pos = new Vector2(9f, 11f);
        var service = CreateService(pressedKeys, pressedButtons, () => pos);
        service.BeginFrame();

        Assert.Equal(pos, service.GetMousePosition());
        Assert.Equal(Vector2.Zero, service.GetMouseDelta());
        Assert.Throws<NotSupportedException>(() => service.GetMousePosition(CoordinateSpace.LocalSpace));
        Assert.Throws<NotSupportedException>(() => service.GetMouseDelta(CoordinateSpace.LocalSpace));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetMousePosition((CoordinateSpace)12345));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetMouseDelta((CoordinateSpace)12345));
    }

    [Fact]
    public void SilkInputService_KeyDown_pulse_makes_WasPressed_when_IsKeyPressed_misses_transient_tap()
    {
        Action<IKeyboard, Key, int>? onKeyDown = null;
        var keyboard = new Mock<IKeyboard>(MockBehavior.Loose);
        keyboard.Setup(k => k.IsKeyPressed(It.IsAny<Key>())).Returns(false);
        keyboard.SetupAdd(k => k.KeyDown += It.IsAny<Action<IKeyboard, Key, int>>())
            .Callback((Action<IKeyboard, Key, int> h) => onKeyDown = h);
        keyboard.SetupRemove(k => k.KeyDown -= It.IsAny<Action<IKeyboard, Key, int>>());

        var input = new Mock<IInputContext>(MockBehavior.Strict);
        input.SetupGet(x => x.Keyboards).Returns(new[] { keyboard.Object });
        input.SetupGet(x => x.Mice).Returns(Array.Empty<IMouse>());
        input.Setup(x => x.Dispose());

        var service = new SilkInputService(input.Object);
        service.Bindings.SetBindings("fire", new[] { new InputBinding(InputControl.Keyboard(Key.Space)) });

        service.BeginFrame();
        Assert.False(service.WasPressed("fire"));

        Assert.NotNull(onKeyDown);
        onKeyDown!(keyboard.Object, Key.Space, 0);

        service.BeginFrame();
        Assert.True(service.WasPressed("fire"));
        Assert.True(service.IsDown("fire"));

        service.BeginFrame();
        Assert.False(service.WasPressed("fire"));
        Assert.False(service.IsDown("fire"));

        service.Dispose();
        input.Verify(x => x.Dispose(), Times.Once);
    }

    private static SilkInputService CreateService(
        HashSet<Key> pressedKeys,
        HashSet<MouseButton> pressedButtons,
        Func<Vector2> mousePosition,
        IRenderer? renderer = null)
    {
        var keyboard = new Mock<IKeyboard>(MockBehavior.Loose);
        keyboard.Setup(x => x.IsKeyPressed(It.IsAny<Key>())).Returns<Key>(key => pressedKeys.Contains(key));

        var mouse = new Mock<IMouse>(MockBehavior.Strict);
        mouse.Setup(x => x.IsButtonPressed(It.IsAny<MouseButton>())).Returns<MouseButton>(button => pressedButtons.Contains(button));
        mouse.SetupGet(x => x.Position).Returns(() => mousePosition());

        var input = new Mock<IInputContext>(MockBehavior.Strict);
        input.SetupGet(x => x.Keyboards).Returns(new[] { keyboard.Object });
        input.SetupGet(x => x.Mice).Returns(new[] { mouse.Object });
        input.Setup(x => x.Dispose());

        return new SilkInputService(input.Object, renderer);
    }

    private static Mock<IRenderer> CreateRendererForInput(Vector2D<int> swapchain, CameraViewRequest camera)
    {
        var renderer = new Mock<IRenderer>(MockBehavior.Strict);
        renderer.SetupGet(x => x.SwapchainPixelSize).Returns(swapchain);
        renderer.SetupGet(x => x.ActiveCameraView).Returns(camera);
        renderer.SetupGet(x => x.ActiveCameraViewportSize).Returns(camera.ViewportSizeWorld);
        return renderer;
    }

    private static void AssertVectorNear(Vector2 expected, Vector2 actual, float epsilon)
    {
        Assert.InRange(actual.X, expected.X - epsilon, expected.X + epsilon);
        Assert.InRange(actual.Y, expected.Y - epsilon, expected.Y + epsilon);
    }

    private static InputControl CreateControl(InputControlKind kind, Key key, MouseButton button, MouseAxis axis)
    {
        var ctor = typeof(InputControl).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            new[] { typeof(InputControlKind), typeof(Key), typeof(MouseButton), typeof(MouseAxis) },
            modifiers: null)!;
        return (InputControl)ctor.Invoke(new object[] { kind, key, button, axis });
    }
}
