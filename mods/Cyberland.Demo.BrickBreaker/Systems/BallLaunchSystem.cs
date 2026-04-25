using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Keeps a docked ball attached to the paddle and launches on input. Sequential fixed.</summary>
public sealed class BallLaunchSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;


    private World _world = null!;
    private readonly EntityId _stateEntity;
    private readonly EntityId _controlEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;
    public BallLaunchSystem(EntityId stateEntity, EntityId controlEntity, EntityId paddleEntity, EntityId ballEntity)
    {
        _stateEntity = stateEntity;
        _controlEntity = controlEntity;
        _paddleEntity = paddleEntity;
        _ballEntity = ballEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
    }

    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        _ = fixedDeltaSeconds;
        ref var game = ref _world.Get<GameState>(_stateEntity);
        if (game.Phase != Phase.Playing)
            return;

        ref var control = ref _world.Get<Control>(_controlEntity);
        ref readonly var paddleTransform = ref _world.Get<Transform>(_paddleEntity);
        ref var paddleBody = ref _world.Get<PaddleBody>(_paddleEntity);
        ref var ballTransform = ref _world.Get<Transform>(_ballEntity);
        ref var ballVel = ref _world.Get<Velocity>(_ballEntity);

        if (game.BallDocked)
        {
            var dockedPos = new Vector2D<float>(
                paddleTransform.WorldPosition.X,
                game.PaddleY + paddleBody.HalfHeight + Constants.BallR);
            ballTransform.LocalPosition = dockedPos;
            ballTransform.WorldPosition = dockedPos;
        }

        if (!game.BallDocked)
        {
            // Avoid leaving LaunchBall stuck true across undocked frames (early input may set it before a fixed substep runs).
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
