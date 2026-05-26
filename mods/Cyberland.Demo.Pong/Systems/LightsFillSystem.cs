using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Late phase: writes arena lights from <see cref="State"/>; accent points follow ball / left paddle when resolved via tags.
/// </summary>
public sealed class LightsFillSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<State, Control>();

    private EntityId _ambient;
    private EntityId _directional;
    private EntityId _spot;
    private EntityId _ballPoint;
    private EntityId _leftAccentPoint;
    private readonly GameHostServices _host;

    /// <summary>Creates the fill pass; light entities are resolved at singleton startup.</summary>
    public LightsFillSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        var world = sessionRow.World;
        _ambient = world.RequireSingleEntityWith<AmbientLightSource>("Pong ambient");
        _directional = world.RequireSingleEntityWith<DirectionalLightSource>("Pong directional");
        _spot = world.RequireSingleEntityWith<SpotLightSource>("Pong spot");
        _ballPoint = world.RequireSingleEntityWith<BallAccentPointLightTag>("Pong ball accent point");
        _leftAccentPoint = world.RequireSingleEntityWith<LeftPaddleAccentPointLightTag>("Pong left paddle accent");
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity sessionRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var world = sessionRow.World;
        var r = _host.Renderer;
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;
        ref readonly var st = ref sessionRow.Get<State>();
        var arenaCx = (st.ArenaMinX + st.ArenaMaxX) * 0.5f;
        var arenaCy = (st.ArenaMinY + st.ArenaMaxY) * 0.5f;
        var center = new Vector2D<float>(arenaCx, arenaCy);

        ref var amb = ref world.Get<AmbientLightSource>(_ambient);
        amb.Active = true;
        amb.Color = new Vector3D<float>(0.2f, 0.23f, 0.3f);
        amb.Intensity = 0.12f;

        ref var dirTransform = ref world.Get<Transform>(_directional);
        dirTransform.LocalRotationRadians = MathF.Atan2(-0.6f, 0.36f);
        dirTransform.WorldRotationRadians = dirTransform.LocalRotationRadians;
        ref var dir = ref world.Get<DirectionalLightSource>(_directional);
        dir.Active = true;
        dir.Color = new Vector3D<float>(0.52f, 0.5f, 0.46f);
        dir.Intensity = 0.19f;

        var spotPos = new Vector2D<float>(st.ArenaMinX + w * 0.04f, st.ArenaMinY + h * 0.08f);
        var dirVec = LightRigMath.DirectionToOrFallback(spotPos, center, new Vector2D<float>(1f, 0f));

        ref var spotTransform = ref world.Get<Transform>(_spot);
        spotTransform.LocalPosition = spotPos;
        spotTransform.LocalRotationRadians = MathF.Atan2(dirVec.Y, dirVec.X);
        spotTransform.WorldRotationRadians = spotTransform.LocalRotationRadians;
        ref var sp = ref world.Get<SpotLightSource>(_spot);
        sp.Active = true;
        sp.Radius = w * 0.55f;
        sp.InnerConeRadians = MathF.PI / 4f;
        sp.OuterConeRadians = MathF.PI / 2.2f;
        sp.Color = new Vector3D<float>(0.38f, 0.58f, 1f);
        sp.Intensity = 0.35f;

        var ballPos = st.Phase == Phase.Playing ? st.BallPos : center;
        var ballIntensity = st.Phase == Phase.Playing ? 0.52f : 0.28f;
        ref var ballTransform = ref world.Get<Transform>(_ballPoint);
        ballTransform.LocalPosition = ballPos;
        ref var bp = ref world.Get<PointLightSource>(_ballPoint);
        bp.Active = true;
        bp.Radius = w * 0.32f;
        bp.Color = new Vector3D<float>(0.9f, 0.95f, 1f);
        bp.Intensity = ballIntensity;
        bp.FalloffExponent = 2f;

        var leftAccentY = st.Phase == Phase.Playing ? st.LeftPaddleY : arenaCy;
        ref var leftTransform = ref world.Get<Transform>(_leftAccentPoint);
        leftTransform.LocalPosition = new Vector2D<float>(st.ArenaMinX, leftAccentY);
        ref var lp = ref world.Get<PointLightSource>(_leftAccentPoint);
        lp.Active = true;
        lp.Radius = w * 0.38f;
        lp.Color = new Vector3D<float>(0.25f, 0.75f, 1f);
        lp.Intensity = st.Phase == Phase.Playing ? 0.34f : 0.2f;
        lp.FalloffExponent = 2.1f;
    }
}
