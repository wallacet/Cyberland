using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Early update: paddle axes, quit, and one-shot gameplay intents via <see cref="InputGameplayCommandExtensions"/>.
/// </summary>
/// <remarks>
/// <see cref="Control.StartRound"/> / <see cref="Control.LaunchBall"/> stay latched until fixed systems consume them — safe when
/// fixed substeps are zero for several render ticks at high refresh.
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
    }

    public void OnSingletonEarlyUpdate(in SingletonEntity controlRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var world = controlRow.World;
        ref var c = ref controlRow.Get<Control>();
        c.MoveLeft = false;
        c.MoveRight = false;

        var r = _host.Renderer;
        var input = _host.Input;

        if (input.IsDown("cyberland.common/quit"))
        {
            r.RequestClose?.Invoke();
            return;
        }

        var phase = world.Get<GameState>(_stateEntity).Phase;
        switch (phase)
        {
            case Phase.Title:
            case Phase.GameOver:
            case Phase.Won:
                if (input.HasAnyActionPressedThisFrame(
                        "cyberland.demo.brickbreaker/start_round",
                        "cyberland.common/start"))
                    c.StartRound = true;
                break;
            case Phase.Playing:
                var move = input.ReadAxis("cyberland.demo.brickbreaker/move_x");
                if (move < 0f)
                    c.MoveLeft = true;
                if (move > 0f)
                    c.MoveRight = true;
                if (input.HasActionPressedThisFrame("cyberland.demo.brickbreaker/launch_ball"))
                    c.LaunchBall = true;
                break;
        }
    }
}
