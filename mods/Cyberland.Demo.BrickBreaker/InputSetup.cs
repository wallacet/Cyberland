using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Default keyboard and mouse actions. Keeping bindings here matches how larger mods isolate control schemes
/// before layering rebinding UI.
/// </summary>
public static class InputSetup
{
    /// <summary>
    /// Seeds <c>cyberland.demo.brickbreaker/*</c> actions next to the host’s baseline table.
    /// </summary>
    public static void RegisterDefaultBindings(ModLoadContext context)
    {
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/move_x", new InputBinding(InputControl.Keyboard(Key.A), -1f));
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/move_x", new InputBinding(InputControl.Keyboard(Key.Left), -1f));
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/move_x", new InputBinding(InputControl.Keyboard(Key.D), +1f));
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/move_x", new InputBinding(InputControl.Keyboard(Key.Right), +1f));
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/launch_ball", new InputBinding(InputControl.Keyboard(Key.Space)));
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/launch_ball", new InputBinding(InputControl.MouseButtonControl(MouseButton.Left)));
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/start_round", new InputBinding(InputControl.Keyboard(Key.Enter)));
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/start_round", new InputBinding(InputControl.Keyboard(Key.KeypadEnter)));
        context.AddDefaultInputBinding("cyberland.demo.brickbreaker/start_round", new InputBinding(InputControl.Keyboard(Key.R)));
    }
}
