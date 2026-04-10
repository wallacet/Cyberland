using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;

namespace Cyberland.Demo.Snake;

public sealed class SnakeInputSystem : ISystem
{
    private readonly GameHostServices _host;
    private readonly SnakeSession _session;
    private readonly EntityId _controlEntity;

    public SnakeInputSystem(GameHostServices host, SnakeSession session, EntityId controlEntity)
    {
        _host = host;
        _session = session;
        _controlEntity = controlEntity;
    }

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref var ctl = ref world.Components<SnakeControl>().Get(_controlEntity);
        ctl = default;

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

        var s = _session;
        switch (s.Phase)
        {
            case SnakePhase.Title:
                if (kb.IsKeyPressed(Key.Enter))
                    ctl.StartGame = true;
                break;
            case SnakePhase.Playing:
                if (kb.IsKeyPressed(Key.Up) && s.DirY == 0)
                {
                    s.NextDirX = 0;
                    s.NextDirY = 1;
                }
                else if (kb.IsKeyPressed(Key.Down) && s.DirY == 0)
                {
                    s.NextDirX = 0;
                    s.NextDirY = -1;
                }
                else if (kb.IsKeyPressed(Key.Left) && s.DirX == 0)
                {
                    s.NextDirX = -1;
                    s.NextDirY = 0;
                }
                else if (kb.IsKeyPressed(Key.Right) && s.DirX == 0)
                {
                    s.NextDirX = 1;
                    s.NextDirY = 0;
                }

                break;
            case SnakePhase.GameOver:
                if (kb.IsKeyPressed(Key.Enter) || kb.IsKeyPressed(Key.R))
                    ctl.StartGame = true;
                break;
        }
    }
}
