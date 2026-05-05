using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Early phase: polls axes/actions into <see cref="Control"/> and reads mode from <see cref="State"/>.
/// </summary>
/// <remarks>
/// Single session entity carries both components — <see cref="ISingletonSystem"/> avoids empty <see cref="SystemQuerySpec"/> while toggling visual-sync via the scheduler.
/// </remarks>
public sealed class InputSystem : ISingletonSystem, ISingletonEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<State, Control>();

    private readonly GameHostServices _host;
    private readonly SystemScheduler _scheduler;

    /// <summary>Needs <paramref name="scheduler"/> so dev toggle can enable/disable <c>cyberland.demo.pong/visual-sync</c>.</summary>
    public InputSystem(GameHostServices host, SystemScheduler scheduler)
    {
        _host = host;
        _scheduler = scheduler;
    }

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _ = sessionRow;
        _ = _host.Renderer
            ?? throw new InvalidOperationException("Cyberland.Demo.Pong InputSystem requires a renderer.");
        _ = _host.Input
            ?? throw new InvalidOperationException("Cyberland.Demo.Pong InputSystem requires input.");
    }

    /// <inheritdoc />
    public void OnSingletonEarlyUpdate(in SingletonEntity sessionRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref var c = ref sessionRow.Get<Control>();
        // Only reset per-frame movement; StartMatch must survive until Simulation consumes it — at high refresh several
        // Render ticks can occur before the fixed accumulator reaches a substep, and clearing the whole struct would drop the latch.
        c.PaddleUp = false;
        c.PaddleDown = false;

        var renderer = _host.Renderer!;
        var input = _host.Input!;
        var syncOn = _scheduler.IsEnabled("cyberland.demo.pong/visual-sync");
        if (input.ConsumePressed("cyberland.demo.pong/toggle_visual_sync") && syncOn)
            _scheduler.SetEnabled("cyberland.demo.pong/visual-sync", !syncOn);
        if (input.IsDown("cyberland.common/quit")) { renderer.RequestClose?.Invoke(); return; }

        ref var st = ref sessionRow.Get<State>();
        switch (st.Phase)
        {
            case Phase.Title: if (input.ConsumePressed("cyberland.demo.pong/start_match") || input.ConsumePressed("cyberland.common/start")) c.StartMatch = true; break;
            case Phase.GameOver: if (input.ConsumePressed("cyberland.demo.pong/start_match") || input.ConsumePressed("cyberland.common/start")) c.StartMatch = true; break;
            case Phase.Playing:
                var y = input.ReadAxis("cyberland.demo.pong/paddle_y");
                if (y > 0f) c.PaddleUp = true;
                if (y < 0f) c.PaddleDown = true;
                break;
        }
    }
}
