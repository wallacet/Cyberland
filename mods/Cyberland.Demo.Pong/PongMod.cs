using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Pong sample: deferred G-buffer lighting with multiple point lights, emissive sprites, bloom,
/// and WBOIT on the semi-transparent hint bar (<see cref="PongVisualSyncSystem"/>).
/// </summary>
public sealed class PongMod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        var w = context.World;
        var session = w.CreateEntity();
        w.Components<PongState>().GetOrAdd(session);
        w.Components<PongControl>().GetOrAdd(session);

        static EntityId spriteEntity(World world)
        {
            var e = world.CreateEntity();
            world.Components<Position>().GetOrAdd(e);
            world.Components<Sprite>().GetOrAdd(e);
            return e;
        }

        var visuals = new PongVisualIds(
            spriteEntity(w),
            spriteEntity(w),
            spriteEntity(w),
            spriteEntity(w),
            spriteEntity(w),
            spriteEntity(w),
            spriteEntity(w),
            spriteEntity(w));

        var host = context.Host;
        context.RegisterSequential("cyberland.demo.pong/input", new PongInputSystem(host, session, context.Scheduler));
        context.RegisterSequential("cyberland.demo.pong/simulation", new PongSimulationSystem(host, session));
        context.RegisterSequential("cyberland.demo.pong/lights", new PongLightsSystem(host, session));
        context.RegisterSequential("cyberland.demo.pong/visual-sync", new PongVisualSyncSystem(host, session, visuals));

        ApplyPongGlobalPost(host);
    }

    public void OnUnload()
    {
    }

    private static void ApplyPongGlobalPost(GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
            return;

        r.SetGlobalPostProcess(new GlobalPostProcessSettings
        {
            BloomEnabled = true,
            BloomRadius = 1.1f,
            BloomGain = 0.3f,
            EmissiveToHdrGain = 0.48f,
            EmissiveToBloomGain = 0.45f,
            Exposure = 1f,
            Saturation = 1.05f,
            TonemapEnabled = true,
            ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f),
            ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f),
            ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
        });
    }
}
