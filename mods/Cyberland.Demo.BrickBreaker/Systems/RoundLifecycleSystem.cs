using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Starts a round: resets session state and re-enables bricks. Brick reactivation iterates chunks in parallel.
/// </summary>
public sealed class RoundLifecycleSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BrickState>();


    private World _world = null!;
    private readonly EntityId _stateEntity;
    private readonly EntityId _controlEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;
    public RoundLifecycleSystem(
        EntityId stateEntity,
        EntityId controlEntity,
        EntityId paddleEntity,
        EntityId ballEntity)
    {
        _stateEntity = stateEntity;
        _controlEntity = controlEntity;
        _paddleEntity = paddleEntity;
        _ballEntity = ballEntity;
    }

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
    }

    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        _ = fixedDeltaSeconds;
        ref var control = ref _world.Get<Control>(_controlEntity);
        if (!control.StartRound)
            return;

        control.StartRound = false;
        ref var game = ref _world.Get<GameState>(_stateEntity);
        game.Phase = Phase.Playing;
        game.Lives = Constants.StartingLives;
        game.Score = 0;
        game.BallDocked = true;

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
        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                var entities = chunk.Entities;
                var states = chunk.Column<BrickState>(0);
                ref var bs = ref states[i];
                bs.Active = true;
                ref var t = ref _world.Get<Trigger>(entities[i]);
                t.Enabled = true;
            });
        }
    }
}
