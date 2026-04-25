using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo.MouseChase;

public static class MouseChaseInputSetup
{
    public static void RegisterDefaultBindings(ModLoadContext context)
    {
        context.AddDefaultInputBinding("cyberland.demo.mousechase/restart", new InputBinding(InputControl.Keyboard(Key.R)));
        context.AddDefaultInputBinding("cyberland.demo.mousechase/restart", new InputBinding(InputControl.Keyboard(Key.Enter)));
        context.AddDefaultInputBinding("cyberland.demo.mousechase/primary", new InputBinding(InputControl.MouseButtonControl(MouseButton.Left)));
        context.AddDefaultInputBinding("cyberland.demo.mousechase/zoom", new InputBinding(InputControl.MouseAxisControl(MouseAxis.WheelY)));
    }
}
