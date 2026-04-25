using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

// Snake sample: Control + Session + Tilemap (background grid) + VisualBundle. Logic lives in Session; one segment
// sprite per cell (preallocated) avoids per-tick entity churn, not a requirement. Systems are all ISystem
// (sequential) for clarity; add IParallelSystem when a hot loop is worth partitioning (see cyberland-design-goals).
// Pipeline: bootstrap (OnStart) -> input (early) -> tick (fixed) -> tilemap layout / lights / visual sync (late).
// Optional: manifest.json <c>disabled: true</c> so publish omits the DLL unless you enable the mod.
public sealed class Mod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        SnakeInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("snake.json");
        var w = context.World;

        // Camera anchors Snake's gameplay to a fixed 1280x720 canvas; non-matching window sizes letterbox
        // instead of exposing more tiles. The camera sits at the canvas center so world-space sprites keep
        // the legacy "fb.Y - offset = near top" convention from before cameras were introduced.
        const int CanvasWidth = 1280;
        const int CanvasHeight = 720;
        var camera = w.CreateEntity();
        var camTransform = Transform.Identity;
        camTransform.WorldPosition = new Vector2D<float>(CanvasWidth * 0.5f, CanvasHeight * 0.5f);
        w.Components<Transform>().GetOrAdd(camera) = camTransform;
        w.Components<Camera2D>().GetOrAdd(camera) = Camera2D.Create(new Vector2D<int>(CanvasWidth, CanvasHeight));

        var controlEntity = w.CreateEntity();
        w.Components<Control>().GetOrAdd(controlEntity);
        var sessionEntity = w.CreateEntity();
        w.Components<Session>().GetOrAdd(sessionEntity);
        var arena = w.CreateEntity();
        w.Components<Tilemap>().GetOrAdd(arena);
        var visualsEntity = w.CreateEntity();
        w.Components<VisualBundle>().GetOrAdd(visualsEntity);

        var amb = w.CreateEntity();
        w.Components<AmbientLightSource>().GetOrAdd(amb) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.22f, 0.26f, 0.32f),
            Intensity = 0.13f
        };
        var dir = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(dir) = Transform.Identity;
        w.Components<DirectionalLightSource>().GetOrAdd(dir) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.52f, 0.5f, 0.46f),
            Intensity = 0.2f,
            CastsShadow = false
        };
        var spot = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(spot) = Transform.Identity;
        w.Components<SpotLightSource>().GetOrAdd(spot) = new SpotLightSource
        {
            Active = true,
            Radius = 500f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.15f,
            Color = new Vector3D<float>(0.35f, 0.72f, 1f),
            Intensity = 0.36f,
            CastsShadow = false
        };
        var headPt = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(headPt) = Transform.Identity;
        w.Components<PointLightSource>().GetOrAdd(headPt) = new PointLightSource
        {
            Active = true,
            Radius = 260f,
            Color = new Vector3D<float>(0.35f, 1f, 0.55f),
            Intensity = 0.52f,
            FalloffExponent = 2f,
            CastsShadow = false
        };
        var foodPt = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(foodPt) = Transform.Identity;
        w.Components<PointLightSource>().GetOrAdd(foodPt) = new PointLightSource
        {
            Active = true,
            Radius = 220f,
            Color = new Vector3D<float>(1f, 0.45f, 0.28f),
            Intensity = 0.44f,
            FalloffExponent = 2.2f,
            CastsShadow = false
        };

        var host = context.Host;
        context.RegisterSequential("cyberland.demo.snake/bootstrap", new BootstrapSystem(host, sessionEntity, arena, visualsEntity));
        context.RegisterSequential("cyberland.demo.snake/input", new InputSystem(host, sessionEntity, controlEntity));
        context.RegisterSequential("cyberland.demo.snake/tick", new TickSystem(host, sessionEntity, controlEntity));
        context.RegisterSequential("cyberland.demo.snake/tilemap-layout", new TilemapLayoutSystem(host, sessionEntity, arena));
        context.RegisterSequential("cyberland.demo.snake/lights",
            new SnakeLightsFillSystem(host, sessionEntity, amb, dir, spot, headPt, foodPt));
        context.RegisterSequential("cyberland.demo.snake/visual-sync", new VisualSyncSystem(host, sessionEntity, visualsEntity));
        ApplyGlobalPost(w);
    }
    public void OnUnload() { }
    private static void ApplyGlobalPost(World world)
    {
        var e = world.CreateEntity();
        world.Components<GlobalPostProcessSource>().GetOrAdd(e) = new GlobalPostProcessSource
        {
            Active = true,
            Priority = 100,
            Settings = new GlobalPostProcessSettings
            {
                BloomEnabled = true, BloomRadius = 1.1f, BloomGain = 0.26f, BloomExtractThreshold = 0.32f, BloomExtractKnee = 0.5f,
                EmissiveToHdrGain = 0.48f, EmissiveToBloomGain = 0.45f, Exposure = 1f, Saturation = 1.08f, TonemapEnabled = true,
                ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f), ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f), ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
            }
        };
    }
}
