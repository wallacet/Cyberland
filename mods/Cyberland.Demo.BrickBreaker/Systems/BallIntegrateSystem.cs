using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Integrates ball motion and resolves arena wall and bottom-out outcomes. Sequential fixed (single ball entity).
/// </summary>
public sealed class BallIntegrateSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;


    private World _world = null!;
    private readonly EntityId _stateEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;
    public BallIntegrateSystem(EntityId stateEntity, EntityId paddleEntity, EntityId ballEntity)
    {
        _stateEntity = stateEntity;
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
        ref var game = ref _world.Get<GameState>(_stateEntity);
        if (game.Phase != Phase.Playing || game.BallDocked)
            return;

        ref var ballTransform = ref _world.Get<Transform>(_ballEntity);
        ref var ballVel = ref _world.Get<Velocity>(_ballEntity);
        // Stage the local position in a value, mutate .X / .Y by velocity + wall response, then assign back so the
        // property setter rebuilds the matrix once per step rather than after every axis tweak.
        var ballPos = ballTransform.LocalPosition;
        ballPos.X += ballVel.Value.X * fixedDeltaSeconds;
        ballPos.Y += ballVel.Value.Y * fixedDeltaSeconds;

        if (ballPos.X - Constants.BallR < game.ArenaMinX)
        {
            ballPos.X = game.ArenaMinX + Constants.BallR;
            ballVel.Value.X *= -1f;
        }
        else if (ballPos.X + Constants.BallR > game.ArenaMaxX)
        {
            ballPos.X = game.ArenaMaxX - Constants.BallR;
            ballVel.Value.X *= -1f;
        }

        if (ballPos.Y + Constants.BallR > game.ArenaMaxY)
        {
            ballPos.Y = game.ArenaMaxY - Constants.BallR;
            ballVel.Value.Y *= -1f;
        }

        if (ballPos.Y >= game.ArenaMinY - Constants.BallFallSafetyBand)
        {
            ballTransform.LocalPosition = ballPos;
            return;
        }

        game.Lives--;
        if (game.Lives <= 0)
        {
            ballTransform.LocalPosition = ballPos;
            game.Phase = Phase.GameOver;
            ref var ballTrigger = ref _world.Get<Trigger>(_ballEntity);
            ref var paddleTrigger = ref _world.Get<Trigger>(_paddleEntity);
            ballTrigger.Enabled = false;
            paddleTrigger.Enabled = false;
            return;
        }

        game.BallDocked = true;
        ref readonly var paddleTransform = ref _world.Get<Transform>(_paddleEntity);
        ref var paddleBody = ref _world.Get<PaddleBody>(_paddleEntity);
        ballPos.X = paddleTransform.WorldPosition.X;
        ballPos.Y = game.PaddleY + paddleBody.HalfHeight + Constants.BallR;
        ballVel.Value = default;
        ballTransform.LocalPosition = ballPos;
    }
}
