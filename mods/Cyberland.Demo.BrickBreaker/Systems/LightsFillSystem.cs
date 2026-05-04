using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Per-frame arena light placement: spot aim follows <see cref="GameState"/> layout; paddle/ball point lights follow entities.
/// Ball point intensity scales with <see cref="Phase"/> (see cold start in <see cref="SceneSetup"/> for static fields).
/// </summary>
public sealed class LightsFillSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SessionTag, GameState, ArenaLightRuntime>();

    private EntityId _paddleEntity;
    private EntityId _ballEntity;
    private EntityId _spot;
    private EntityId _paddlePoint;
    private EntityId _ballPoint;

    public void OnSingletonStart(in SingletonEntity session)
    {
        var rt = session.Get<ArenaLightRuntime>();
        _paddleEntity = rt.Paddle;
        _ballEntity = rt.Ball;
        _spot = rt.Spot;
        _paddlePoint = rt.PaddlePoint;
        _ballPoint = rt.BallPoint;
    }

    public void OnSingletonLateUpdate(in SingletonEntity session, float deltaSeconds)
    {
        _ = deltaSeconds;

        var world = session.World;
        ref readonly var s = ref session.Get<GameState>();
        ref readonly var paddleTransform = ref world.Get<Transform>(_paddleEntity);
        ref readonly var ballTransform = ref world.Get<Transform>(_ballEntity);
        ref readonly var paddleBody = ref world.Get<PaddleBody>(_paddleEntity);
        var cx = (s.ArenaMinX + s.ArenaMaxX) * 0.5f;
        var brickMidY = s.BrickTopY - (Constants.Rows * 0.5f) * s.BrickH;
        var paddleX = paddleTransform.WorldPosition.X;

        var spotPos = new Vector2D<float>(s.ArenaMinX + 48f, s.BrickTopY + 20f);
        var center = new Vector2D<float>(cx, brickMidY);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(0f, -1f);

        ref var spotTransform = ref world.Get<Transform>(_spot);
        spotTransform.LocalPosition = spotPos;
        spotTransform.LocalRotationRadians = MathF.Atan2(dirVec.Y, dirVec.X);
        spotTransform.WorldRotationRadians = spotTransform.LocalRotationRadians;

        ref var paddlePointTransform = ref world.Get<Transform>(_paddlePoint);
        paddlePointTransform.LocalPosition = new Vector2D<float>(paddleX, s.PaddleY - 24f);

        var trackedBallPos = s.Phase == Phase.Playing
            ? ballTransform.WorldPosition
            : new Vector2D<float>(paddleX, s.PaddleY + paddleBody.HalfHeight + Constants.BallR);

        ref var ballPointTransform = ref world.Get<Transform>(_ballPoint);
        ballPointTransform.LocalPosition = trackedBallPos;
        ref var bp = ref world.Get<PointLightSource>(_ballPoint);
        bp.Intensity = s.Phase == Phase.Playing ? 0.55f : 0.28f;
    }
}
