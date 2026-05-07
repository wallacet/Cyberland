using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
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
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Cyberland.Engine.UI.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Cyberland.Engine;

/// <summary>
/// Boots the game: creates the window, initializes Vulkan, audio, the ECS <see cref="Core.Ecs.World"/>,
/// loads mods, and runs the per-frame <see cref="Core.Tasks.SystemScheduler"/>. Shipped gameplay lives in mod assemblies, not here.
/// </summary>
/// <remarks>
/// <para>
/// Typical flow: construct <c>new GameApplication(args)</c>, call <see cref="Run"/> (blocking until the window closes), then <see cref="Dispose"/>.
/// The host wires <see cref="Hosting.GameHostServices.Renderer"/> and <see cref="Hosting.GameHostServices.Input"/> after graphics init so mods receive a working <see cref="Modding.ModLoadContext"/>.
/// </para>
/// <para>
/// Core engine systems (transforms, triggers, sprite animation, particles; after mods: viewport anchors, lighting, post-process volumes, tilemaps, sprites, particles, text staging, bitmap text) register on the scheduler before and after <see cref="Modding.ModLoader.LoadAll"/> (each mod’s <see cref="Modding.IMod.OnLoadAsync"/> completes synchronously on the load thread); mods add their own serial or parallel systems via <see cref="Modding.ModLoadContext.RegisterSerial"/> / <see cref="Modding.ModLoadContext.RegisterParallel"/>, implementing <see cref="Core.Ecs.IEarlyUpdate"/> / <see cref="Core.Ecs.IFixedUpdate"/> / <see cref="Core.Ecs.ILateUpdate"/> (or parallel equivalents) as needed.
/// </para>
/// <para>
/// <see cref="Core.Tasks.SystemScheduler.RunFrame(Cyberland.Engine.Core.Ecs.World, float)"/> runs once per window <strong>Render</strong> tick (not per <strong>Update</strong>), with <c>deltaSeconds</c> equal to Silk’s <c>Render</c> callback argument (the render stopwatch interval from <c>DoRender</c>). Avoid a <c>minimum</c> frame duration clamp: at high refresh + mailbox, real intervals can fall below 2 ms; a floor would add fake time every tick and make fixed-step sim run faster than wall clock. Only cap large hitches (max) to keep the accumulator bounded. Silk may call <strong>Update</strong> more often than <strong>Render</strong>; running the full ECS from <strong>Update</strong> advanced gameplay multiple times per presented frame and made HUD frame timing misleading.
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
    private readonly string? _profileDumpPath;
    private readonly string? _perfDumpPath;
    private Stopwatch? _profileWall;
    private bool _profileWallStarted;
    private bool _profileCloseRequested;
    private int _profilePresentedFrames;
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
        FrameProfiler.ApplyEnvironmentDefaults();
        _profileWallSeconds = ProfileCommandLine.TryParseProfileSeconds(_commandLineArgs);
        _profileDumpPath = ProfileCommandLine.TryParseProfileDump(_commandLineArgs);
        _perfDumpPath = ProfileCommandLine.TryParsePerfDump(_commandLineArgs);
#if !DEBUG
        if (_profileWallSeconds is not null || !string.IsNullOrEmpty(_profileDumpPath) || !string.IsNullOrEmpty(_perfDumpPath))
        {
            Console.Error.WriteLine(
                "Cyberland: --profile-seconds auto-close works in all configs. --profile-dump requires Debug; use --perf-dump for Release perf summaries.");
        }
#endif
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

#if DEBUG
        Console.WriteLine(
            $"Cyberland startup | Configuration=Debug | FrameProfilerEnabled={FrameProfiler.IsEnabled} | UiIncremental={UiLayoutGating.UseIncrementalDocumentFrames}");
#else
        Console.WriteLine(
            $"Cyberland startup | Configuration=Release | UiIncremental={UiLayoutGating.UseIncrementalDocumentFrames}");
