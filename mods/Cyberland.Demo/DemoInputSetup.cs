using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo;

/// <summary>
/// Central place for default keyboard actions used by <see cref="InputSystem"/>. Keeping bindings here (instead of scattering
/// magic strings) matches how larger mods isolate control schemes before layering rebinding UI.
/// </summary>
public static class DemoInputSetup
{
    /// <summary>
    /// Seeds <c>cyberland.demo/*</c> actions next to the host’s baseline table. User overrides in <c>input-bindings.json</c> replace
    /// these defaults at startup when keys collide with user preferences.
    /// </summary>
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
