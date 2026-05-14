using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo.WhackAMole;

/// <summary>Default named actions for the Whack-a-Mole sample.</summary>
public static class WhackAMoleInputSetup
{
    /// <summary>Registers first-run defaults; user JSON can rebind these ids later.</summary>
    public static void RegisterDefaultBindings(ModLoadContext context)
    {
        context.AddDefaultInputBinding(
            "cyberland.demo.whackamole/hit",
            new InputBinding(InputControl.MouseButtonControl(MouseButton.Left)));
        context.AddDefaultInputBinding(
            "cyberland.demo.whackamole/restart",
            new InputBinding(InputControl.Keyboard(Key.R)));
    }
}
