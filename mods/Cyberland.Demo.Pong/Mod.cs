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
        context.LocalizedContent.MergeStringTableAsync("pong.json").GetAwaiter().GetResult();
        var world = context.World;
        var session = world.CreateEntity();
        world.Components<State>().GetOrAdd(session);
        world.Components<Control>().GetOrAdd(session);

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
        world.Components<AmbientLightSource>().GetOrAdd(amb) = new AmbientLightSource { Active = true, Light = default };
        var dir = world.CreateEntity();
        world.Components<DirectionalLightSource>().GetOrAdd(dir) = new DirectionalLightSource { Active = true, Light = default };
        var spot = world.CreateEntity();
        world.Components<SpotLightSource>().GetOrAdd(spot) = new SpotLightSource { Active = true, Light = default };
        var ballPt = world.CreateEntity();
        world.Components<PointLightSource>().GetOrAdd(ballPt) = new PointLightSource { Active = true, Light = default };
        var leftPt = world.CreateEntity();
        world.Components<PointLightSource>().GetOrAdd(leftPt) = new PointLightSource { Active = true, Light = default };

        var host = context.Host;
        context.RegisterSequential("cyberland.demo.pong/input", new InputSystem(host, session, context.Scheduler));
        context.RegisterSequential("cyberland.demo.pong/simulation", new SimulationSystem(host, session, visuals));
        context.RegisterSequential("cyberland.demo.pong/lights",
            new PongLightsFillSystem(host, session, amb, dir, spot, ballPt, leftPt));
        context.RegisterSequential("cyberland.demo.pong/visual-sync", new VisualSyncSystem(host, session, visuals, texts));

        ApplyGlobalPost(host);
    }

    public void OnUnload() { }

    private static EntityId CreateSpriteEntity(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Position>().GetOrAdd(entity);
        world.Components<Sprite>().GetOrAdd(entity);
        return entity;
    }

    private static EntityId CreateHudTextEntity(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Position>().GetOrAdd(entity);
        ref var text = ref world.Components<BitmapText>().GetOrAdd(entity);
        text.Visible = false;
        text.Content = " ";
        text.SortKey = 450f;
        text.CoordinateSpace = CoordinateSpace.ScreenSpace;
        text.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        text.IsLocalizationKey = false;
        return entity;
    }

    private static void ApplyGlobalPost(GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Pong — Post-process unavailable", "Host.Renderer was null; global HDR/bloom settings for the demo were not applied.");
            return;
        }
        r.SetGlobalPostProcess(new GlobalPostProcessSettings
        {
            BloomEnabled = true, BloomRadius = 1.1f, BloomGain = 0.3f, BloomExtractThreshold = 0.32f, BloomExtractKnee = 0.5f,
            EmissiveToHdrGain = 0.48f, EmissiveToBloomGain = 0.45f, Exposure = 1f, Saturation = 1.05f, TonemapEnabled = true,
            ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f), ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f), ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
        });
    }
}
