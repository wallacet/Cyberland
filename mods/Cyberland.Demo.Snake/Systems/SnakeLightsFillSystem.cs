using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Late phase: positions arena lights from <see cref="Session"/> (authored in <see cref="SceneSetup"/>; viewport-aware intensities).
/// </summary>
public sealed class SnakeLightsFillSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Session>();

    private EntityId _ambient;
    private EntityId _directional;
    private EntityId _spot;
    private EntityId _headPoint;
    private EntityId _foodPoint;
    private readonly GameHostServices _host;

    /// <summary>Creates the light fill pass.</summary>
    public SnakeLightsFillSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        var world = sessionRow.World;
        _ambient = world.RequireSingleEntityWith<AmbientLightSource>("Snake ambient");
        _directional = world.RequireSingleEntityWith<DirectionalLightSource>("Snake directional");
        _spot = world.RequireSingleEntityWith<SpotLightSource>("Snake spot");
        _headPoint = world.RequireSingleEntityWith<HeadFollowPointLightTag>("Snake head point light");
        _foodPoint = world.RequireSingleEntityWith<FoodFollowPointLightTag>("Snake food point light");
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
        ref var s = ref sessionRow.Get<Session>();
        s.UpdateLayout(w, h);
        var cell = s.Cell;
        var ox = s.OriginX;
        var oy = s.OriginY;
        var gridCx = ox + Constants.GridW * 0.5f * cell;
        var gridCy = oy + Constants.GridH * 0.5f * cell;
        var gridCenterWorld = WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(gridCx, gridCy), fb);

        ref var amb = ref world.Get<AmbientLightSource>(_ambient);
        amb.Active = true;
        amb.Color = new Vector3D<float>(0.22f, 0.26f, 0.32f);
        amb.Intensity = 0.13f;

        ref var dirTransform = ref world.Get<Transform>(_directional);
        dirTransform.LocalRotationRadians = MathF.Atan2(-0.58f, 0.38f);
        dirTransform.WorldRotationRadians = dirTransform.LocalRotationRadians;
        ref var dir = ref world.Get<DirectionalLightSource>(_directional);
        dir.Active = true;
        dir.Color = new Vector3D<float>(0.52f, 0.5f, 0.46f);
        dir.Intensity = 0.2f;
        dir.CastsShadow = false;

        var spotPos = WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(ox + cell * 2f, oy + cell * 1.5f), fb);
        var dirVec = LightRigMath.DirectionToOrFallback(spotPos, gridCenterWorld, new Vector2D<float>(0f, 1f));

        ref var spotTransform = ref world.Get<Transform>(_spot);
        spotTransform.LocalPosition = spotPos;
        spotTransform.LocalRotationRadians = MathF.Atan2(dirVec.Y, dirVec.X);
        spotTransform.WorldRotationRadians = spotTransform.LocalRotationRadians;
        ref var sp = ref world.Get<SpotLightSource>(_spot);
        sp.Active = true;
        sp.Radius = w * 0.52f;
        sp.InnerConeRadians = MathF.PI / 4f;
        sp.OuterConeRadians = MathF.PI / 2.15f;
        sp.Color = new Vector3D<float>(0.35f, 0.72f, 1f);
        sp.Intensity = 0.36f;
        sp.CastsShadow = false;

        var headWorld = s.Phase == Phase.Playing && s.Snake.Count > 0
            ? s.CellCenterWorld(s.Snake.First!.Value.x, s.Snake.First!.Value.y, fb)
            : gridCenterWorld;
        var headIntensity = s.Phase == Phase.Playing && s.Snake.Count > 0 ? 0.52f : 0.26f;
        ref var headTransform = ref world.Get<Transform>(_headPoint);
        headTransform.LocalPosition = headWorld;
        ref var hp = ref world.Get<PointLightSource>(_headPoint);
        hp.Active = true;
        hp.Radius = w * 0.28f;
        hp.Color = new Vector3D<float>(0.35f, 1f, 0.55f);
        hp.Intensity = headIntensity;
        hp.FalloffExponent = 2f;
        hp.CastsShadow = false;

        var foodWorld = s.Phase == Phase.Playing
            ? s.CellCenterWorld(s.Food.x, s.Food.y, fb)
            : WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(ox + (Constants.GridW - 0.5f) * cell, oy + cell * 0.5f), fb);
        var foodIntensity = s.Phase == Phase.Playing ? 0.44f : 0.22f;
        ref var foodTransform = ref world.Get<Transform>(_foodPoint);
        foodTransform.LocalPosition = foodWorld;
        ref var fp = ref world.Get<PointLightSource>(_foodPoint);
        fp.Active = true;
        fp.Radius = w * 0.22f;
        fp.Color = new Vector3D<float>(1f, 0.45f, 0.28f);
        fp.Intensity = foodIntensity;
        fp.FalloffExponent = 2.2f;
        fp.CastsShadow = false;
    }
}
