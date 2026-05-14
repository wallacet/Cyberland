using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Audio;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Cyberland.Engine;

/// <summary>
/// Boots the game: creates the window, initializes graphics/input, presents an initial frame,
/// then completes deferred host IO + mod load before running the per-frame <see cref="Core.Tasks.SystemScheduler"/>.
/// Shipped gameplay lives in mod assemblies, not here.
/// </summary>
/// <remarks>
/// <para>
/// Typical flow: construct <c>new GameApplication(args)</c>, call <see cref="Run"/> (blocking until the window closes), then <see cref="Dispose"/>.
/// The host wires <see cref="Hosting.GameHostServices.Renderer"/> and <see cref="Hosting.GameHostServices.Input"/> during early bootstrap.
/// Mods are loaded later, after first present, so <see cref="Modding.ModLoadContext"/> still sees fully initialized host services while cold-start first paint stays responsive.
/// </para>
/// <para>
/// Scheduler registration order is defined by mod load order. Shipped engine mods call
/// <see cref="EngineDefaultSchedulerSystems.RegisterBeforeGameplayMods"/> and
/// <see cref="EngineDefaultSchedulerSystems.RegisterAfterGameplayMods"/> around gameplay mods.
/// </para>
/// <para>
/// <see cref="Core.Tasks.SystemScheduler.RunFrame(Cyberland.Engine.Core.Ecs.World, float)"/> runs once per window <strong>Render</strong> tick (not per <strong>Update</strong>) after boot reaches <c>FullGame</c>, with <c>deltaSeconds</c> equal to Silk’s <c>Render</c> callback argument (the render stopwatch interval from <c>DoRender</c>). Avoid a <c>minimum</c> frame duration clamp: at high refresh + mailbox, real intervals can fall below 2 ms; a floor would add fake time every tick and make fixed-step sim run faster than wall clock. Only cap large hitches (max) to keep the accumulator bounded. Silk may call <strong>Update</strong> more often than <strong>Render</strong>; running the full ECS from <strong>Update</strong> advanced gameplay multiple times per presented frame and made HUD frame timing misleading.
/// </para>
/// </remarks>
/// <example>
/// <code lang="csharp">
/// using var app = new GameApplication(args);
/// app.Run(); // blocks
/// </code>
/// </example>
[ExcludeFromCodeCoverage(Justification = "Requires a real window, input, and mod staging; covered by manual / integration runs.")]
public sealed class GameApplication : IDisposable
{
    private enum GameBootPhase
    {
        ColdStart,
        MinimalPresenting,
        BuildingFullVk,
        DeferHostIo,
        LoadingMods,
        FullGame,
        Failed
    }

    private readonly ParallelismSettings _parallelism = new();
    private readonly VirtualFileSystem _vfs = new();
    private readonly World _world = new();
    private readonly SystemScheduler _scheduler;
    private readonly ModLoader _mods = new();
    private readonly LocalizationManager _localization = new();
    private readonly GameHostServices _host;
    private readonly string[] _commandLineArgs;
    private ILocalizedContent? _localizedContent;
    private OpenALAudioDevice? _audio;
    private VulkanRenderer? _renderer;
    private IWindow? _window;
    private SilkInputService? _input;

    private readonly double? _profileWallSeconds;
#if DEBUG
    private readonly string? _profileDumpPath;
#endif
    private readonly string? _perfDumpPath;
    private Stopwatch? _profileWall;
    private bool _profileWallStarted;
    private bool _profileCloseRequested;
    private int _profilePresentedFrames;
    private Stopwatch? _startupWall;
    private bool _firstPresentLogged;
    private bool _audioInitAttempted;
    private bool _audioInitLogged;
    private Task<OpenALAudioDevice?>? _pendingAudioInitTask;
    private CancellationTokenSource? _audioInitCts;
    private double _startupLoadCallbackMs;
    private double _startupFirstPresentMs;
    private int _bakedGlyphImports;
    private readonly int _bakedAtlasPageBudget;
    private TextGlyphCache.GlyphCacheTelemetry _lastGlyphTelemetry;
    private GameBootPhase _bootPhase = GameBootPhase.ColdStart;
    private Stopwatch? _startupStageSw;
    private IReadOnlySet<string>? _excludedModIds;
    private bool _startupLoadMilestoneLogged;
    private Task? _pendingBindingsLoadTask;
    private Task? _pendingBindingsSaveTask;
    private bool _bindingsStageLogged;
    private bool _deferredLocalizationReady;
    private Stopwatch? _bindingsLoadSw;
#if DEBUG
    private bool _profileHudInTitle;
    private int _profileTitleThrottle;
#endif

