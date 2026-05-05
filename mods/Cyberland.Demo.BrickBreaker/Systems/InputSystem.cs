using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Early update: maps actions to <see cref="Control"/> and forwards common quit to the host renderer’s close request.
/// </summary>
/// <remarks>
/// <see cref="ISingletonSystem"/> on the control row (<see cref="ControlTag"/> + <see cref="Control"/>); session phase is read from the
/// <see cref="SessionTag"/> entity resolved once at startup.
/// </remarks>
public sealed class InputSystem : ISingletonSystem, ISingletonEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ControlTag, Control>();

    private EntityId _stateEntity;
    private readonly GameHostServices _host;

    public InputSystem(GameHostServices host) => _host = host;

    public void OnSingletonStart(in SingletonEntity controlRow)
    {
        _stateEntity = Session.RequireStateEntity(controlRow.World);
        _ = _host.Input
            ?? throw new InvalidOperationException("cyberland.demo.brick/input requires Host.Input during OnSingletonStart.");
        _ = _host.Renderer
            ?? throw new InvalidOperationException("cyberland.demo.brick/input requires Host.Renderer during OnSingletonStart.");
    }

    public void OnSingletonEarlyUpdate(in SingletonEntity controlRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var world = controlRow.World;
        ref var c = ref controlRow.Get<Control>();
        c.MoveLeft = false;
        c.MoveRight = false;

        var r = _host.Renderer!;
        var input = _host.Input!;

        if (input.IsDown("cyberland.common/quit"))
        {
            r.RequestClose?.Invoke();
            return;
        }

        var phase = world.Get<GameState>(_stateEntity).Phase;
        switch (phase)
        {
            case Phase.Title:
                if (input.ConsumePressed("cyberland.demo.brickbreaker/start_round") || input.ConsumePressed("cyberland.common/start"))
                    c.StartRound = true;
                break;
            case Phase.GameOver:
            case Phase.Won:
                if (input.ConsumePressed("cyberland.demo.brickbreaker/start_round") || input.ConsumePressed("cyberland.common/start"))
                    c.StartRound = true;
                break;
            case Phase.Playing:
                var move = input.ReadAxis("cyberland.demo.brickbreaker/move_x");
                if (move < 0f)
                    c.MoveLeft = true;
                if (move > 0f)
                    c.MoveRight = true;
                if (input.ConsumePressed("cyberland.demo.brickbreaker/launch_ball"))
                    c.LaunchBall = true;
                break;
        }
    }
}
