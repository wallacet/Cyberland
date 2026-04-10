using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;

namespace Cyberland.Demo.Pong;

/// <summary>Keyboard → <see cref="PongControl"/>; quit is immediate.</summary>
public sealed class PongInputSystem : ISystem
{
    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly SystemScheduler _scheduler;

    public PongInputSystem(GameHostServices host, EntityId session, SystemScheduler scheduler)
    {
        _host = host;
        _session = session;
        _scheduler = scheduler;
    }

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref var c = ref world.Components<PongControl>().Get(_session);
        c = default;

        var r = _host.Renderer;
        var input = _host.Input;
        if (r is null)
            return;

        var kb = input?.Keyboards.Count > 0 ? input.Keyboards[0] : null;
        if (kb is null)
            return;

        if (kb.IsKeyPressed(Key.F10) && _scheduler.TryGetEnabled("cyberland.demo.pong/visual-sync", out var syncOn))
            _scheduler.SetEnabled("cyberland.demo.pong/visual-sync", !syncOn);

        if (kb.IsKeyPressed(Key.Q))
        {
            r.RequestClose?.Invoke();
            return;
        }

        ref var st = ref world.Components<PongState>().Get(_session);
        switch (st.Phase)
        {
            case PongPhase.Title:
                if (kb.IsKeyPressed(Key.Enter))
                    c.StartMatch = true;
                break;
            case PongPhase.GameOver:
                if (kb.IsKeyPressed(Key.Enter) || kb.IsKeyPressed(Key.R))
                    c.StartMatch = true;
                break;
            case PongPhase.Playing:
                if (kb.IsKeyPressed(Key.W) || kb.IsKeyPressed(Key.Up))
                    c.PaddleUp = true;
                if (kb.IsKeyPressed(Key.S) || kb.IsKeyPressed(Key.Down))
                    c.PaddleDown = true;
                break;
        }
    }
}