    /// <summary>
    /// Prepares host services and the frame scheduler. Does not open the window until <see cref="Run"/> is called.
    /// </summary>
    /// <param name="commandLineArgs">Optional argv (e.g. for <c>--exclude-mods</c> parsed by <see cref="Modding.ExcludeModsParser"/>).</param>
    public GameApplication(string[]? commandLineArgs = null)
    {
        _commandLineArgs = commandLineArgs ?? Array.Empty<string>();
#if DEBUG
        FrameProfiler.ApplyEnvironmentDefaults();
        TextGlyphCache.EnableMsdfFallbackConsoleWarnings = true;
        _profileDumpPath = ProfileCommandLine.TryParseProfileDump(_commandLineArgs);
        // Per-scope allocation sampling uses GC.GetAllocatedBytesForCurrentThread on every Push/Pop — far too
        // expensive for high-FPS timing dumps. Opt in via --profile-alloc or CYBERLAND_FRAME_PROFILER_TRACK_ALLOC.
        if (ProfileCommandLine.TryParseProfileAlloc(_commandLineArgs))
            FrameProfiler.TrackSessionAllocations = true;
#else
        if (!string.IsNullOrEmpty(ProfileCommandLine.TryParseProfileDump(_commandLineArgs)))
        {
            Console.Error.WriteLine(
                "Cyberland: --profile-dump is ignored in Release builds (hierarchical frame profiler is Debug-only).");
        }
#endif
        _profileWallSeconds = ProfileCommandLine.TryParseProfileSeconds(_commandLineArgs);
        _perfDumpPath = ProfileCommandLine.TryParsePerfDump(_commandLineArgs);
        _bakedAtlasPageBudget = ParseBakedAtlasPageBudget();
        UiLayoutGating.ApplyEnvironmentDefaults();
        _scheduler = new SystemScheduler(_parallelism);
        _host = new GameHostServices();
    }

    /// <summary>
    /// Creates the main window and runs the Silk.NET message loop until exit. Blocking on the calling thread.
    /// </summary>
    public void Run()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "Cyberland";

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClosing;

