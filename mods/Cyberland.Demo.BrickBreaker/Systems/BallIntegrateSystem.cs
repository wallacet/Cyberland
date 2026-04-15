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

        ref var ballPos = ref world.Components<Position>().Get(_ballEntity);
        ref var ballVel = ref world.Components<Velocity>().Get(_ballEntity);
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
        ref var paddlePos = ref world.Components<Position>().Get(_paddleEntity);
        ref var paddleBody = ref world.Components<PaddleBody>().Get(_paddleEntity);
        ballPos.X = paddlePos.X;
        ballPos.Y = game.PaddleY + paddleBody.HalfHeight + Constants.BallR;
        ballVel.Value = default;
    }
}
