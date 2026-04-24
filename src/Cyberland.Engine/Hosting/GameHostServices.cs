using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
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
/// <para>
/// Each render tick, the stock host sets <see cref="FixedAccumulatorSeconds"/> from the scheduler&apos;s fixed-step remainder
/// <strong>before</strong> <see cref="Core.Ecs.ILateUpdate"/> runs, and sets <see cref="FixedDeltaSeconds"/> after the scheduler finishes a frame.
/// </para>
/// </remarks>
public sealed class GameHostServices
{
    /// <summary>
    /// Creates services with user key bindings (loaded from disk by the host after startup).
    /// </summary>
    /// <param name="keyBindings">Shared store for action → key mappings.</param>
    public GameHostServices(KeyBindingStore keyBindings)
    {
        KeyBindings = keyBindings;
        Fonts = new FontLibrary();
        BuiltinFonts.AddTo(Fonts);
        TextGlyphCache = new TextGlyphCache();
    }

    /// <summary>User-editable bindings (e.g. <c>keybindings.json</c>).</summary>
    public KeyBindingStore KeyBindings { get; }

    /// <summary>Localized strings + media resolution; set by the host before <see cref="Modding.ModLoader.LoadAll"/>.</summary>
    public ILocalizedContent? LocalizedContent { get; set; }

    /// <summary>Built-in UI/mono fonts for <see cref="Scene.Systems.TextRenderSystem"/> and <see cref="TextRenderer"/>.</summary>
    public FontLibrary Fonts { get; }

    /// <summary>Shared glyph atlas for bitmap text (thread-safe internal locking).</summary>
    public TextGlyphCache TextGlyphCache { get; }

    /// <summary>
    /// Draw and lighting submit API. In the stock host this is a <see cref="Rendering.VulkanRenderer"/>; depend on <see cref="Rendering.IRenderer"/> in mods.
    /// </summary>
    public IRenderer? Renderer { get; set; }

    /// <summary>Silk.NET input polled from the main window; null until the host assigns it.</summary>
    public IInputContext? Input { get; set; }

    /// <summary>Optional backing store for <see cref="Cyberland.Engine.Scene.Tilemap"/> grid data used by <see cref="Cyberland.Engine.Scene.Systems.TilemapRenderSystem"/>.</summary>
    public ITilemapDataStore? Tilemaps { get; set; }

    /// <summary>
    /// Wall-clock seconds between successive rendered frames (set by the host after each draw). Matches the
    /// <c>deltaSeconds</c> passed to <see cref="Core.Tasks.SystemScheduler.RunFrame(Cyberland.Engine.Core.Ecs.World, float)"/> in the stock
    /// <see cref="GameApplication"/> (ECS runs once per <c>Render</c> tick).
    /// </summary>
    public float LastPresentDeltaSeconds { get; internal set; }

    /// <summary>
    /// Fixed-step accumulator remainder in seconds (same as <see cref="Core.Tasks.SystemScheduler.FixedAccumulator"/> after
    /// fixed substeps). The stock host updates this <strong>before</strong> <see cref="Core.Ecs.ILateUpdate"/> each frame. Use for optional
    /// display extrapolation: e.g. <c>position + velocity * FixedAccumulatorSeconds</c>.
    /// </summary>
    public float FixedAccumulatorSeconds { get; internal set; }

    /// <summary>
    /// Fixed timestep step size in seconds (mirrors <see cref="Core.Tasks.SystemScheduler.FixedDeltaSeconds"/> after each frame).
    /// </summary>
    public float FixedDeltaSeconds { get; internal set; } = 1f / 60f;

}