#endif

        // Bootstrap order: Vulkan + audio + input → assign Host.Renderer/Input → baseline HDR once (EngineDefaultGlobalPostProcess)
        // → sync input bindings (window thread; GetAwaiter().GetResult avoids re-entrancy on the same thread)
        // → register core parallel sim systems → ModLoader.LoadAll (mods register systems) → parallel render submit systems
        // → locale bootstrap. First RunFrame runs after this returns.
        _renderer = new VulkanRenderer(_window);
        try
        {
            _renderer.Initialize();
        }
        catch (GraphicsInitializationException ex)
        {
            UserMessageDialog.ShowError("Cyberland — Graphics unavailable", ex.UserMessage);
            _renderer.Dispose();
            _renderer = null;
            _window.Close();
            return;
        }

        EngineDiagnostics.UseNativeUserNotifications();

        try
        {
            _audio = new OpenALAudioDevice();
        }
        catch (Exception ex)
        {
            // OpenAL Soft may be missing on some machines; game continues silent until configured.
#if DEBUG
            Console.Error.WriteLine($"Cyberland: audio initialization failed ({ex.GetType().Name}): {ex.Message}");
#else
            _ = ex;
#endif
            _audio = null;
        }

        _input = new SilkInputService(_window.CreateInput(), _renderer);
        _host.Renderer = _renderer;
        _host.Input = _input;
        _host.Tilemaps ??= new TilemapDataStore();
        _host.CameraRuntimeState = Hosting.CameraRuntimeState.CreateDefault(_renderer.SwapchainPixelSize);
        _host.EnsureCoreServicesReady();
        _renderer.RequestClose = () => _window?.Close();

        if (_profileWallSeconds is not null)
        {
            _renderer.FramePacing = new FramePacing(FramePacingMode.Unlimited);
#if DEBUG
            FrameProfiler.ResetSession();
            FrameProfiler.ConfigureWarmup(TimeSpan.FromSeconds(1));
#endif
            _profileWall = new Stopwatch();
            _profileWallStarted = false;
        }

        EngineDefaultGlobalPostProcess.Apply(_renderer);

        var bindingsFile = Path.Combine(AppContext.BaseDirectory, "input-bindings.json");
        _input.Bindings.LoadOrCreateUserFileAsync(bindingsFile).GetAwaiter().GetResult();

        var languageFile = Path.Combine(AppContext.BaseDirectory, "language.json");
        var primaryCulture = LanguagePreference.Resolve(_commandLineArgs, languageFile);
        _localizedContent = new LocalizedContent(_localization, _vfs, primaryCulture);
        _host.LocalizedContent = _localizedContent;

        try
        {
            _scheduler.BeginDeferExecutionOrderRebuilds();
            try
            {
                _scheduler.RegisterParallel("cyberland.engine/transform2d", new TransformHierarchySystem());
                _scheduler.RegisterParallel("cyberland.engine/sprite-animation", new SpriteAnimationSystem());
                _scheduler.RegisterParallel("cyberland.engine/particle-sim", new ParticleSimulationSystem());

                var excluded = ExcludeModsParser.TryParse(_commandLineArgs);
                IReadOnlySet<string>? excludedSet = null;
                if (excluded is not null)
                    excludedSet = new HashSet<string>(excluded, StringComparer.OrdinalIgnoreCase);

                _mods.LoadAll(
                    Path.Combine(AppContext.BaseDirectory, "Mods"),
                    _vfs,
                    _localizedContent,
                    _world,
                    _scheduler,
                    _host,
                    excludedSet);

                _scheduler.RegisterParallel("cyberland.engine/camera-follow", new CameraFollowSystem());
                _scheduler.RegisterParallel("cyberland.engine/trigger", new TriggerSystem());
                // Publish camera runtime state before viewport anchors so gameplay/layout reads deterministic ECS-owned
                // camera data instead of renderer queue snapshots.
                _scheduler.RegisterSerial("cyberland.engine/camera-submit", new CameraSubmitSystem(_host));
                _scheduler.RegisterSerial("cyberland.engine/camera-runtime-state", new CameraRuntimeStateSystem(_host));
                _scheduler.RegisterSerial("cyberland.engine/viewport-layout", new ViewportAnchorSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/lighting-ambient", new AmbientLightSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/lighting-directional", new DirectionalLightSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/lighting-spot", new SpotLightSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/lighting-point", new PointLightSystem(_host));
                _scheduler.RegisterSerial("cyberland.engine/global-post-process", new GlobalPostProcessSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/post-process-volumes", new PostProcessVolumeSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/tilemap-render", new TilemapRenderSystem(_host));
                _scheduler.RegisterSerial("cyberland.engine/sprite-localized-assets", new SpriteLocalizedAssetSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/sprite-render", new SpriteRenderSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/particle-render", new ParticleRenderSystem(_host));
                _scheduler.RegisterParallel("cyberland.engine/text-staging", new TextStagingSystem(_host));
                // Bitmap text build is folded into TextRenderSystem; it parallelizes per chunk/range and submits thread-safe requests.
                _scheduler.RegisterParallel("cyberland.engine/text-render", new TextRenderSystem(_host));
                _scheduler.RegisterSerial("cyberland.engine/ui-document-frame", new UiDocumentFrameSystem(_host));
                _scheduler.RegisterSerial("cyberland.engine/ui-command-drain", new UiCommandDrainSystem(_host));
            }
            finally
            {
                _scheduler.EndDeferExecutionOrderRebuilds();
            }

            _localizedContent.MergeStringTable("strings.json");
        }
        catch (Exception ex)
        {
            // Startup failed after renderer/input were initialized. Keep failure handling deterministic:
            // unload any partially loaded mods, tear down renderer, and close the window.
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
            _window.Close();
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

        using var __frame = FrameProfilerScope.Enter("OnRender.Total");

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
            using var __ = FrameProfilerScope.Enter("Game.ResetPendingSubmissions");
            _renderer?.ResetPendingSubmissionsForNewTick();
        }

        {
            using var __ = FrameProfilerScope.Enter("Input.BeginFrame");
            _host.Input?.BeginFrame();
        }

        {
            using var __ = FrameProfilerScope.Enter("Scheduler.RunFrame");
            // Publish fixed-step remainder before ILateUpdate so visual extrapolation (e.g. pos + vel * acc) uses this frame's alpha.
            _scheduler.RunFrame(_world, dt, acc => _host.FixedAccumulatorSeconds = acc);
        }

        _host.FixedDeltaSeconds = _scheduler.FixedDeltaSeconds;

        {
            using var __ = FrameProfilerScope.Enter("Vulkan.DrawFrame");
            _renderer?.DrawFrame();
        }

        _host.LastPresentDeltaSeconds = dt;
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
            if (_input.WasPressed("cyberland.engine/profile-hud"))
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
        if (_profileWallSeconds is not null && _profileWall is not null)
        {
            var wallSeconds = _profileWall.Elapsed.TotalSeconds;
            var fps = wallSeconds > 0d ? _profilePresentedFrames / wallSeconds : 0d;
            Console.WriteLine(
                $"Perf summary | frames={_profilePresentedFrames} wallSeconds={wallSeconds.ToString("0.###", CultureInfo.InvariantCulture)} fps={fps.ToString("0.###", CultureInfo.InvariantCulture)}");
            if (!string.IsNullOrWhiteSpace(_perfDumpPath))
                WritePerfDump(_perfDumpPath, _profilePresentedFrames, wallSeconds, fps);
        }
#if DEBUG
        if (!string.IsNullOrEmpty(_profileDumpPath))
            FrameProfiler.WriteDump(_profileDumpPath);
#endif
        _mods.UnloadAll();
    }

    private static void WritePerfDump(string path, int frames, double wallSeconds, double fps)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder(128);
        sb.Append("frames=").Append(frames.ToString(CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("wallSeconds=").Append(wallSeconds.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("fps=").Append(fps.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Releases GPU, audio, and window resources. Safe to call once after <see cref="Run"/> returns.
    /// </summary>
    public void Dispose()
    {
        _input?.Dispose();
        _renderer?.Dispose();
        _audio?.Dispose();
        _window?.Dispose();
    }
}
