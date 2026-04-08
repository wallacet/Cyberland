using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Silk.NET.Input;

namespace Cyberland.Engine.Hosting;

/// <summary>
/// Stable host capabilities exposed to mods via <see cref="Modding.ModLoadContext"/>.
/// The host assigns <see cref="Renderer"/> and <see cref="Input"/> after the window and graphics are ready.
/// </summary>
public sealed class GameHostServices
{
    public GameHostServices(KeyBindingStore keyBindings) =>
        KeyBindings = keyBindings;

    public KeyBindingStore KeyBindings { get; }

    public VulkanRenderer? Renderer { get; set; }

    public IInputContext? Input { get; set; }
}
