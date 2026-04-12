using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.Snake;

public sealed class SnakeTickSystem : ISystem, IFixedUpdate
{
    private readonly GameHostServices _host;
    private readonly SnakeSession _session;
    private readonly EntityId _controlEntity;

    public SnakeTickSystem(GameHostServices host, SnakeSession session, EntityId controlEntity)
    {
        _host = host;
        _session = session;
        _controlEntity = controlEntity;
    }

    public void OnFixedUpdate(World world, float fixedDeltaSeconds)
    {
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        if (fb.X <= 0 || fb.Y <= 0)
            return;

        var s = _session;
        s.UpdateLayout(fb.X, fb.Y);

        ref var ctl = ref world.Components<SnakeControl>().Get(_controlEntity);
        if (ctl.StartGame)
        {
            ctl.StartGame = false;
            s.StartGame();
        }

        if (s.Phase != SnakePhase.Playing)
            return;

        s.TickAcc += fixedDeltaSeconds;
        while (s.TickAcc >= SnakeConstants.TickSeconds)
        {
            s.TickAcc -= SnakeConstants.TickSeconds;
            s.DirX = s.NextDirX;
            s.DirY = s.NextDirY;
            s.Step();
        }
    }
}
