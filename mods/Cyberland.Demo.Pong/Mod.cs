using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

// Pong sample: one session entity holds State + Control; sprites/HUD use explicit VisualIds / HudTextIds (handles, not tag queries).
// Pipeline: sequential early input -> sequential fixed simulation -> sequential late lights + visual sync.
// All systems are ISystem: this demo has a single gameplay entity, so there is no meaningful ECS chunk parallelism.
//
// MergeStringTableAsync blocks here because mod load is synchronous; strings must exist before the first RunFrame.
public sealed class Mod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        PongInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("pong.json");
        var world = context.World;
        var session = world.CreateEntity();
        world.Components<State>().GetOrAdd(session);
        world.Components<Control>().GetOrAdd(session);

        // Pong authors gameplay in a fixed 1280x720 canvas; anchor the camera at the center so world-space
        // sprites (ball, paddles) line up with the legacy "fb.X/2, fb.Y/2" layout without rewriting the
        // simulation.
        const int CanvasWidth = 1280;
        const int CanvasHeight = 720;
        var camera = world.CreateEntity();
        var camTransform = Transform.Identity;
        camTransform.WorldPosition = new Vector2D<float>(CanvasWidth * 0.5f, CanvasHeight * 0.5f);
        world.Components<Transform>().GetOrAdd(camera) = camTransform;
        world.Components<Camera2D>().GetOrAdd(camera) = Camera2D.Create(new Vector2D<int>(CanvasWidth, CanvasHeight));

        var visuals = new VisualIds(
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world));

        var texts = new HudTextIds(
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world));

        var amb = world.CreateEntity();
        world.Components<AmbientLightSource>().GetOrAdd(amb) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.2f, 0.23f, 0.3f),
            Intensity = 0.12f
        };
        var dir = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(dir) = Transform.Identity;
        world.Components<DirectionalLightSource>().GetOrAdd(dir) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.52f, 0.5f, 0.46f),
            Intensity = 0.19f,
            CastsShadow = false
        };
        var spot = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(spot) = Transform.Identity;
        world.Components<SpotLightSource>().GetOrAdd(spot) = new SpotLightSource
        {
            Active = true,
            Radius = 460f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.2f,
            Color = new Vector3D<float>(0.38f, 0.58f, 1f),
            Intensity = 0.35f,
            CastsShadow = false
        };
        var ballPt = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(ballPt) = Transform.Identity;
        world.Components<PointLightSource>().GetOrAdd(ballPt) = new PointLightSource
        {
            Active = true,
            Radius = 280f,
            Color = new Vector3D<float>(0.9f, 0.95f, 1f),
            Intensity = 0.52f,
            FalloffExponent = 2f,
            CastsShadow = false
        };
        var leftPt = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(leftPt) = Transform.Identity;
        world.Components<PointLightSource>().GetOrAdd(leftPt) = new PointLightSource
        {
            Active = true,
            Radius = 320f,
            Color = new Vector3D<float>(0.25f, 0.75f, 1f),
            Intensity = 0.34f,
            FalloffExponent = 2.1f,
            CastsShadow = false
        };

        var host = context.Host;
        context.RegisterSequential("cyberland.demo.pong/input", new InputSystem(host, session, context.Scheduler));
        context.RegisterSequential("cyberland.demo.pong/simulation", new SimulationSystem(host, session, visuals));
        context.RegisterSequential("cyberland.demo.pong/lights",
            new PongLightsFillSystem(host, session, amb, dir, spot, ballPt, leftPt));
        context.RegisterSequential("cyberland.demo.pong/visual-sync", new VisualSyncSystem(host, session, visuals, texts));

        ApplyGlobalPost(world);
    }

    public void OnUnload() { }

    private static EntityId CreateSpriteEntity(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(entity) = Transform.Identity;
        world.Components<Sprite>().GetOrAdd(entity);
        return entity;
    }

    private static EntityId CreateHudTextEntity(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(entity) = Transform.Identity;
        ref var text = ref world.Components<BitmapText>().GetOrAdd(entity);
        text.Visible = false;
        text.Content = " ";
        text.SortKey = 450f;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
        text.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        text.IsLocalizationKey = false;
        return entity;
    }

    private static void ApplyGlobalPost(World world)
    {
        var e = world.CreateEntity();
        world.Components<GlobalPostProcessSource>().GetOrAdd(e) = new GlobalPostProcessSource
        {
            Active = true,
            Priority = 100,
            Settings = new GlobalPostProcessSettings
            {
            BloomEnabled = true, BloomRadius = 1.1f, BloomGain = 0.3f, BloomExtractThreshold = 0.32f, BloomExtractKnee = 0.5f,
            EmissiveToHdrGain = 0.48f, EmissiveToBloomGain = 0.45f, Exposure = 1f, Saturation = 1.05f, TonemapEnabled = true,
            ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f), ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f), ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
            }
        };
    }
}
