using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Keeps a docked ball attached to the paddle and launches on input.</summary>
public sealed class BallLaunchSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BallTag, Transform, Velocity>();

    private World _world = null!;
    private EntityId _stateEntity;
    private EntityId _controlEntity;
    private EntityId _paddleEntity;

    public void OnSingletonStart(in SingletonEntity ballRow)
    {
        _world = ballRow.World;
        _stateEntity = Session.RequireStateEntity(_world);
        _controlEntity = _world.QueryChunks(SystemQuerySpec.All<ControlTag>())
            .RequireSingleEntityWith<ControlTag>("brick control");
        _paddleEntity = _world.QueryChunks(SystemQuerySpec.All<Paddle>())
            .RequireSingleEntityWith<Paddle>("brick paddle");
    }

    public void OnSingletonFixedUpdate(in SingletonEntity ballRow, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        ref var game = ref _world.Get<GameState>(_stateEntity);
        if (game.Phase != Phase.Playing)
            return;

        ref var control = ref _world.Get<Control>(_controlEntity);
        ref readonly var paddleTransform = ref _world.Get<Transform>(_paddleEntity);
        ref var paddleBody = ref _world.Get<PaddleBody>(_paddleEntity);
        ref var ballTransform = ref ballRow.Get<Transform>();
        ref var ballVel = ref ballRow.Get<Velocity>();

        if (game.BallDocked)
        {
            var dockedPos = new Vector2D<float>(
                paddleTransform.WorldPosition.X,
                game.PaddleY + paddleBody.HalfHeight + Constants.BallR);
            ballTransform.LocalPosition = dockedPos;
        }

        if (!game.BallDocked)
        {
            control.LaunchBall = false;
            return;
        }

        if (!control.LaunchBall)
            return;

        game.BallDocked = false;
        control.LaunchBall = false;
        var v = new Vector2D<float>((Random.Shared.NextSingle() - 0.5f) * 0.5f, 1f);
        var len = MathF.Sqrt(v.X * v.X + v.Y * v.Y);
        if (len > 1e-5f)
            v *= Constants.BallSpeed / len;
        ballVel.Value = v;
    }
}
