using System.Collections.Generic;
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

    private readonly EntityId _stateEntity;
    private readonly EntityId _controlEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;
    private readonly List<MultiComponentChunkView> _chunks = new();

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

    public void OnParallelFixedUpdate(World world, ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        _ = fixedDeltaSeconds;
        ref var control = ref world.Components<Control>().Get(_controlEntity);
        if (!control.StartRound)
            return;

        control.StartRound = false;
        ref var game = ref world.Components<GameState>().Get(_stateEntity);
        game.Phase = Phase.Playing;
        game.Lives = Constants.StartingLives;
        game.Score = 0;
        game.BallDocked = true;

        ref var paddleTransform = ref world.Components<Transform>().Get(_paddleEntity);
        ref var paddleBody = ref world.Components<PaddleBody>().Get(_paddleEntity);
        paddleTransform.LocalPosition = new Vector2D<float>(game.LayoutWidth * 0.5f, game.PaddleY);
        paddleTransform.WorldPosition = paddleTransform.LocalPosition;
        paddleBody.HalfWidth = 72f;
        paddleBody.HalfHeight = 10f;

        ref var ballTransform = ref world.Components<Transform>().Get(_ballEntity);
        ref var ballVel = ref world.Components<Velocity>().Get(_ballEntity);
        ballTransform.LocalPosition.X = paddleTransform.LocalPosition.X;
        ballTransform.LocalPosition.Y = game.PaddleY + paddleBody.HalfHeight + Constants.BallR;
        ballTransform.WorldPosition = ballTransform.LocalPosition;
        ballVel.Value = default;

        ref var paddleTrigger = ref world.Components<Trigger>().Get(_paddleEntity);
        paddleTrigger.Enabled = true;
        paddleTrigger.HalfExtents = new Vector2D<float>(paddleBody.HalfWidth, paddleBody.HalfHeight);
        ref var ballTrigger = ref world.Components<Trigger>().Get(_ballEntity);
        ballTrigger.Enabled = true;
        ballTrigger.Radius = Constants.BallR;

        var brickTrigger = world.Components<Trigger>();
        _chunks.Clear();
        foreach (var chunk in query)
            _chunks.Add(chunk);

        Parallel.ForEach(
            _chunks,
            parallelOptions,
            chunk =>
            {
                var entities = chunk.Entities;
                var states = chunk.Column<BrickState>(0);
                for (var i = 0; i < chunk.Count; i++)
                {
                    ref var bs = ref states[i];
                    bs.Active = true;
                    ref var t = ref brickTrigger.Get(entities[i]);
                    t.Enabled = true;
                }
            });
    }
}
