using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene2D;
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

    /// <summary>2D submit API; concrete type is <see cref="VulkanRenderer"/> in the shipping host.</summary>
    public IRenderer? Renderer { get; set; }

    public IInputContext? Input { get; set; }

    /// <summary>Optional tile index storage for <see cref="Scene2D.Systems.TilemapRenderSystem"/>.</summary>
    public ITilemapDataStore? Tilemaps { get; set; }

    /// <summary>Optional CPU particle buffers for <see cref="Scene2D.Systems.ParticleSimulationSystem"/>.</summary>
    public ParticleStore? Particles { get; set; }
}
