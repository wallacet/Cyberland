using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Submits arena lighting each frame for the deferred path: ambient + directional + spot in the base pass,
/// multiple <see cref="PointLight"/>s via SSBO + instanced draws (all queued lights are evaluated).
/// Positions match <see cref="SnakeVisualSyncSystem"/> (grid to world via <see cref="SnakeSession"/> layout).
/// </summary>
public sealed class SnakeLightsSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private readonly SnakeSession _session;

    public SnakeLightsSystem(GameHostServices host, SnakeSession session)
    {
        _host = host;
        _session = session;
    }

    public void OnLateUpdate(World world, float deltaSeconds)
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
        s.UpdateLayout(w, h);

        var cell = s.Cell;
        var ox = s.OriginX;
        var oy = s.OriginY;
        var gridCx = ox + SnakeConstants.GridW * 0.5f * cell;
        var gridCy = oy + SnakeConstants.GridH * 0.5f * cell;
        var gridCenterWorld =
            WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(gridCx, gridCy), fb);

        r.SubmitAmbientLight(new AmbientLight
        {
            Color = new Vector3D<float>(0.22f, 0.26f, 0.32f),
            Intensity = 0.13f
        });

        r.SubmitDirectionalLight(new DirectionalLight
        {
            DirectionWorld = new Vector2D<float>(0.38f, -0.58f),
            Color = new Vector3D<float>(0.52f, 0.5f, 0.46f),
            Intensity = 0.2f,
            CastsShadow = false
        });

        var spotPosScreen = new Vector2D<float>(ox + cell * 2f, oy + cell * 1.5f);
        var spotPos = WorldScreenSpace.ScreenPixelToWorldCenter(spotPosScreen, fb);
        var dx = gridCenterWorld.X - spotPos.X;
        var dy = gridCenterWorld.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dir = dLen > 1e-4f
            ? new Vector2D<float>(dx / dLen, dy / dLen)
            : new Vector2D<float>(0f, 1f);

        r.SubmitSpotLight(new SpotLight
        {
            PositionWorld = spotPos,
            DirectionWorld = dir,
            Radius = w * 0.52f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.15f,
            Color = new Vector3D<float>(0.35f, 0.72f, 1f),
            Intensity = 0.36f,
            CastsShadow = false
        });

        Vector2D<float> headWorld;
        float headIntensity;
        if (s.Phase == SnakePhase.Playing && s.Snake.Count > 0)
        {
            var head = s.Snake.First!.Value;
            headWorld = s.CellCenterWorld(head.x, head.y, fb);
            headIntensity = 0.52f;
        }
        else
        {
            headWorld = gridCenterWorld;
            headIntensity = 0.26f;
        }

        r.SubmitPointLight(new PointLight
        {
            PositionWorld = headWorld,
            Radius = w * 0.28f,
            Color = new Vector3D<float>(0.35f, 1f, 0.55f),
            Intensity = headIntensity,
            FalloffExponent = 2f,
            CastsShadow = false
        });

        Vector2D<float> foodWorld;
        float foodIntensity;
        if (s.Phase == SnakePhase.Playing)
        {
            foodWorld = s.CellCenterWorld(s.Food.x, s.Food.y, fb);
            foodIntensity = 0.44f;
        }
        else
        {
            var foodScreen = new Vector2D<float>(
                ox + (SnakeConstants.GridW - 0.5f) * cell,
                oy + cell * 0.5f);
            foodWorld = WorldScreenSpace.ScreenPixelToWorldCenter(foodScreen, fb);
            foodIntensity = 0.22f;
        }

        r.SubmitPointLight(new PointLight
        {
            PositionWorld = foodWorld,
            Radius = w * 0.22f,
            Color = new Vector3D<float>(1f, 0.45f, 0.28f),
            Intensity = foodIntensity,
            FalloffExponent = 2.2f,
            CastsShadow = false
        });
    }
}
