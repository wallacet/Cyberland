using Cyberland.Engine.Input;
using Cyberland.Engine.Modding;
using Silk.NET.Input;

namespace Cyberland.Demo.Rts;

/// <summary>Default axis bindings for camera pan (WASD + arrows) and mouse-wheel zoom.</summary>
public static class RtsInputSetup
{
    public static void RegisterDefaultBindings(ModLoadContext context)
    {
        context.AddDefaultInputBinding("cyberland.demo.rts/pan_x", new InputBinding(InputControl.Keyboard(Key.A), -1f));
        context.AddDefaultInputBinding("cyberland.demo.rts/pan_x", new InputBinding(InputControl.Keyboard(Key.Left), -1f));
        context.AddDefaultInputBinding("cyberland.demo.rts/pan_x", new InputBinding(InputControl.Keyboard(Key.D), +1f));
        context.AddDefaultInputBinding("cyberland.demo.rts/pan_x", new InputBinding(InputControl.Keyboard(Key.Right), +1f));
        context.AddDefaultInputBinding("cyberland.demo.rts/pan_y", new InputBinding(InputControl.Keyboard(Key.S), -1f));
        context.AddDefaultInputBinding("cyberland.demo.rts/pan_y", new InputBinding(InputControl.Keyboard(Key.Down), -1f));
        context.AddDefaultInputBinding("cyberland.demo.rts/pan_y", new InputBinding(InputControl.Keyboard(Key.W), +1f));
        context.AddDefaultInputBinding("cyberland.demo.rts/pan_y", new InputBinding(InputControl.Keyboard(Key.Up), +1f));
        context.AddDefaultInputBinding("cyberland.demo.rts/zoom", new InputBinding(InputControl.MouseAxisControl(MouseAxis.WheelY)));
    }
}