        _window.Run();
    }

    private void OnLoad()
    {
        if (_window is null)
            return;
        _startupWall = Stopwatch.StartNew();
        _startupStageSw = Stopwatch.StartNew();
        _firstPresentLogged = false;
        _startupLoadMilestoneLogged = false;
        _bootPhase = GameBootPhase.ColdStart;
        _pendingBindingsLoadTask = null;
        _pendingBindingsSaveTask = null;
        _bindingsStageLogged = false;
        _deferredLocalizationReady = false;
        _bindingsLoadSw = null;
        _audioInitLogged = false;
        _pendingAudioInitTask = null;
        _audioInitCts?.Dispose();
        _audioInitCts = null;

#if DEBUG
        Console.WriteLine(
            $"Cyberland startup | Configuration=Debug | FrameProfilerEnabled={FrameProfiler.IsEnabled} | FrameProfilerTrackAlloc={FrameProfiler.TrackSessionAllocations} | UiIncremental={UiLayoutGating.UseIncrementalDocumentFrames} | BakedAtlasPageBudget={_bakedAtlasPageBudget}");
#else
        Console.WriteLine(
            $"Cyberland startup | Configuration=Release | UiIncremental={UiLayoutGating.UseIncrementalDocumentFrames} | BakedAtlasPageBudget={_bakedAtlasPageBudget}");
#endif

        // Bootstrap target: first present should happen before disk-heavy host IO and before mod load.
        // OnLoad does minimal setup, then OnRender advances remaining stages in order.
        try
        {
            InitializeMinimalPresentBootstrap();
            _excludedModIds = ParseExcludedModIds(_commandLineArgs);
            _bootPhase = GameBootPhase.MinimalPresenting;
        }
        catch (GraphicsInitializationException ex)
        {
            HandleGraphicsInitializationFailure(ex);
        }
        catch (Exception ex)
        {
            HandleStartupFailure(ex);
        }
    }

    private void InitializeMinimalPresentBootstrap()
    {
        if (_window is null)
            return;

        _audioInitAttempted = false;
        StartAudioInitializationInBackground();
        _renderer = new VulkanRenderer(_window);
        _renderer.InitializeMinimal();
        LogStartupStage("renderer.initialize.minimal");

        EngineDiagnostics.UseNativeUserNotifications();
        LogStartupStage("audio.initialize.parallel.start");

        if (_profileWallSeconds is not null && _renderer is not null)
        {
            _renderer.FramePacing = new FramePacing(FramePacingMode.Unlimited);
#if DEBUG
            FrameProfiler.ResetSession();
            FrameProfiler.ConfigureWarmup(TimeSpan.FromSeconds(1));
#endif
            _profileWall = new Stopwatch();
            _profileWallStarted = false;
        }
    }

    private void AdvanceBootstrapAfterFirstPresent()
    {
        switch (_bootPhase)
        {
            case GameBootPhase.MinimalPresenting:
                _bootPhase = GameBootPhase.BuildingFullVk;
                break;
            case GameBootPhase.BuildingFullVk:
                CompleteDeferredRendererAndHostInitialization();
                _bootPhase = GameBootPhase.DeferHostIo;
                break;
            case GameBootPhase.DeferHostIo:
                if (TryRunDeferredHostIo())
                    _bootPhase = GameBootPhase.LoadingMods;
                break;
            case GameBootPhase.LoadingMods:
                RunDeferredModLoadAndFinalize();
                _bootPhase = GameBootPhase.FullGame;
                break;
            case GameBootPhase.FullGame:
            case GameBootPhase.Failed:
            case GameBootPhase.ColdStart:
            default:
                break;
        }
    }

    private void CompleteDeferredRendererAndHostInitialization()
    {
        if (_window is null || _renderer is null)
            throw new InvalidOperationException("Renderer/window must exist before deferred renderer completion.");

        _renderer.CompleteDeferredInitialization();
        LogStartupStage("renderer.initialize.deferred_full");

        _input = new SilkInputService(_window.CreateInput(), _renderer, _host);
        _host.Renderer = _renderer;
        _host.Input = _input;
        _host.Tilemaps ??= new TilemapDataStore();
        _host.CameraRuntimeState = Hosting.CameraRuntimeState.CreateDefault(_renderer.SwapchainPixelSize);
        _host.TextGlyphCache.UseAsyncRasterization = true;
        _host.EnsureCoreServicesReady();
        _renderer.RequestClose = () => _window?.Close();
        LogStartupStage("host.services");

        EngineDefaultGlobalPostProcess.Apply(_renderer);
        LogStartupStage("renderer.postprocess-defaults");
    }

    private bool TryRunDeferredHostIo()
    {
        if (_input is null)
            throw new InvalidOperationException("Input service must be initialized before deferred host IO.");

        if (!_deferredLocalizationReady)
        {
            var languageFile = Path.Combine(AppContext.BaseDirectory, "language.json");
            var primaryCulture = LanguagePreference.Resolve(_commandLineArgs, languageFile);
            _localizedContent = new LocalizedContent(_localization, _vfs, primaryCulture);
            _host.LocalizedContent = _localizedContent;
            _deferredLocalizationReady = true;
            LogStartupStage("localization.bootstrap");
        }

        if (_pendingBindingsLoadTask is null)
        {
            var bindingsFile = Path.Combine(AppContext.BaseDirectory, "input-bindings.json");
            if (!File.Exists(bindingsFile))
            {
                // Cold-start fast path: avoid blocking first gameplay frames on writing a brand-new defaults file.
                // Mods still append defaults later during LoadAll through ModLoadContext.
                _input.Bindings.LoadDefaults();
                _pendingBindingsSaveTask = _input.Bindings.SaveAsync(bindingsFile);
                _bindingsStageLogged = true;
                LogStartupStage("input.bindings.load");
            }
            else
            {
                _bindingsLoadSw = Stopwatch.StartNew();
                _pendingBindingsLoadTask = _input.Bindings.LoadOrCreateUserFileAsync(bindingsFile);
                return false;
            }
        }

        if (_pendingBindingsLoadTask is not null)
        {
            if (!_pendingBindingsLoadTask.IsCompleted)
                return false;

            _pendingBindingsLoadTask.GetAwaiter().GetResult();
            _pendingBindingsLoadTask = null;
            _bindingsLoadSw?.Stop();
            if (!_bindingsStageLogged)
            {
                _bindingsStageLogged = true;
                LogStartupStage("input.bindings.load");
            }
        }

        return true;
    }

    private void RunDeferredModLoadAndFinalize()
    {
        if (_localizedContent is null)
            throw new InvalidOperationException("Localized content must be initialized before deferred mod load.");
        if (_renderer is null)
            throw new InvalidOperationException("Renderer must be initialized before deferred mod load.");

        _scheduler.BeginDeferExecutionOrderRebuilds();
        try
        {
            _mods.LoadAll(
                Path.Combine(AppContext.BaseDirectory, "Mods"),
                _vfs,
                _localizedContent,
                _world,
                _scheduler,
                _host,
                _excludedModIds);
            LogStartupStage("mods.load_all");
            LogModLoadTiming(_mods.LastLoadTiming);
            PreloadAllBuiltinAtlasesIfRequested(_renderer);
            LogStartupStage("fonts.preload-builtins");
            LogStartupStage("scheduler.register-systems");
        }
        finally
        {
            _scheduler.EndDeferExecutionOrderRebuilds();
        }

        LogStartupStage("scheduler.rebuild-order");

        _localizedContent.MergeStringTable("strings.json");
        LogStartupStage("localization.merge-strings");
        if (_startupWall is not null)
            _startupLoadCallbackMs = _startupWall.Elapsed.TotalMilliseconds;
        if (!_startupLoadMilestoneLogged)
        {
            _startupLoadMilestoneLogged = true;
            _startupWall?.Stop();
            Console.WriteLine(
                $"Startup milestone | load_callback_total_ms={_startupLoadCallbackMs.ToString("0.###", CultureInfo.InvariantCulture)}");
        }
    }

    private void OnUpdate(double delta)
    {
        // Intentionally empty: Silk may invoke Update more often than Render. Running RunFrame here caused multiple ECS
        // passes per draw (choppy motion) and tiny per-tick deltas (inflated FPS if used for HUD).
        _ = delta;
    }

    private void OnRender(double silkRenderDelta)
    {
        if (_window is null)
            return;
        if (_renderer is null)
            return;

#if DEBUG
        using var __frame = FrameProfilerScope.Enter("OnRender.Total");
#endif

        // Same value Silk computed in DoRender from the render stopwatch (interval since the previous successful Render).
        float dt = (float)silkRenderDelta;
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f)
            dt = 0f;

        // No positive minimum: real dt can be under 2 ms at high refresh + low-latency present; clamping up to 1/500 s
        // made each tick contribute at least 2 ms to the fixed accumulator → simulation faster than wall time (~2× near ~1 kHz ticks).
        if (dt > 0f)
            dt = Math.Min(dt, 0.25f);

        // Render tick order (see IRenderer remarks): discard stale CPU submits first so a missed DrawFrame cannot merge
        // with this tick's Submit*, then simulate + submit sprites/lights/camera, then encode/present.
        {
            _bakedGlyphImports += _host.BakedMsdfAtlasLoader.DrainPendingUploads(_renderer, result =>
            {
                if (!result.Loaded)
                {
                    Console.WriteLine(
                        $"Baked atlas load | source={result.ManifestPath} skipped={result.Message}");
                }
            });
            _host.TextGlyphCache.DrainPendingGlyphUploads(_renderer);
        }

        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Game.ResetPendingSubmissions");
#endif
            _renderer?.ResetPendingSubmissionsForNewTick();
        }

        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Input.BeginFrame");
