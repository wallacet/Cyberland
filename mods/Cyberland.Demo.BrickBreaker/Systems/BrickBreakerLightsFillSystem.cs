using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Writes BrickBreaker arena light sources from <see cref="GameState"/> and paddle/ball positions.</summary>
public sealed class BrickBreakerLightsFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

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
        _ = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.BrickBreaker.BrickBreakerLightsFillSystem",
                "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("BrickBreakerLightsFillSystem requires Host.Renderer during OnStart.");
        }
    }

    /// <inheritdoc />
    public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var r = _host.Renderer!;
        var fb = r.SwapchainPixelSize;
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;

        ref readonly var s = ref world.Components<GameState>().Get(_stateEntity);
        ref readonly var paddlePos = ref world.Components<Position>().Get(_paddleEntity);
        ref readonly var ballPos = ref world.Components<Position>().Get(_ballEntity);
        ref readonly var paddleBody = ref world.Components<PaddleBody>().Get(_paddleEntity);
        var cx = (s.ArenaMinX + s.ArenaMaxX) * 0.5f;
        var brickMidY = s.BrickTopY - (Constants.Rows * 0.5f) * s.BrickH;
        var paddleX = paddlePos.X;

        ref var amb = ref world.Components<AmbientLightSource>().Get(_ambient);
        amb.Active = true;
        amb.Light = new AmbientLight { Color = new Vector3D<float>(0.22f, 0.24f, 0.32f), Intensity = 0.14f };

        ref var dir = ref world.Components<DirectionalLightSource>().Get(_directional);
        dir.Active = true;
        dir.Light = new DirectionalLight
        {
            DirectionWorld = new Vector2D<float>(0.35f, -0.62f),
            Color = new Vector3D<float>(0.55f, 0.52f, 0.48f),
            Intensity = 0.22f,
            CastsShadow = false
        };

        var spotPos = new Vector2D<float>(s.ArenaMinX + 48f, s.BrickTopY + 20f);
        var center = new Vector2D<float>(cx, brickMidY);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(0f, -1f);

        ref var sp = ref world.Components<SpotLightSource>().Get(_spot);
        sp.Active = true;
        sp.Light = new SpotLight
        {
            PositionWorld = spotPos,
            DirectionWorld = dirVec,
            Radius = w * 0.55f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.2f,
            Color = new Vector3D<float>(0.35f, 0.55f, 0.95f),
            Intensity = 0.38f,
            CastsShadow = false
        };

        ref var pp = ref world.Components<PointLightSource>().Get(_paddlePoint);
        pp.Active = true;
        pp.Light = new PointLight
        {
            PositionWorld = new Vector2D<float>(paddleX, s.PaddleY - 24f),
            Radius = w * 0.5f,
            Color = new Vector3D<float>(1f, 0.55f, 0.28f),
            Intensity = 0.32f,
            FalloffExponent = 2.2f,
            CastsShadow = false
        };

        var trackedBallPos = s.Phase == Phase.Playing
            ? new Vector2D<float>(ballPos.X, ballPos.Y)
            : new Vector2D<float>(paddleX, s.PaddleY + paddleBody.HalfHeight + Constants.BallR);

        ref var bp = ref world.Components<PointLightSource>().Get(_ballPoint);
        bp.Active = true;
        bp.Light = new PointLight
        {
            PositionWorld = trackedBallPos,
            Radius = 140f,
            Color = new Vector3D<float>(0.85f, 0.95f, 1f),
            Intensity = s.Phase == Phase.Playing ? 0.55f : 0.28f,
            FalloffExponent = 2f,
            CastsShadow = false
        };
    }
}
