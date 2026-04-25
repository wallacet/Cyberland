using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.Pong;

public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;


    private World _world = null!;
    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly SystemScheduler _scheduler;
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

        if (_host.Input is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Pong.InputSystem startup failed", "Host.Input was null during OnStart.");
            throw new InvalidOperationException("Cyberland.Demo.Pong InputSystem requires input.");
        }
    }

    public void OnEarlyUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        ref var c = ref _world.Get<Control>(_session);
        // Only reset per-frame movement; StartMatch must survive until Simulation consumes it — at high refresh several
        // Render ticks can occur before the fixed accumulator reaches a substep, and clearing the whole struct would drop the latch.
        c.PaddleUp = false;
        c.PaddleDown = false;

        var renderer = _host.Renderer!;
        var input = _host.Input!;
        var syncOn = _scheduler.IsEnabled("cyberland.demo.pong/visual-sync");
        if (input.WasPressed("cyberland.demo.pong/toggle_visual_sync") && syncOn)
            _scheduler.SetEnabled("cyberland.demo.pong/visual-sync", !syncOn);
        if (input.IsDown("cyberland.common/quit")) { renderer.RequestClose?.Invoke(); return; }

        ref var st = ref _world.Get<State>(_session);
        switch (st.Phase)
        {
            case Phase.Title: if (input.WasPressed("cyberland.demo.pong/start_match") || input.WasPressed("cyberland.common/start")) c.StartMatch = true; break;
            case Phase.GameOver: if (input.WasPressed("cyberland.demo.pong/start_match") || input.WasPressed("cyberland.common/start")) c.StartMatch = true; break;
            case Phase.Playing:
                var y = input.ReadAxis("cyberland.demo.pong/paddle_y");
                if (y > 0f) c.PaddleUp = true;
                if (y < 0f) c.PaddleDown = true;
                break;
        }
    }
}
