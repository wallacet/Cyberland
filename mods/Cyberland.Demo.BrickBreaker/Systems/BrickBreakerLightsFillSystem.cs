using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Writes BrickBreaker arena light sources from <see cref="GameState"/> and paddle/ball positions.</summary>
public sealed class BrickBreakerLightsFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;


    private World _world = null!;
    private readonly GameHostServices _host;
    private readonly EntityId _stateEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;
    private readonly EntityId _ambient;
    private readonly EntityId _directional;
    private readonly EntityId _spot;
    private readonly EntityId _paddlePoint;
    private readonly EntityId _ballPoint;
    /// <summary>Creates the system.</summary>
    public BrickBreakerLightsFillSystem(GameHostServices host, EntityId stateEntity, EntityId paddleEntity, EntityId ballEntity,
        EntityId ambient, EntityId directional, EntityId spot, EntityId paddlePoint, EntityId ballPoint)
    {
        _host = host;
        _stateEntity = stateEntity;
        _paddleEntity = paddleEntity;
        _ballEntity = ballEntity;
        _ambient = ambient;
        _directional = directional;
        _spot = spot;
        _paddlePoint = paddlePoint;
        _ballPoint = ballPoint;
    }

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.BrickBreaker.BrickBreakerLightsFillSystem",
                "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("BrickBreakerLightsFillSystem requires Host.Renderer during OnStart.");
        }
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var w = Constants.CanvasWidth;

        ref readonly var s = ref _world.Get<GameState>(_stateEntity);
        ref readonly var paddleTransform = ref _world.Get<Transform>(_paddleEntity);
        ref readonly var ballTransform = ref _world.Get<Transform>(_ballEntity);
        ref readonly var paddleBody = ref _world.Get<PaddleBody>(_paddleEntity);
        var cx = (s.ArenaMinX + s.ArenaMaxX) * 0.5f;
        var brickMidY = s.BrickTopY - (Constants.Rows * 0.5f) * s.BrickH;
        var paddleX = paddleTransform.WorldPosition.X;

        ref var amb = ref _world.Get<AmbientLightSource>(_ambient);
        amb.Active = true;
        amb.Color = new Vector3D<float>(0.22f, 0.24f, 0.32f);
        amb.Intensity = 0.14f;

        ref var dirTransform = ref _world.Get<Transform>(_directional);
        dirTransform.LocalRotationRadians = MathF.Atan2(-0.62f, 0.35f);
        dirTransform.WorldRotationRadians = dirTransform.LocalRotationRadians;
        ref var dir = ref _world.Get<DirectionalLightSource>(_directional);
        dir.Active = true;
        dir.Color = new Vector3D<float>(0.55f, 0.52f, 0.48f);
        dir.Intensity = 0.22f;
        dir.CastsShadow = false;

        var spotPos = new Vector2D<float>(s.ArenaMinX + 48f, s.BrickTopY + 20f);
        var center = new Vector2D<float>(cx, brickMidY);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(0f, -1f);

        ref var spotTransform = ref _world.Get<Transform>(_spot);
        spotTransform.LocalPosition = spotPos;
        spotTransform.LocalRotationRadians = MathF.Atan2(dirVec.Y, dirVec.X);
        spotTransform.WorldRotationRadians = spotTransform.LocalRotationRadians;
        ref var sp = ref _world.Get<SpotLightSource>(_spot);
        sp.Active = true;
        sp.Radius = w * 0.55f;
        sp.InnerConeRadians = MathF.PI / 4f;
        sp.OuterConeRadians = MathF.PI / 2.2f;
        sp.Color = new Vector3D<float>(0.35f, 0.55f, 0.95f);
        sp.Intensity = 0.38f;
        sp.CastsShadow = false;

        ref var paddlePointTransform = ref _world.Get<Transform>(_paddlePoint);
        paddlePointTransform.LocalPosition = new Vector2D<float>(paddleX, s.PaddleY - 24f);
        ref var pp = ref _world.Get<PointLightSource>(_paddlePoint);
        pp.Active = true;
        pp.Radius = w * 0.5f;
        pp.Color = new Vector3D<float>(1f, 0.55f, 0.28f);
        pp.Intensity = 0.32f;
        pp.FalloffExponent = 2.2f;
        pp.CastsShadow = false;

        var trackedBallPos = s.Phase == Phase.Playing
            ? ballTransform.WorldPosition
            : new Vector2D<float>(paddleX, s.PaddleY + paddleBody.HalfHeight + Constants.BallR);

        ref var ballPointTransform = ref _world.Get<Transform>(_ballPoint);
        ballPointTransform.LocalPosition = trackedBallPos;
        ref var bp = ref _world.Get<PointLightSource>(_ballPoint);
        bp.Active = true;
        bp.Radius = 140f;
        bp.Color = new Vector3D<float>(0.85f, 0.95f, 1f);
        bp.Intensity = s.Phase == Phase.Playing ? 0.55f : 0.28f;
        bp.FalloffExponent = 2f;
        bp.CastsShadow = false;
    }
}
