using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;

namespace Cyberland.Demo.BrickBreaker;

public sealed class BrickInputSystem : ISystem, IEarlyUpdate
{
    private readonly GameHostServices _host;
    private readonly BrickSession _session;
    private readonly EntityId _controlEntity;

    public BrickInputSystem(GameHostServices host, BrickSession session, EntityId controlEntity)
    {
        _host = host;
        _session = session;
        _controlEntity = controlEntity;
    }

    public void OnEarlyUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref var c = ref world.Components<BrickControl>().Get(_controlEntity);
        c = default;

        var r = _host.Renderer;
        var input = _host.Input;
        if (r is null)
            return;

        var kb = input?.Keyboards.Count > 0 ? input.Keyboards[0] : null;
        if (kb is null)
            return;

        if (kb.IsKeyPressed(Key.Q))
        {
            r.RequestClose?.Invoke();
            return;
        }

        switch (_session.Phase)
        {
            case BrickPhase.Title:
                if (kb.IsKeyPressed(Key.Enter))
                    c.StartRound = true;
                break;
            case BrickPhase.GameOver:
                if (kb.IsKeyPressed(Key.Enter) || kb.IsKeyPressed(Key.R))
                    c.StartRound = true;
                break;
            case BrickPhase.Playing:
                if (kb.IsKeyPressed(Key.A) || kb.IsKeyPressed(Key.Left))
                    c.MoveLeft = true;
                if (kb.IsKeyPressed(Key.D) || kb.IsKeyPressed(Key.Right))
                    c.MoveRight = true;
                if (kb.IsKeyPressed(Key.Space))
                    c.LaunchBall = true;
                break;
        }
    }
}
