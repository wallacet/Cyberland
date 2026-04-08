using Cyberland.Engine.Assets;
using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Cyberland.Engine;

/// <summary>
/// Host-facing bootstrap: window, GL, audio, ECS tick, mod load, and input. Keeps Program.cs thin.
/// </summary>
public sealed class GameApplication : IDisposable
{
    private readonly ParallelismSettings _parallelism = new();
    private readonly VirtualFileSystem _vfs = new();
    private readonly World _world = new();
    private readonly SystemScheduler _scheduler;
    private readonly ModLoader _mods = new();
    private readonly LocalizationManager _localization = new();
    private OpenALAudioDevice? _audio;
    private OpenGLRenderer? _renderer;
    private IWindow? _window;
    private IInputContext? _input;
    private readonly KeyBindingStore _bindings = new();

    public GameApplication()
    {
        _scheduler = new SystemScheduler(_parallelism);
    }

    public void Run()
    {
        var options = WindowOptions.Default;
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

        _renderer = new OpenGLRenderer(_window);
        _renderer.Initialize();

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

        var bindingsFile = Path.Combine(AppContext.BaseDirectory, "keybindings.json");
        _bindings.LoadOrCreateUserFileAsync(bindingsFile).GetAwaiter().GetResult();

        var assets = new AssetManager(_vfs);
        _mods.LoadAll(
            Path.Combine(AppContext.BaseDirectory, "Mods"),
            _vfs,
            _localization,
            _world,
            _scheduler);

        LocalizationBootstrap.LoadAsync(_localization, assets, "Locale/en/strings.json").GetAwaiter().GetResult();
    }

    private void OnUpdate(double delta)
    {
        var dt = (float)delta;
        _scheduler.RunFrame(_world, dt);

        if (_input?.Keyboards.Count > 0)
        {
            var kb = _input.Keyboards[0];
            if (_bindings.IsDown(kb, "menu") && _window != null)
                _window.Close();
        }
    }

    private void OnRender(double _)
    {
        _renderer?.BeginFrame();
        _renderer?.EndFrame();
    }

    private void OnClosing()
    {
        _mods.UnloadAll();
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _audio?.Dispose();
        _window?.Dispose();
    }
}
