using Cyberland.Engine.Assets;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Core.Ecs;
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
    /// <param name="uiCommands">Optional queue override (tests may supply an <see cref="IUiCommandQueue"/> that exercises edge cases).</param>
    public GameHostServices(IUiCommandQueue? uiCommands = null)
    {
        Fonts = new FontLibrary();
        BuiltinFonts.AddTo(Fonts);
        TextGlyphCache = new TextGlyphCache();
        BakedMsdfAtlasLoader = new BakedMsdfAtlasLoader();
        UiDocuments = new UiDocumentRegistry();
        UiCommands = uiCommands ?? new UiCommandQueue();
        StartupProgress = new StartupProgressTracker();
    }

    /// <summary>Localized strings + media resolution; set by the host before <see cref="Modding.ModLoader.LoadAll"/>.</summary>
    public ILocalizedContent? LocalizedContent { get; set; }

    /// <summary>Built-in UI/mono fonts for <see cref="Scene.Systems.TextRenderSystem"/> and <see cref="TextRenderer"/>.</summary>
    public FontLibrary Fonts { get; }

    /// <summary>Shared glyph atlas for bitmap text (thread-safe internal locking).</summary>
    public TextGlyphCache TextGlyphCache { get; }

    /// <summary>Loader that seeds baked MSDF atlas pages/glyph entries before runtime fallback generation.</summary>
    internal BakedMsdfAtlasLoader BakedMsdfAtlasLoader { get; }

    /// <summary>Maps ECS entities to retained UI documents processed by <see cref="Scene.Systems.UiDocumentFrameSystem"/>.</summary>
    public UiDocumentRegistry UiDocuments { get; }

    /// <summary>Queued gameplay intents produced during UI dispatch; drained by <see cref="Scene.Systems.UiCommandDrainSystem"/>.</summary>
    public IUiCommandQueue UiCommands { get; }

    /// <summary>
    /// Shared weighted startup progress tracker used by host bootstrap and mod load phases.
    /// </summary>
    public StartupProgressTracker StartupProgress { get; }

    /// <summary>
    /// Root session clock (time scale, pause). Additive scene worlds must not advance this; read <see cref="GlobalSessionClock.SessionSeconds"/>.
    /// </summary>
    public GlobalSessionClock SessionClock { get; } = new();

    /// <summary>In-game scene load progress keyed independently from <see cref="StartupProgress"/>.</summary>
    public InGameLoadProgressTracker InGameLoadProgress { get; } = new();

    private SceneRuntime? _runtimeScenes;
    private UiRuntime? _runtimeUi;

    /// <summary>Runtime scene stack; null until <see cref="InitializeRuntimeScenes"/> runs.</summary>
    public SceneRuntime? RuntimeScenes => _runtimeScenes;

    /// <summary>Same as <see cref="RuntimeScenes"/> as interface reference.</summary>
    public ISceneRuntime? Scenes => _runtimeScenes;

    /// <summary>Runtime UI JSON loader; null until <see cref="InitializeRuntimeUi"/> runs.</summary>
    public UiRuntime? RuntimeUi => _runtimeUi;

    /// <summary>Same as <see cref="RuntimeUi"/> as interface reference.</summary>
    public IUiRuntime? Ui => _runtimeUi;

    /// <summary>
    /// Wires the root ECS world pair and constructs additive scene services (call once during host bootstrap).
    /// </summary>
    public void InitializeRuntimeScenes(
        VirtualFileSystem vfs,
        ParallelismSettings parallelism,
        Func<ILocalizedContent?> getLocalized,
        World rootWorld,
        SystemScheduler rootScheduler)
    {
        ArgumentNullException.ThrowIfNull(vfs);
        ArgumentNullException.ThrowIfNull(parallelism);
        ArgumentNullException.ThrowIfNull(getLocalized);
        ArgumentNullException.ThrowIfNull(rootWorld);
        ArgumentNullException.ThrowIfNull(rootScheduler);
        _runtimeScenes = new SceneRuntime(this, vfs, parallelism, getLocalized);
        _runtimeScenes.InitializeRoot(rootWorld, rootScheduler);
    }

    /// <summary>Wires UI JSON loading (call once when <see cref="Renderer"/> is available).</summary>
    public void InitializeRuntimeUi(
        VirtualFileSystem vfs,
        IRenderer renderer,
        Func<ILocalizedContent?> getLocalized)
    {
        ArgumentNullException.ThrowIfNull(vfs);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(getLocalized);
        _runtimeUi = new UiRuntime(vfs, getLocalized);
        _runtimeUi.SetRenderer(renderer);
    }

    /// <summary>Optional hook invoked once per dequeued command after UI input runs on the render tick.</summary>
    public Action<IUiCommand>? UiCommandDispatcher { get; set; }

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

    /// <summary>
    /// Ensures the host finished wiring core runtime services before systems or mods execute.
    /// Violations indicate host bootstrap ordering bugs and should fail fast.
    /// </summary>
    public void EnsureCoreServicesReady()
    {
        if (Renderer is null)
            throw new InvalidOperationException("Host bootstrap invariant violated: GameHostServices.Renderer must be assigned before systems execute.");
        if (Input is null)
            throw new InvalidOperationException("Host bootstrap invariant violated: GameHostServices.Input must be assigned before systems execute.");
        if (CameraRuntimeState.Equals(default(CameraRuntimeState)))
            throw new InvalidOperationException("Host bootstrap invariant violated: GameHostServices.CameraRuntimeState must be assigned before systems execute.");
    }

}
