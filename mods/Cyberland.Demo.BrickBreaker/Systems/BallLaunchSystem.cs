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

    private readonly EntityId _stateEntity;
    private readonly EntityId _controlEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;
    private World _world;

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
        var world = _world;
        ref var game = ref world.Components<GameState>().Get(_stateEntity);
        if (game.Phase != Phase.Playing)
            return;

        ref var control = ref world.Components<Control>().Get(_controlEntity);
        ref readonly var paddleTransform = ref world.Components<Transform>().Get(_paddleEntity);
        ref var paddleBody = ref world.Components<PaddleBody>().Get(_paddleEntity);
        ref var ballTransform = ref world.Components<Transform>().Get(_ballEntity);
        ref var ballVel = ref world.Components<Velocity>().Get(_ballEntity);

        if (game.BallDocked)
        {
            var dockedPos = new Vector2D<float>(
                paddleTransform.WorldPosition.X,
                game.PaddleY + paddleBody.HalfHeight + Constants.BallR);
            ballTransform.LocalPosition = dockedPos;
            ballTransform.WorldPosition = dockedPos;
        }

        if (!game.BallDocked || !control.LaunchBall)
            return;

        game.BallDocked = false;
        var v = new Vector2D<float>((Random.Shared.NextSingle() - 0.5f) * 0.5f, 1f);
        var len = MathF.Sqrt(v.X * v.X + v.Y * v.Y);
        if (len > 1e-5f)
            v *= Constants.BallSpeed / len;
        ballVel.Value = v;
    }
}
