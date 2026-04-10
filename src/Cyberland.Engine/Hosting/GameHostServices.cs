using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Input;

namespace Cyberland.Engine.Hosting;

/// <summary>
/// A small façade of host-owned services passed into every mod’s <see cref="Modding.ModLoadContext"/>.
/// Mods read <see cref="Renderer"/> / <see cref="Input"/> after the host finishes graphics and input setup.
/// </summary>
/// <remarks>
/// Do not stash gameplay singletons here—keep campaign state in your mod’s ECS components or session objects.
/// Threading: treat <see cref="Rendering.IRenderer"/> submit methods as documented on <see cref="Rendering.IRenderer"/> (safe from parallel ECS workers; recording/present stays on the window thread).
/// </remarks>
public sealed class GameHostServices
{
    /// <summary>
    /// Creates services with user key bindings (loaded from disk by the host after startup).
    /// </summary>
    /// <param name="keyBindings">Shared store for action → key mappings.</param>
    public GameHostServices(KeyBindingStore keyBindings) =>
        KeyBindings = keyBindings;

    /// <summary>User-editable bindings (e.g. <c>keybindings.json</c>).</summary>
    public KeyBindingStore KeyBindings { get; }

    /// <summary>
    /// Draw and lighting submit API. In the stock host this is a <see cref="Rendering.VulkanRenderer"/>; depend on <see cref="Rendering.IRenderer"/> in mods.
    /// </summary>
    public IRenderer? Renderer { get; set; }

    /// <summary>Silk.NET input polled from the main window; null until the host assigns it.</summary>
    public IInputContext? Input { get; set; }

    /// <summary>Optional backing store for <see cref="Cyberland.Engine.Scene.Tilemap"/> grid data used by <see cref="Cyberland.Engine.Scene.Systems.TilemapRenderSystem"/>.</summary>
    public ITilemapDataStore? Tilemaps { get; set; }

    /// <summary>Optional shared particle arenas for <see cref="Cyberland.Engine.Scene.Systems.ParticleSimulationSystem"/> / <see cref="Cyberland.Engine.Scene.Systems.ParticleRenderSystem"/>.</summary>
    public ParticleStore? Particles { get; set; }
}
