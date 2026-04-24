using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>Writes Pong arena light sources from <see cref="State"/> (entities created in <see cref="Mod.OnLoad"/>).</summary>
public sealed class PongLightsFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly EntityId _ambient;
    private readonly EntityId _directional;
    private readonly EntityId _spot;
    private readonly EntityId _ballPoint;
    private readonly EntityId _leftAccentPoint;
    private World _world = null!;

    /// <summary>Creates the system.</summary>
    public PongLightsFillSystem(GameHostServices host, EntityId session, EntityId ambient, EntityId directional, EntityId spot,
        EntityId ballPoint, EntityId leftAccentPoint)
    {
        _host = host;
        _session = session;
        _ambient = ambient;
        _directional = directional;
        _spot = spot;
        _ballPoint = ballPoint;
        _leftAccentPoint = leftAccentPoint;
    }

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Pong.PongLightsFillSystem",
                "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("PongLightsFillSystem requires a renderer.");
        }
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var world = _world;
        var r = _host.Renderer!;
        // Lights are authored in world units that match the camera's virtual viewport; using
        // ActiveCameraViewportSize keeps relative sizes stable regardless of the physical window.
        var fb = r.ActiveCameraViewportSize;
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;
        ref readonly var st = ref world.Components<State>().Get(_session);
        var arenaCx = (st.ArenaMinX + st.ArenaMaxX) * 0.5f;
        var arenaCy = (st.ArenaMinY + st.ArenaMaxY) * 0.5f;
        var center = new Vector2D<float>(arenaCx, arenaCy);

        ref var amb = ref world.Components<AmbientLightSource>().Get(_ambient);
        amb.Active = true;
        amb.Color = new Vector3D<float>(0.2f, 0.23f, 0.3f);
        amb.Intensity = 0.12f;

        ref var dirTransform = ref world.Components<Transform>().Get(_directional);
        dirTransform.LocalRotationRadians = MathF.Atan2(-0.6f, 0.36f);
        dirTransform.WorldRotationRadians = dirTransform.LocalRotationRadians;
        ref var dir = ref world.Components<DirectionalLightSource>().Get(_directional);
        dir.Active = true;
        dir.Color = new Vector3D<float>(0.52f, 0.5f, 0.46f);
        dir.Intensity = 0.19f;
        dir.CastsShadow = false;

        var spotPos = new Vector2D<float>(st.ArenaMinX + w * 0.04f, st.ArenaMinY + h * 0.08f);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(1f, 0f);

        ref var spotTransform = ref world.Components<Transform>().Get(_spot);
        spotTransform.LocalPosition = spotPos;
        spotTransform.WorldPosition = spotPos;
        spotTransform.LocalRotationRadians = MathF.Atan2(dirVec.Y, dirVec.X);
        spotTransform.WorldRotationRadians = spotTransform.LocalRotationRadians;
        ref var sp = ref world.Components<SpotLightSource>().Get(_spot);
        sp.Active = true;
        sp.Radius = w * 0.55f;
        sp.InnerConeRadians = MathF.PI / 4f;
        sp.OuterConeRadians = MathF.PI / 2.2f;
        sp.Color = new Vector3D<float>(0.38f, 0.58f, 1f);
        sp.Intensity = 0.35f;
        sp.CastsShadow = false;

        var ballPos = st.Phase == Phase.Playing ? st.BallPos : center;
        var ballIntensity = st.Phase == Phase.Playing ? 0.52f : 0.28f;
        ref var ballTransform = ref world.Components<Transform>().Get(_ballPoint);
        ballTransform.LocalPosition = ballPos;
        ballTransform.WorldPosition = ballPos;
        ref var bp = ref world.Components<PointLightSource>().Get(_ballPoint);
        bp.Active = true;
        bp.Radius = w * 0.32f;
        bp.Color = new Vector3D<float>(0.9f, 0.95f, 1f);
        bp.Intensity = ballIntensity;
        bp.FalloffExponent = 2f;
        bp.CastsShadow = false;

        var leftAccentY = st.Phase == Phase.Playing ? st.LeftPaddleY : arenaCy;
        ref var leftTransform = ref world.Components<Transform>().Get(_leftAccentPoint);
        leftTransform.LocalPosition = new Vector2D<float>(st.ArenaMinX, leftAccentY);
        leftTransform.WorldPosition = leftTransform.LocalPosition;
        ref var lp = ref world.Components<PointLightSource>().Get(_leftAccentPoint);
        lp.Active = true;
        lp.Radius = w * 0.38f;
        lp.Color = new Vector3D<float>(0.25f, 0.75f, 1f);
        lp.Intensity = st.Phase == Phase.Playing ? 0.34f : 0.2f;
        lp.FalloffExponent = 2.1f;
        lp.CastsShadow = false;
    }
}
