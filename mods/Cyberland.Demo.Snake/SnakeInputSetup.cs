using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo.Snake;

public static class SnakeInputSetup
{
    public static void RegisterDefaultBindings(ModLoadContext context)
    {
        context.AddDefaultInputBinding("cyberland.demo.snake/up", new InputBinding(InputControl.Keyboard(Key.Up)));
        context.AddDefaultInputBinding("cyberland.demo.snake/down", new InputBinding(InputControl.Keyboard(Key.Down)));
        context.AddDefaultInputBinding("cyberland.demo.snake/left", new InputBinding(InputControl.Keyboard(Key.Left)));
        context.AddDefaultInputBinding("cyberland.demo.snake/right", new InputBinding(InputControl.Keyboard(Key.Right)));
        context.AddDefaultInputBinding("cyberland.demo.snake/start_game", new InputBinding(InputControl.Keyboard(Key.Enter)));
        context.AddDefaultInputBinding("cyberland.demo.snake/start_game", new InputBinding(InputControl.Keyboard(Key.KeypadEnter)));
        context.AddDefaultInputBinding("cyberland.demo.snake/start_game", new InputBinding(InputControl.Keyboard(Key.R)));
    }
}
