using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo.BrickBreaker;

public static class BrickInputSetup
{
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
