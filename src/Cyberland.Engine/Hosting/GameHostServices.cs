using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using System.Diagnostics.CodeAnalysis;
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
 [ExcludeFromCodeCoverage]
public sealed class GameHostServices
{
    /// <summary>
    /// Creates host-owned services used by mods and built-in systems.
    /// </summary>
    public GameHostServices()
    {
        Fonts = new FontLibrary();
        BuiltinFonts.AddTo(Fonts);
        TextGlyphCache = new TextGlyphCache();
        UiDocuments = new UiDocumentRegistry();
        UiCommands = new UiCommandQueue();
    }

    /// <summary>Localized strings + media resolution; set by the host before <see cref="Modding.ModLoader.LoadAll"/>.</summary>
    public ILocalizedContent? LocalizedContent { get; set; }

    /// <summary>Built-in UI/mono fonts for <see cref="Scene.Systems.TextRenderSystem"/> and <see cref="TextRenderer"/>.</summary>
    public FontLibrary Fonts { get; }

    /// <summary>Shared glyph atlas for bitmap text (thread-safe internal locking).</summary>
    public TextGlyphCache TextGlyphCache { get; }

    /// <summary>Maps ECS entities to retained UI documents processed by <see cref="Scene.Systems.UiDocumentFrameSystem"/>.</summary>
    public UiDocumentRegistry UiDocuments { get; }

    /// <summary>Queued gameplay intents produced during UI dispatch; drained by <see cref="Scene.Systems.UiCommandDrainSystem"/>.</summary>
    public IUiCommandQueue UiCommands { get; }

    /// <summary>Optional hook invoked once per dequeued command after UI input runs on the render tick.</summary>
    public Action<object?>? UiCommandDispatcher { get; set; }

    /// <summary>
    /// Draw and lighting submit API. In the stock host this is a <see cref="Rendering.VulkanRenderer"/>; depend on <see cref="Rendering.IRenderer"/> in mods.
    /// Set during host bootstrap before mods run.
    /// </summary>
    public IRenderer Renderer { get; set; } = null!;

    /// <summary>
    /// Frame-stable input service populated by the host after window/input setup.
    /// Set during host bootstrap before mods run.
    /// </summary>
    public IInputService Input { get; set; } = null!;

    /// <summary>
    /// Frame-stable active camera state published by engine camera runtime systems. Gameplay/layout code should prefer
    /// this over renderer queue introspection APIs.
    /// </summary>
    public CameraRuntimeState CameraRuntimeState { get; set; }

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
