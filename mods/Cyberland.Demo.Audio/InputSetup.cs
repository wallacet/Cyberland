using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo.Audio;

/// <summary>Default key bindings for the audio demo.</summary>
public static class InputSetup
{
    /// <summary>Registers demo actions.</summary>
    public static void RegisterDefaultBindings(ModLoadContext context)
    {
        context.AddDefaultInputBinding("cyberland.demo.audio/ui_click", new InputBinding(InputControl.Keyboard(Key.U)));
        context.AddDefaultInputBinding("cyberland.demo.audio/footstep", new InputBinding(InputControl.Keyboard(Key.F)));
        context.AddDefaultInputBinding("cyberland.demo.audio/dialogue", new InputBinding(InputControl.Keyboard(Key.D)));
        context.AddDefaultInputBinding("cyberland.demo.audio/spam", new InputBinding(InputControl.Keyboard(Key.S)));
        context.AddDefaultInputBinding("cyberland.demo.audio/music_toggle", new InputBinding(InputControl.Keyboard(Key.M)));
        context.AddDefaultInputBinding("cyberland.demo.audio/cinematic", new InputBinding(InputControl.Keyboard(Key.C)));
        context.AddDefaultInputBinding("cyberland.demo.audio/listener_toggle", new InputBinding(InputControl.Keyboard(Key.L)));
        context.AddDefaultInputBinding("cyberland.demo.audio/pause_toggle", new InputBinding(InputControl.Keyboard(Key.P)));
        context.AddDefaultInputBinding("cyberland.demo.audio/move_x", new InputBinding(InputControl.Keyboard(Key.Right), +1f));
        context.AddDefaultInputBinding("cyberland.demo.audio/move_x", new InputBinding(InputControl.Keyboard(Key.Left), -1f));
        context.AddDefaultInputBinding("cyberland.demo.audio/move_y", new InputBinding(InputControl.Keyboard(Key.Up), +1f));
        context.AddDefaultInputBinding("cyberland.demo.audio/move_y", new InputBinding(InputControl.Keyboard(Key.Down), -1f));
    }
}
