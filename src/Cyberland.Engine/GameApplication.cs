using System.Diagnostics.CodeAnalysis;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
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
/// Core engine systems (transforms, sprite animation, particles, tilemaps, sprites) register on the scheduler before and after <see cref="Modding.ModLoader.LoadAll"/>; mods add their own <see cref="Core.Ecs.ISystem"/> passes via <see cref="Modding.ModLoadContext.RegisterSequential"/> / <see cref="Modding.ModLoadContext.RegisterParallel"/>.
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
    private readonly KeyBindingStore _bindings = new();
    private readonly GameHostServices _host;
    private readonly string[] _commandLineArgs;
    private OpenALAudioDevice? _audio;
    private VulkanRenderer? _renderer;
    private IWindow? _window;
    private IInputContext? _input;

    /// <summary>
    /// Prepares host services and the frame scheduler. Does not open the window until <see cref="Run"/> is called.
    /// </summary>
    /// <param name="commandLineArgs">Optional argv (e.g. for <c>--exclude-mods</c> parsed by <see cref="Modding.ExcludeModsParser"/>).</param>
    public GameApplication(string[]? commandLineArgs = null)
    {
        _commandLineArgs = commandLineArgs ?? Array.Empty<string>();
        _scheduler = new SystemScheduler(_parallelism);
        _host = new GameHostServices(_bindings);
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

        // Bootstrap order: Vulkan + audio + input → assign Host.Renderer/Input → baseline HDR once (EngineDefaultGlobalPostProcess)
        // → sync keybindings (window thread; GetAwaiter().GetResult avoids re-entrancy on the same thread)
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

        try
        {
            _audio = new OpenALAudioDevice();
        }
        catch
        {
            // OpenAL Soft may be missing on some machines; game continues silent until configured.
            _audio = null;
        }

        _input = _window.CreateInput();
        _host.Renderer = _renderer;
        _host.Input = _input;
        _host.Tilemaps ??= new TilemapDataStore();
        _host.Particles ??= new ParticleStore();
        _renderer.RequestClose = () => _window?.Close();

        EngineDefaultGlobalPostProcess.Apply(_renderer);

        var bindingsFile = Path.Combine(AppContext.BaseDirectory, "keybindings.json");
        _bindings.LoadOrCreateUserFileAsync(bindingsFile).GetAwaiter().GetResult();

        _scheduler.RegisterParallel("cyberland.engine/transform2d", new TransformHierarchySystem());
        _scheduler.RegisterParallel("cyberland.engine/sprite-animation", new SpriteAnimationSystem());
        _scheduler.RegisterParallel("cyberland.engine/particle-sim", new ParticleSimulationSystem(_host));

        var assets = new AssetManager(_vfs);
        var excluded = ExcludeModsParser.TryParse(_commandLineArgs);
        IReadOnlySet<string>? excludedSet = null;
        if (excluded is not null)
            excludedSet = new HashSet<string>(excluded, StringComparer.OrdinalIgnoreCase);

        _mods.LoadAll(
            Path.Combine(AppContext.BaseDirectory, "Mods"),
            _vfs,
            _localization,
            _world,
            _scheduler,
            _host,
            excludedSet);

        _scheduler.RegisterParallel("cyberland.engine/tilemap-render", new TilemapRenderSystem(_host));
        _scheduler.RegisterParallel("cyberland.engine/sprite-render", new SpriteRenderSystem(_host));
        _scheduler.RegisterParallel("cyberland.engine/particle-render", new ParticleRenderSystem(_host));

        LocalizationBootstrap.LoadAsync(_localization, assets, "Locale/en/strings.json").GetAwaiter().GetResult();
    }

    private void OnUpdate(double delta)
    {
        var dt = (float)delta;
        _scheduler.RunFrame(_world, dt);
    }

    private void OnRender(double _)
    {
        _renderer?.DrawFrame();
    }

    private void OnClosing()
    {
        _mods.UnloadAll();
    }

    /// <summary>
    /// Releases GPU, audio, and window resources. Safe to call once after <see cref="Run"/> returns.
    /// </summary>
    public void Dispose()
    {
        _renderer?.Dispose();
        _audio?.Dispose();
        _window?.Dispose();
    }
}
