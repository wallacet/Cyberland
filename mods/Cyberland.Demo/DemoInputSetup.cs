using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo;

/// <summary>Default keyboard bindings for this mod; <see cref="IMod.OnLoad"/> adds them after the host seed.</summary>
public static class DemoInputSetup
{
    /// <summary>Registers <c>cyberland.demo/*</c> actions. User <c>input-bindings.json</c> can override the same action ids.</summary>
    public static void RegisterDefaultBindings(ModLoadContext context)
    {
        context.AddDefaultInputBinding("cyberland.demo/move_x", new InputBinding(InputControl.Keyboard(Key.A), -1f));
        context.AddDefaultInputBinding("cyberland.demo/move_x", new InputBinding(InputControl.Keyboard(Key.Left), -1f));
        context.AddDefaultInputBinding("cyberland.demo/move_x", new InputBinding(InputControl.Keyboard(Key.D), +1f));
        context.AddDefaultInputBinding("cyberland.demo/move_x", new InputBinding(InputControl.Keyboard(Key.Right), +1f));
        context.AddDefaultInputBinding("cyberland.demo/move_y", new InputBinding(InputControl.Keyboard(Key.S), -1f));
        context.AddDefaultInputBinding("cyberland.demo/move_y", new InputBinding(InputControl.Keyboard(Key.Down), -1f));
        context.AddDefaultInputBinding("cyberland.demo/move_y", new InputBinding(InputControl.Keyboard(Key.W), +1f));
        context.AddDefaultInputBinding("cyberland.demo/move_y", new InputBinding(InputControl.Keyboard(Key.Up), +1f));
        context.AddDefaultInputBinding("cyberland.demo/toggle_velocity_damp", new InputBinding(InputControl.Keyboard(Key.F9)));
    }
}
