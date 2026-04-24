using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Sequential early input: fills <see cref="Control"/> and maps <c>Q</c> to <see cref="IRenderer.RequestClose"/>.</summary>
public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _stateEntity;
    private readonly EntityId _controlEntity;
    private World _world;

    public InputSystem(GameHostServices host, EntityId stateEntity, EntityId controlEntity)
    {
        _host = host;
        _stateEntity = stateEntity;
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
        ref var c = ref world.Components<Control>().Get(_controlEntity);
        // Preserve latched intents for fixed systems: several Render ticks may occur before the next fixed substep at high refresh.
        var pendingStart = c.StartRound;
        var pendingLaunch = c.LaunchBall;
        c.MoveLeft = false;
        c.MoveRight = false;
        c.StartRound = pendingStart;
        c.LaunchBall = pendingLaunch;
        var r = _host.Renderer;
        var input = _host.Input;
        if (r is null)
            return;
        if (input is null)
            return;
        if (input.IsDown("cyberland.common/quit"))
        {
            r.RequestClose?.Invoke();
            return;
        }
        var phase = world.Components<GameState>().Get(_stateEntity).Phase;
        switch (phase)
        {
            case Phase.Title:
                if (input.WasPressed("cyberland.demo.brickbreaker/start_round") || input.WasPressed("cyberland.common/start"))
                    c.StartRound = true;
                break;
            case Phase.GameOver:
                if (input.WasPressed("cyberland.demo.brickbreaker/start_round") || input.WasPressed("cyberland.common/start"))
                    c.StartRound = true;
                break;
            case Phase.Playing:
                var move = input.ReadAxis("cyberland.demo.brickbreaker/move_x");
                if (move < 0f)
                    c.MoveLeft = true;
                if (move > 0f)
                    c.MoveRight = true;
                if (input.WasPressed("cyberland.demo.brickbreaker/launch_ball"))
                    c.LaunchBall = true;
                break;
        }
    }
}
