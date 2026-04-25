using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.Snake;

public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _sessionEntity;
    private readonly EntityId _controlEntity;
    private World _world;
    public InputSystem(GameHostServices host, EntityId sessionEntity, EntityId controlEntity)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _controlEntity = controlEntity;
    }
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
    }
    public void OnEarlyUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var world = _world;
        ref var ctl = ref world.Components<Control>().Get(_controlEntity);
        // Do not clear the whole component: StartGame is latched for TickSystem (fixed). High refresh can deliver many
        // Render ticks before the next fixed substep; ctl = default would erase an unconsumed StartGame before Tick runs.
        var r = _host.Renderer;
        var input = _host.Input;
        if (r is null) return;
        if (input is null) return;
        if (input.IsDown("cyberland.common/quit")) { r.RequestClose?.Invoke(); return; }
        ref var session = ref world.Components<Session>().Get(_sessionEntity);
        switch (session.Phase)
        {
            case Phase.Title: if (input.WasPressed("cyberland.demo.snake/start_game") || input.WasPressed("cyberland.common/start")) ctl.StartGame = true; break;
            case Phase.Playing:
                if (input.WasPressed("cyberland.demo.snake/up") && session.DirY == 0) { session.NextDirX = 0; session.NextDirY = 1; }
                else if (input.WasPressed("cyberland.demo.snake/down") && session.DirY == 0) { session.NextDirX = 0; session.NextDirY = -1; }
                else if (input.WasPressed("cyberland.demo.snake/left") && session.DirX == 0) { session.NextDirX = -1; session.NextDirY = 0; }
                else if (input.WasPressed("cyberland.demo.snake/right") && session.DirX == 0) { session.NextDirX = 1; session.NextDirY = 0; }
                break;
            case Phase.GameOver:
            case Phase.Won:
                if (input.WasPressed("cyberland.demo.snake/start_game") || input.WasPressed("cyberland.common/start")) ctl.StartGame = true; break;
        }
    }
}