#endif
            _host.Input?.BeginFrame();
        }

        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Scheduler.RunFrame");
#endif
            // Publish fixed-step remainder before ILateUpdate so visual extrapolation (e.g. pos + vel * acc) uses this frame's alpha.
            if (_bootPhase == GameBootPhase.FullGame)
                _scheduler.RunFrame(_world, dt, acc => _host.FixedAccumulatorSeconds = acc);
        }

        _host.FixedDeltaSeconds = _scheduler.FixedDeltaSeconds;

        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Vulkan.DrawFrame");
#endif
            if (_bootPhase == GameBootPhase.FullGame)
                _renderer?.DrawFrame();
            else
                _renderer?.DrawBootstrapFrame();
        }

        _host.LastPresentDeltaSeconds = dt;
        if (!_firstPresentLogged && _startupWall is not null)
        {
            _firstPresentLogged = true;
            _startupFirstPresentMs = _startupWall.Elapsed.TotalMilliseconds;
            Console.WriteLine(
                $"Startup milestone | first_present_ms={_startupFirstPresentMs.ToString("0.###", CultureInfo.InvariantCulture)}");
            TryInitializeAudioAfterFirstPresent();
        }

        if (_firstPresentLogged &&
            _bootPhase != GameBootPhase.FullGame &&
            _bootPhase != GameBootPhase.Failed)
        {
            try
            {
                AdvanceBootstrapAfterFirstPresent();
            }
            catch (Exception ex)
            {
                HandleStartupFailure(ex);
                return;
            }
        }

        TryFinalizeBackgroundAudioInitialization();

        if (_profileWallSeconds is not null && _profileWall is not null)
        {
            if (!_profileWallStarted)
            {
                _profileWallStarted = true;
                _profileWall.Start();
            }
            _profilePresentedFrames++;
#if DEBUG
            FrameProfiler.MarkFrame();
#endif
        }

        if (_profileWallSeconds is not null && _profileWall is not null && !_profileCloseRequested &&
            _profileWall.Elapsed.TotalSeconds >= _profileWallSeconds.Value)
        {
            _profileCloseRequested = true;
            _renderer?.RequestClose?.Invoke();
        }

#if DEBUG
        if (_input is not null && _window is not null)
        {
            if (_input.HasActionPressedThisFrame("cyberland.engine/profile-hud"))
                _profileHudInTitle = !_profileHudInTitle;
            if (_profileHudInTitle && ++_profileTitleThrottle >= 30)
            {
                _profileTitleThrottle = 0;
                var sb = new System.Text.StringBuilder(256);
                FrameProfiler.AppendTopScopes(sb, 6);
                _window.Title = "Cyberland | " + sb.ToString().Replace('\n', ' ');
            }
        }
#endif
    }

    private void OnClosing()
    {
        _lastGlyphTelemetry = TextGlyphCache.SnapshotAndResetTelemetry();
        var missCodepoints = TextGlyphCache.SnapshotAndResetMissCodepointSummary();
        var missGlyphKeys = TextGlyphCache.SnapshotAndResetMissGlyphKeySummary();
        if (_profileWallSeconds is not null && _profileWall is not null)
        {
            var wallSeconds = _profileWall.Elapsed.TotalSeconds;
            var fps = wallSeconds > 0d ? _profilePresentedFrames / wallSeconds : 0d;
            Console.WriteLine(
                $"Perf summary | frames={_profilePresentedFrames} wallSeconds={wallSeconds.ToString("0.###", CultureInfo.InvariantCulture)} fps={fps.ToString("0.###", CultureInfo.InvariantCulture)}");
            if (!string.IsNullOrWhiteSpace(_perfDumpPath))
                WritePerfDump(_perfDumpPath, _profilePresentedFrames, wallSeconds, fps, _startupLoadCallbackMs, _startupFirstPresentMs, _lastGlyphTelemetry, _bakedGlyphImports);
        }
        Console.WriteLine(
            $"Glyph cache telemetry | hits={_lastGlyphTelemetry.CacheHits} misses={_lastGlyphTelemetry.CacheMisses} baked_imports={_bakedGlyphImports} raster_ms={_lastGlyphTelemetry.RasterizeMs.ToString("0.###", CultureInfo.InvariantCulture)} uploads={_lastGlyphTelemetry.UploadCalls} upload_mb={(_lastGlyphTelemetry.UploadBytes / (1024d * 1024d)).ToString("0.###", CultureInfo.InvariantCulture)} upload_ms={_lastGlyphTelemetry.UploadMs.ToString("0.###", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Glyph miss codepoints | {missCodepoints}");
        Console.WriteLine($"Glyph miss keys | {missGlyphKeys}");
#if DEBUG
        if (!string.IsNullOrEmpty(_profileDumpPath))
            FrameProfiler.WriteDump(_profileDumpPath);
#endif
        _mods.UnloadAll();
    }

    private static IReadOnlySet<string>? ParseExcludedModIds(string[] commandLineArgs)
    {
        var excluded = ExcludeModsParser.TryParse(commandLineArgs);
        if (excluded is null)
            return null;

        return new HashSet<string>(excluded, StringComparer.OrdinalIgnoreCase);
    }

    private void HandleGraphicsInitializationFailure(GraphicsInitializationException ex)
    {
        UserMessageDialog.ShowError("Cyberland — Graphics unavailable", ex.UserMessage);
        _renderer?.Dispose();
        _renderer = null;
        _bootPhase = GameBootPhase.Failed;
        _window?.Close();
    }

    private void HandleStartupFailure(Exception ex)
    {
        Console.Error.WriteLine($"Cyberland startup failure: {ex}");
        try
        {
            _mods.UnloadAll();
        }
        catch
        {
        }

        UserMessageDialog.ShowError("Cyberland — Startup failed", ex.Message);
        _renderer?.Dispose();
        _renderer = null;
        _bootPhase = GameBootPhase.Failed;
        _window?.Close();
    }

    private static void WritePerfDump(
        string path,
        int frames,
        double wallSeconds,
        double fps,
        double startupLoadCallbackMs,
        double startupFirstPresentMs,
        TextGlyphCache.GlyphCacheTelemetry glyphTelemetry,
        int bakedGlyphImports)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder(128);
        sb.Append("frames=").Append(frames.ToString(CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("wallSeconds=").Append(wallSeconds.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("fps=").Append(fps.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("startupLoadCallbackMs=").Append(startupLoadCallbackMs.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("startupFirstPresentMs=").Append(startupFirstPresentMs.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("glyphCacheHits=").Append(glyphTelemetry.CacheHits.ToString(CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("glyphCacheMisses=").Append(glyphTelemetry.CacheMisses.ToString(CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("glyphBakedImports=").Append(bakedGlyphImports.ToString(CultureInfo.InvariantCulture)).AppendLine();
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private void LogStartupStage(string stageName)
    {
        if (_startupStageSw is null)
            return;

        var startupStageSw = _startupStageSw;
        startupStageSw.Stop();
        Console.WriteLine(
            $"Startup stage | {stageName}={startupStageSw.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}ms");
        startupStageSw.Restart();
    }

    private void TryInitializeAudioAfterFirstPresent()
    {
        if (_audioInitAttempted)
            return;
        _audioInitAttempted = true;
        // Audio boot already starts in parallel with renderer bootstrap. Keep this method as the lifecycle gate
        // that ensures completion logging starts no earlier than first present.
        TryFinalizeBackgroundAudioInitialization();
    }

    private void StartAudioInitializationInBackground()
    {
        if (_pendingAudioInitTask is not null)
            return;

        _audioInitCts = new CancellationTokenSource();
        var token = _audioInitCts.Token;
        _pendingAudioInitTask = Task.Run(() =>
        {
            if (token.IsCancellationRequested)
                return null;
            try
            {
                return new OpenALAudioDevice();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine($"Cyberland: audio initialization failed ({ex.GetType().Name}): {ex.Message}");
#else
                _ = ex;
#endif
                return null;
            }
        }, token);
    }

    private void TryFinalizeBackgroundAudioInitialization()
    {
        if (_audioInitLogged || !_audioInitAttempted)
            return;
        if (_pendingAudioInitTask is null || !_pendingAudioInitTask.IsCompleted)
            return;

        var sw = Stopwatch.StartNew();
        try
        {
            _audio = _pendingAudioInitTask.GetAwaiter().GetResult();
        }
        finally
        {
            sw.Stop();
            _audioInitLogged = true;
            Console.WriteLine(
                $"Startup stage | audio.initialize.parallel.complete={sw.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}ms");
        }
    }

    private static void LogModLoadTiming(ModLoader.ModLoadTiming? timing)
    {
        if (timing is null)
            return;

        Console.WriteLine(
            $"Mod load timing | manifests={timing.ManifestCount} loaded={timing.LoadedModCount} total={timing.TotalMs.ToString("0.###", CultureInfo.InvariantCulture)}ms parse={timing.ParseManifestsMs.ToString("0.###", CultureInfo.InvariantCulture)}ms mount={timing.MountContentMs.ToString("0.###", CultureInfo.InvariantCulture)}ms load={timing.LoadAssembliesAndModsMs.ToString("0.###", CultureInfo.InvariantCulture)}ms");
        foreach (var entry in timing.Entries)
        {
            Console.WriteLine(
                $"Mod load entry | id={entry.ModId} asm={entry.AssemblyLoadMs.ToString("0.###", CultureInfo.InvariantCulture)}ms type={entry.TypeResolveMs.ToString("0.###", CultureInfo.InvariantCulture)}ms onload={entry.OnLoadMs.ToString("0.###", CultureInfo.InvariantCulture)}ms");
        }
    }

    private void PreloadAllBuiltinAtlasesIfRequested(IRenderer renderer)
    {
        var raw = Environment.GetEnvironmentVariable("CYBERLAND_PRELOAD_ALL_ENGINE_FONT_ATLASES");
        if (!IsTruthy(raw))
            return;
        if (_bakedAtlasPageBudget <= 0)
        {
            Console.WriteLine("Baked atlas preload | skipped (page budget <= 0)");
            return;
        }

        try
        {
            var assets = new AssetManager(_vfs);
            foreach (var manifestPath in BuiltinFonts.EnumerateBakedAtlasManifestPaths())
            {
                var result = _host.BakedMsdfAtlasLoader.LoadFromPath(
                    assets,
                    renderer,
                    _host.TextGlyphCache,
                    manifestPath,
                    _bakedAtlasPageBudget);
                if (result.Loaded)
                {
                    _bakedGlyphImports += result.GlyphCount;
                    Console.WriteLine(
                        $"Baked atlas load | source={result.ManifestPath} glyphs={result.GlyphCount} pages={result.PageCount}");
                }
                else
                {
                    Console.WriteLine(
                        $"Baked atlas load | source={result.ManifestPath} skipped={result.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Baked atlas load | skipped due to exception: {ex.Message}");
        }
    }

    private static bool IsTruthy(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) &&
        (raw.Equals("1", StringComparison.OrdinalIgnoreCase) ||
         raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
         raw.Equals("on", StringComparison.OrdinalIgnoreCase));

    private static int ParseBakedAtlasPageBudget()
    {
        var raw = Environment.GetEnvironmentVariable("CYBERLAND_BAKED_ATLAS_PAGE_BUDGET");
        // Default must cover the largest shipped builtin atlas (UiSans 22–24px use three 2048² pages); otherwise
        // glyphs on later pages (including punctuation packed after Latin) never load and the cache misses at runtime.
        if (string.IsNullOrWhiteSpace(raw))
            return 4;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return Math.Max(0, parsed);
        return 4;
    }

    /// <summary>
    /// Releases GPU, audio, and window resources. Safe to call once after <see cref="Run"/> returns.
    /// </summary>
    public void Dispose()
    {
        _audioInitCts?.Cancel();
        _audioInitCts?.Dispose();
        _audioInitCts = null;
        if (_audio is null && _pendingAudioInitTask is not null && _pendingAudioInitTask.IsCompletedSuccessfully)
            _audio = _pendingAudioInitTask.Result;
        _input?.Dispose();
        _host.TextGlyphCache.Shutdown();
        _renderer?.Dispose();
        _audio?.Dispose();
        _window?.Dispose();
    }
}
