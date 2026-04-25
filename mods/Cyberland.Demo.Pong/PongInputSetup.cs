using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo.Pong;

public static class PongInputSetup
{
    public static void RegisterDefaultBindings(ModLoadContext context)
    {
        context.AddDefaultInputBinding("cyberland.demo.pong/paddle_y", new InputBinding(InputControl.Keyboard(Key.S), -1f));
        context.AddDefaultInputBinding("cyberland.demo.pong/paddle_y", new InputBinding(InputControl.Keyboard(Key.Down), -1f));
        context.AddDefaultInputBinding("cyberland.demo.pong/paddle_y", new InputBinding(InputControl.Keyboard(Key.W), +1f));
        context.AddDefaultInputBinding("cyberland.demo.pong/paddle_y", new InputBinding(InputControl.Keyboard(Key.Up), +1f));
        context.AddDefaultInputBinding("cyberland.demo.pong/toggle_visual_sync", new InputBinding(InputControl.Keyboard(Key.F10)));
        context.AddDefaultInputBinding("cyberland.demo.pong/start_match", new InputBinding(InputControl.Keyboard(Key.Enter)));
        context.AddDefaultInputBinding("cyberland.demo.pong/start_match", new InputBinding(InputControl.Keyboard(Key.KeypadEnter)));
        context.AddDefaultInputBinding("cyberland.demo.pong/start_match", new InputBinding(InputControl.Keyboard(Key.R)));
    }
}
