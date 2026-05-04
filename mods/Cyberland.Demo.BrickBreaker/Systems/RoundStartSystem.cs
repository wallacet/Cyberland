using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Fixed: consumes <see cref="Control.StartRound"/>, resets session/paddle/ball, and hands off brick reactivation to
/// <see cref="ReactivateSystem"/> via <see cref="GameState.PendingReactivation"/>.
/// </summary>
public sealed class RoundStartSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SessionTag, GameState>();

    private World _world = null!;
    private EntityId _controlEntity;
    private EntityId _paddleEntity;
    private EntityId _ballEntity;

    public void OnSingletonStart(in SingletonEntity session)
    {
        _world = session.World;
        _controlEntity = _world.QueryChunks(SystemQuerySpec.All<ControlTag>())
            .RequireSingleEntityWith<ControlTag>("brick control");
        _paddleEntity = _world.QueryChunks(SystemQuerySpec.All<Paddle>())
            .RequireSingleEntityWith<Paddle>("brick paddle");
        _ballEntity = _world.QueryChunks(SystemQuerySpec.All<BallTag>())
            .RequireSingleEntityWith<BallTag>("brick ball");
    }

    public void OnSingletonFixedUpdate(in SingletonEntity session, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        ref var control = ref _world.Get<Control>(_controlEntity);
        if (!control.StartRound)
            return;

        control.StartRound = false;
        ref var game = ref session.Get<GameState>();
        game.Phase = Phase.Playing;
        game.Lives = Constants.StartingLives;
        game.Score = 0;
        game.BallDocked = true;
        game.PendingReactivation = true;

        ref var paddleTransform = ref _world.Get<Transform>(_paddleEntity);
        ref var paddleBody = ref _world.Get<PaddleBody>(_paddleEntity);
        var paddlePos = new Vector2D<float>(game.LayoutWidth * 0.5f, game.PaddleY);
        paddleTransform.LocalPosition = paddlePos;
        paddleBody.HalfWidth = 72f;
        paddleBody.HalfHeight = 10f;

        ref var ballTransform = ref _world.Get<Transform>(_ballEntity);
        ref var ballVel = ref _world.Get<Velocity>(_ballEntity);
        var ballPos = new Vector2D<float>(paddlePos.X, game.PaddleY + paddleBody.HalfHeight + Constants.BallR);
        ballTransform.LocalPosition = ballPos;
        ballVel.Value = default;

        ref var paddleTrigger = ref _world.Get<Trigger>(_paddleEntity);
        paddleTrigger.Enabled = true;
        paddleTrigger.HalfExtents = new Vector2D<float>(paddleBody.HalfWidth, paddleBody.HalfHeight);
        ref var ballTrigger = ref _world.Get<Trigger>(_ballEntity);
        ballTrigger.Enabled = true;
        ballTrigger.Radius = Constants.BallR;
    }
}
