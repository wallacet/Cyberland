using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Submits arena lighting each frame for the deferred 2D path: ambient + directional + spot in the base pass,
/// multiple <see cref="PointLight"/>s via SSBO + instanced draws (all queued lights are evaluated).
/// Reads <see cref="PongState"/> from the session entity (same world space as <see cref="PongVisualSyncSystem"/>).
/// </summary>
public sealed class PongLightsSystem : ISystem
{
    private readonly GameHostServices _host;
    private readonly EntityId _session;

    public PongLightsSystem(GameHostServices host, EntityId session)
    {
        _host = host;
        _session = session;
    }

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;

        ref readonly var st = ref world.Components<PongState>().Get(_session);

        var arenaCx = (st.ArenaMinX + st.ArenaMaxX) * 0.5f;
        var arenaCy = (st.ArenaMinY + st.ArenaMaxY) * 0.5f;
        var center = new Vector2D<float>(arenaCx, arenaCy);

        r.SubmitAmbientLight(new AmbientLight
        {
            Color = new Vector3D<float>(0.2f, 0.23f, 0.3f),
            Intensity = 0.12f
        });

        r.SubmitDirectionalLight(new DirectionalLight
        {
            DirectionWorld = new Vector2D<float>(0.36f, -0.6f),
            Color = new Vector3D<float>(0.52f, 0.5f, 0.46f),
            Intensity = 0.19f,
            CastsShadow = false
        });

        var spotPos = new Vector2D<float>(st.ArenaMinX + w * 0.04f, st.ArenaMinY + h * 0.08f);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dir = dLen > 1e-4f
            ? new Vector2D<float>(dx / dLen, dy / dLen)
            : new Vector2D<float>(1f, 0f);

        r.SubmitSpotLight(new SpotLight
        {
            PositionWorld = spotPos,
            DirectionWorld = dir,
            Radius = w * 0.55f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.2f,
            Color = new Vector3D<float>(0.38f, 0.58f, 1f),
            Intensity = 0.35f,
            CastsShadow = false
        });

        Vector2D<float> ballPos;
        float ballIntensity;
        if (st.Phase == PongPhase.Playing)
        {
            ballPos = st.BallPos;
            ballIntensity = 0.52f;
        }
        else
        {
            ballPos = center;
            ballIntensity = 0.28f;
        }

        r.SubmitPointLight(new PointLight
        {
            PositionWorld = ballPos,
            Radius = w * 0.32f,
            Color = new Vector3D<float>(0.9f, 0.95f, 1f),
            Intensity = ballIntensity,
            FalloffExponent = 2f,
            CastsShadow = false
        });

        var leftAccentY = st.Phase == PongPhase.Playing ? st.LeftPaddleY : arenaCy;
        r.SubmitPointLight(new PointLight
        {
            PositionWorld = new Vector2D<float>(st.ArenaMinX, leftAccentY),
            Radius = w * 0.38f,
            Color = new Vector3D<float>(0.25f, 0.75f, 1f),
            Intensity = st.Phase == PongPhase.Playing ? 0.34f : 0.2f,
            FalloffExponent = 2.1f,
            CastsShadow = false
        });
    }
}
