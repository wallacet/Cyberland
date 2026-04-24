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
    private ComponentStore<Control> _control = default!;
    private ComponentStore<GameState> _game = default!;
    private ComponentStore<Transform> _transforms = default!;
    private ComponentStore<PaddleBody> _paddleBodies = default!;
    private ComponentStore<Velocity> _velocities = default!;
    private ComponentStore<Trigger> _triggers = default!;

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
        _control = world.Components<Control>();
        _game = world.Components<GameState>();
        _transforms = world.Components<Transform>();
        _paddleBodies = world.Components<PaddleBody>();
        _velocities = world.Components<Velocity>();
        _triggers = world.Components<Trigger>();
        _ = query;
    }

    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        _ = fixedDeltaSeconds;
        ref var control = ref _control.Get(_controlEntity);
        if (!control.StartRound)
            return;

        control.StartRound = false;
        ref var game = ref _game.Get(_stateEntity);
        game.Phase = Phase.Playing;
        game.Lives = Constants.StartingLives;
        game.Score = 0;
        game.BallDocked = true;

        ref var paddleTransform = ref _transforms.Get(_paddleEntity);
        ref var paddleBody = ref _paddleBodies.Get(_paddleEntity);
        var paddlePos = new Vector2D<float>(game.LayoutWidth * 0.5f, game.PaddleY);
        paddleTransform.LocalPosition = paddlePos;
        paddleTransform.WorldPosition = paddlePos;
        paddleBody.HalfWidth = 72f;
        paddleBody.HalfHeight = 10f;

        ref var ballTransform = ref _transforms.Get(_ballEntity);
        ref var ballVel = ref _velocities.Get(_ballEntity);
        var ballPos = new Vector2D<float>(paddlePos.X, game.PaddleY + paddleBody.HalfHeight + Constants.BallR);
        ballTransform.LocalPosition = ballPos;
        ballTransform.WorldPosition = ballPos;
        ballVel.Value = default;

        ref var paddleTrigger = ref _triggers.Get(_paddleEntity);
        paddleTrigger.Enabled = true;
        paddleTrigger.HalfExtents = new Vector2D<float>(paddleBody.HalfWidth, paddleBody.HalfHeight);
        ref var ballTrigger = ref _triggers.Get(_ballEntity);
        ballTrigger.Enabled = true;
        ballTrigger.Radius = Constants.BallR;
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
                    ref var t = ref _triggers.Get(entities[i]);
                    t.Enabled = true;
                }
            });
    }
}
