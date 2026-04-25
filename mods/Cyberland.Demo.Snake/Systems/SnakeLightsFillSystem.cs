using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>Writes Snake arena light sources from <see cref="Session"/> (entities created in <see cref="Mod.OnLoad"/>).</summary>
public sealed class SnakeLightsFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;


    private World _world = null!;
    private readonly GameHostServices _host;
    private readonly EntityId _sessionEntity;
    private readonly EntityId _ambient;
    private readonly EntityId _directional;
    private readonly EntityId _spot;
    private readonly EntityId _headPoint;
    private readonly EntityId _foodPoint;
    /// <summary>Creates the system.</summary>
    public SnakeLightsFillSystem(GameHostServices host, EntityId sessionEntity, EntityId ambient, EntityId directional,
        EntityId spot, EntityId headPoint, EntityId foodPoint)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _ambient = ambient;
        _directional = directional;
        _spot = spot;
        _headPoint = headPoint;
        _foodPoint = foodPoint;
    }

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake.SnakeLightsFillSystem",
                "Renderer was null during OnStart.");
            throw new InvalidOperationException("Renderer is required by SnakeLightsFillSystem.");
        }
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;
        ref var s = ref _world.Get<Session>(_sessionEntity);
        s.UpdateLayout(w, h);
        var cell = s.Cell;
        var ox = s.OriginX;
        var oy = s.OriginY;
        var gridCx = ox + Constants.GridW * 0.5f * cell;
        var gridCy = oy + Constants.GridH * 0.5f * cell;
        var gridCenterWorld = WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(gridCx, gridCy), fb);

        ref var amb = ref _world.Get<AmbientLightSource>(_ambient);
        amb.Active = true;
        amb.Color = new Vector3D<float>(0.22f, 0.26f, 0.32f);
        amb.Intensity = 0.13f;

        ref var dirTransform = ref _world.Get<Transform>(_directional);
        dirTransform.LocalRotationRadians = MathF.Atan2(-0.58f, 0.38f);
        dirTransform.WorldRotationRadians = dirTransform.LocalRotationRadians;
        ref var dir = ref _world.Get<DirectionalLightSource>(_directional);
        dir.Active = true;
        dir.Color = new Vector3D<float>(0.52f, 0.5f, 0.46f);
        dir.Intensity = 0.2f;
        dir.CastsShadow = false;

        var spotPos = WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(ox + cell * 2f, oy + cell * 1.5f), fb);
        var dx = gridCenterWorld.X - spotPos.X;
        var dy = gridCenterWorld.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(0f, 1f);

        ref var spotTransform = ref _world.Get<Transform>(_spot);
        spotTransform.LocalPosition = spotPos;
        spotTransform.WorldPosition = spotPos;
        spotTransform.LocalRotationRadians = MathF.Atan2(dirVec.Y, dirVec.X);
        spotTransform.WorldRotationRadians = spotTransform.LocalRotationRadians;
        ref var sp = ref _world.Get<SpotLightSource>(_spot);
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
        ref var headTransform = ref _world.Get<Transform>(_headPoint);
        headTransform.LocalPosition = headWorld;
        headTransform.WorldPosition = headWorld;
        ref var hp = ref _world.Get<PointLightSource>(_headPoint);
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
        ref var foodTransform = ref _world.Get<Transform>(_foodPoint);
        foodTransform.LocalPosition = foodWorld;
        foodTransform.WorldPosition = foodWorld;
        ref var fp = ref _world.Get<PointLightSource>(_foodPoint);
        fp.Active = true;
        fp.Radius = w * 0.22f;
        fp.Color = new Vector3D<float>(1f, 0.45f, 0.28f);
        fp.Intensity = foodIntensity;
        fp.FalloffExponent = 2.2f;
        fp.CastsShadow = false;
    }
}
