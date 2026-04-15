using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

// Snake sample: Control + Session + Tilemap + VisualBundle entities. Simulation is centralized in Session; segment entities are visual only.
// Pipeline: bootstrap (OnStart only, seq) -> input (early, seq) -> tick (fixed, seq) -> tilemap layout / lights / visual sync (late, seq).
// manifest.json often sets disabled: true so publish skips this mod unless enabled.
//
// MergeStringTableAsync is synchronous here so strings exist before the first RunFrame.
public sealed class Mod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        context.LocalizedContent.MergeStringTableAsync("snake.json").GetAwaiter().GetResult();
        var w = context.World;
        var controlEntity = w.CreateEntity();
        w.Components<Control>().GetOrAdd(controlEntity);
        var sessionEntity = w.CreateEntity();
        w.Components<Session>().GetOrAdd(sessionEntity);
        var arena = w.CreateEntity();
        w.Components<Tilemap>().GetOrAdd(arena);
        var visualsEntity = w.CreateEntity();
        w.Components<VisualBundle>().GetOrAdd(visualsEntity);

        var amb = w.CreateEntity();
        w.Components<AmbientLightSource>().GetOrAdd(amb) = new AmbientLightSource { Active = true, Light = default };
        var dir = w.CreateEntity();
        w.Components<DirectionalLightSource>().GetOrAdd(dir) = new DirectionalLightSource { Active = true, Light = default };
        var spot = w.CreateEntity();
        w.Components<SpotLightSource>().GetOrAdd(spot) = new SpotLightSource { Active = true, Light = default };
        var headPt = w.CreateEntity();
        w.Components<PointLightSource>().GetOrAdd(headPt) = new PointLightSource { Active = true, Light = default };
        var foodPt = w.CreateEntity();
        w.Components<PointLightSource>().GetOrAdd(foodPt) = new PointLightSource { Active = true, Light = default };

        var host = context.Host;
        context.RegisterSequential("cyberland.demo.snake/bootstrap", new BootstrapSystem(host, sessionEntity, arena, visualsEntity));
        context.RegisterSequential("cyberland.demo.snake/input", new InputSystem(host, sessionEntity, controlEntity));
        context.RegisterSequential("cyberland.demo.snake/tick", new TickSystem(host, sessionEntity, controlEntity));
        context.RegisterSequential("cyberland.demo.snake/tilemap-layout", new TilemapLayoutSystem(host, sessionEntity, arena));
        context.RegisterSequential("cyberland.demo.snake/lights",
            new SnakeLightsFillSystem(host, sessionEntity, amb, dir, spot, headPt, foodPt));
        context.RegisterSequential("cyberland.demo.snake/visual-sync", new VisualSyncSystem(host, sessionEntity, visualsEntity));
        ApplyGlobalPost(host);
    }
    public void OnUnload() { }
    private static void ApplyGlobalPost(GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake — Post-process unavailable", "Host.Renderer was null; global HDR/bloom settings for the demo were not applied.");
            return;
        }
        r.SetGlobalPostProcess(new GlobalPostProcessSettings
        {
            BloomEnabled = true, BloomRadius = 1.1f, BloomGain = 0.26f, BloomExtractThreshold = 0.32f, BloomExtractKnee = 0.5f,
            EmissiveToHdrGain = 0.48f, EmissiveToBloomGain = 0.45f, Exposure = 1f, Saturation = 1.08f, TonemapEnabled = true,
            ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f), ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f), ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
        });
    }
}
