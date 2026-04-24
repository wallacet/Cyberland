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

    private readonly EntityId _stateEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;
    private World _world;

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
        var world = _world;
        ref var game = ref world.Components<GameState>().Get(_stateEntity);
        if (game.Phase != Phase.Playing || game.BallDocked)
            return;

        ref var ballTransform = ref world.Components<Transform>().Get(_ballEntity);
        ref var ballVel = ref world.Components<Velocity>().Get(_ballEntity);
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
            ref var ballTrigger = ref world.Components<Trigger>().Get(_ballEntity);
            ref var paddleTrigger = ref world.Components<Trigger>().Get(_paddleEntity);
            ballTrigger.Enabled = false;
            paddleTrigger.Enabled = false;
            return;
        }

        game.BallDocked = true;
        ref readonly var paddleTransform = ref world.Components<Transform>().Get(_paddleEntity);
        ref var paddleBody = ref world.Components<PaddleBody>().Get(_paddleEntity);
        ballPos.X = paddleTransform.WorldPosition.X;
        ballPos.Y = game.PaddleY + paddleBody.HalfHeight + Constants.BallR;
        ballVel.Value = default;
        ballTransform.LocalPosition = ballPos;
        ballTransform.WorldPosition = ballPos;
    }
}
