using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Submits arena lighting each frame for the deferred 2D path: ambient + directional + spot in the base pass,
/// multiple <see cref="PointLight"/>s via SSBO + instanced draws (all queued lights are evaluated).
/// </summary>
public sealed class BrickLightsSystem : ISystem
{
    private readonly GameHostServices _host;
    private readonly BrickSession _session;

    public BrickLightsSystem(GameHostServices host, BrickSession session)
    {
        _host = host;
        _session = session;
    }

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = world;
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;

        var s = _session;
        var cx = (s.ArenaMinX + s.ArenaMaxX) * 0.5f;
        var brickMidY = s.BrickTopY - (BrickConstants.Rows * 0.5f) * s.BrickH;
        var paddleX = s.PaddleCenterX > 1f ? s.PaddleCenterX : w * 0.5f;

        r.SubmitAmbientLight(new AmbientLight
        {
            Color = new Vector3D<float>(0.22f, 0.24f, 0.32f),
            Intensity = 0.14f
        });

        r.SubmitDirectionalLight(new DirectionalLight
        {
            DirectionWorld = new Vector2D<float>(0.35f, -0.62f),
            Color = new Vector3D<float>(0.55f, 0.52f, 0.48f),
            Intensity = 0.22f,
            CastsShadow = false
        });

        var spotPos = new Vector2D<float>(s.ArenaMinX + 48f, s.BrickTopY + 20f);
        var center = new Vector2D<float>(cx, brickMidY);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dir = dLen > 1e-4f
            ? new Vector2D<float>(dx / dLen, dy / dLen)
            : new Vector2D<float>(0f, -1f);

        r.SubmitSpotLight(new SpotLight
        {
            PositionWorld = spotPos,
            DirectionWorld = dir,
            Radius = w * 0.55f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.2f,
            Color = new Vector3D<float>(0.35f, 0.55f, 0.95f),
            Intensity = 0.38f,
            CastsShadow = false
        });

        // Warm fill along the bottom playfield (contrasts with cool spot).
        r.SubmitPointLight(new PointLight
        {
            PositionWorld = new Vector2D<float>(paddleX, s.PaddleY - 24f),
            Radius = w * 0.5f,
            Color = new Vector3D<float>(1f, 0.55f, 0.28f),
            Intensity = 0.32f,
            FalloffExponent = 2.2f,
            CastsShadow = false
        });

        var ballPos = s.Phase == BrickPhase.Playing
            ? s.BallPos
            : new Vector2D<float>(paddleX, s.PaddleY + s.PaddleHalfH + BrickConstants.BallR);

        r.SubmitPointLight(new PointLight
        {
            PositionWorld = ballPos,
            Radius = 140f,
            Color = new Vector3D<float>(0.85f, 0.95f, 1f),
            Intensity = s.Phase == BrickPhase.Playing ? 0.55f : 0.28f,
            FalloffExponent = 2f,
            CastsShadow = false
        });
    }
}
