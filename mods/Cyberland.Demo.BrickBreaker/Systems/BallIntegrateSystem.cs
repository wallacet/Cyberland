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

    public BallIntegrateSystem(EntityId stateEntity, EntityId paddleEntity, EntityId ballEntity)
    {
        _stateEntity = stateEntity;
        _paddleEntity = paddleEntity;
        _ballEntity = ballEntity;
    }

    public void OnFixedUpdate(World world, ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        ref var game = ref world.Components<GameState>().Get(_stateEntity);
        if (game.Phase != Phase.Playing || game.BallDocked)
            return;

        ref var ballTransform = ref world.Components<Transform>().Get(_ballEntity);
        ref var ballVel = ref world.Components<Velocity>().Get(_ballEntity);
        ballTransform.LocalPosition.X += ballVel.Value.X * fixedDeltaSeconds;
        ballTransform.LocalPosition.Y += ballVel.Value.Y * fixedDeltaSeconds;

        if (ballTransform.LocalPosition.X - Constants.BallR < game.ArenaMinX)
        {
            ballTransform.LocalPosition.X = game.ArenaMinX + Constants.BallR;
            ballVel.Value.X *= -1f;
        }
        else if (ballTransform.LocalPosition.X + Constants.BallR > game.ArenaMaxX)
        {
            ballTransform.LocalPosition.X = game.ArenaMaxX - Constants.BallR;
            ballVel.Value.X *= -1f;
        }

        if (ballTransform.LocalPosition.Y + Constants.BallR > game.ArenaMaxY)
        {
            ballTransform.LocalPosition.Y = game.ArenaMaxY - Constants.BallR;
            ballVel.Value.Y *= -1f;
        }

        if (ballTransform.LocalPosition.Y >= game.ArenaMinY - Constants.BallFallSafetyBand)
            return;

        game.Lives--;
        if (game.Lives <= 0)
        {
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
        ballTransform.LocalPosition.X = paddleTransform.WorldPosition.X;
        ballTransform.LocalPosition.Y = game.PaddleY + paddleBody.HalfHeight + Constants.BallR;
        ballVel.Value = default;
        ballTransform.WorldPosition = ballTransform.LocalPosition;
    }
}
