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
        _ = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake.SnakeLightsFillSystem",
                "Renderer was null during OnStart.");
            throw new InvalidOperationException("Renderer is required by SnakeLightsFillSystem.");
        }
    }

    /// <inheritdoc />
    public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;
        var fb = r.SwapchainPixelSize;
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;
        ref var s = ref world.Components<Session>().Get(_sessionEntity);
        s.UpdateLayout(w, h);
        var cell = s.Cell;
        var ox = s.OriginX;
        var oy = s.OriginY;
        var gridCx = ox + Constants.GridW * 0.5f * cell;
        var gridCy = oy + Constants.GridH * 0.5f * cell;
        var gridCenterWorld = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(gridCx, gridCy), fb);

        ref var amb = ref world.Components<AmbientLightSource>().Get(_ambient);
        amb.Active = true;
        amb.Light = new AmbientLight { Color = new Vector3D<float>(0.22f, 0.26f, 0.32f), Intensity = 0.13f };

        ref var dir = ref world.Components<DirectionalLightSource>().Get(_directional);
        dir.Active = true;
        dir.Light = new DirectionalLight
        {
            DirectionWorld = new Vector2D<float>(0.38f, -0.58f),
            Color = new Vector3D<float>(0.52f, 0.5f, 0.46f),
            Intensity = 0.2f,
            CastsShadow = false
        };

        var spotPos = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(ox + cell * 2f, oy + cell * 1.5f), fb);
        var dx = gridCenterWorld.X - spotPos.X;
        var dy = gridCenterWorld.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(0f, 1f);

        ref var sp = ref world.Components<SpotLightSource>().Get(_spot);
        sp.Active = true;
        sp.Light = new SpotLight
        {
            PositionWorld = spotPos,
            DirectionWorld = dirVec,
            Radius = w * 0.52f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.15f,
            Color = new Vector3D<float>(0.35f, 0.72f, 1f),
            Intensity = 0.36f,
            CastsShadow = false
        };

        var headWorld = s.Phase == Phase.Playing && s.Snake.Count > 0
            ? s.CellCenterWorld(s.Snake.First!.Value.x, s.Snake.First!.Value.y, fb)
            : gridCenterWorld;
        var headIntensity = s.Phase == Phase.Playing && s.Snake.Count > 0 ? 0.52f : 0.26f;
        ref var hp = ref world.Components<PointLightSource>().Get(_headPoint);
        hp.Active = true;
        hp.Light = new PointLight
        {
            PositionWorld = headWorld,
            Radius = w * 0.28f,
            Color = new Vector3D<float>(0.35f, 1f, 0.55f),
            Intensity = headIntensity,
            FalloffExponent = 2f,
            CastsShadow = false
        };

        var foodWorld = s.Phase == Phase.Playing
            ? s.CellCenterWorld(s.Food.x, s.Food.y, fb)
            : WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(ox + (Constants.GridW - 0.5f) * cell, oy + cell * 0.5f), fb);
        var foodIntensity = s.Phase == Phase.Playing ? 0.44f : 0.22f;
        ref var fp = ref world.Components<PointLightSource>().Get(_foodPoint);
        fp.Active = true;
        fp.Light = new PointLight
        {
            PositionWorld = foodWorld,
            Radius = w * 0.22f,
            Color = new Vector3D<float>(1f, 0.45f, 0.28f),
            Intensity = foodIntensity,
            FalloffExponent = 2.2f,
            CastsShadow = false
        };
    }
}
