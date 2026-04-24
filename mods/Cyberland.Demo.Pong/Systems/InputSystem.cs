using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;

namespace Cyberland.Demo.Pong;

public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly SystemScheduler _scheduler;
    private World _world;
    private IKeyboard? _keyboard;

    public InputSystem(GameHostServices host, EntityId session, SystemScheduler scheduler) { _host = host; _session = session; _scheduler = scheduler; }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Pong.InputSystem startup failed", "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("Cyberland.Demo.Pong InputSystem requires a renderer.");
        }

        var input = _host.Input;
        if (input?.Keyboards.Count is not > 0)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Pong.InputSystem startup failed", "No keyboard input device was available during OnStart.");
            throw new InvalidOperationException("Cyberland.Demo.Pong InputSystem requires a keyboard.");
        }

        _keyboard = input.Keyboards[0];
    }

    public void OnEarlyUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var world = _world;
        ref var c = ref world.Components<Control>().Get(_session);
        c = default;

        var renderer = _host.Renderer!;
        var keyboard = _keyboard!;
        var syncOn = _scheduler.IsEnabled("cyberland.demo.pong/visual-sync");
        if (keyboard.IsKeyPressed(Key.F10) && syncOn)
            _scheduler.SetEnabled("cyberland.demo.pong/visual-sync", !syncOn);
        if (keyboard.IsKeyPressed(Key.Q)) { renderer.RequestClose?.Invoke(); return; }

        ref var st = ref world.Components<State>().Get(_session);
        switch (st.Phase)
        {
            case Phase.Title: if (keyboard.IsKeyPressed(Key.Enter)) c.StartMatch = true; break;
            case Phase.GameOver: if (keyboard.IsKeyPressed(Key.Enter) || keyboard.IsKeyPressed(Key.R)) c.StartMatch = true; break;
            case Phase.Playing:
                if (keyboard.IsKeyPressed(Key.W) || keyboard.IsKeyPressed(Key.Up)) c.PaddleUp = true;
                if (keyboard.IsKeyPressed(Key.S) || keyboard.IsKeyPressed(Key.Down)) c.PaddleDown = true;
                break;
        }
    }
}
