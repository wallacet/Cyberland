using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Arcade sample: deferred G-buffer lighting, multiple point lights, emissive sprites,
/// bloom, and WBOIT on semi-transparent UI (<see cref="BrickVisualSyncSystem"/>).
/// </summary>
public sealed class BrickMod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        var session = new BrickSession();
        var w = context.World;
        var controlEntity = w.CreateEntity();
        w.Components<BrickControl>().GetOrAdd(controlEntity);

        static EntityId Sprite(World world)
        {
            var e = world.CreateEntity();
            world.Components<Position>().GetOrAdd(e);
            world.Components<Sprite>().GetOrAdd(e);
            return e;
        }

        var background = Sprite(w);
        var paddle = Sprite(w);
        var ball = Sprite(w);
        var titleUi = Sprite(w);
        var gameOverPanel = Sprite(w);
        var gameOverBar = Sprite(w);
        var lives = new EntityId[BrickConstants.StartingLives];
        for (var i = 0; i < lives.Length; i++)
            lives[i] = Sprite(w);

        var cells = new EntityId[BrickConstants.Cols, BrickConstants.Rows];
        for (var cx = 0; cx < BrickConstants.Cols; cx++)
        for (var cy = 0; cy < BrickConstants.Rows; cy++)
            cells[cx, cy] = Sprite(w);

        var host = context.Host;
        context.RegisterSequential("cyberland.demo.brick/input", new BrickInputSystem(host, session, controlEntity));
        context.RegisterSequential("cyberland.demo.brick/simulation", new BrickSimulationSystem(host, session, controlEntity));
        context.RegisterSequential("cyberland.demo.brick/lights", new BrickLightsSystem(host, session));
        context.RegisterSequential("cyberland.demo.brick/visual-sync",
            new BrickVisualSyncSystem(host, session, background, paddle, ball, titleUi, gameOverPanel, gameOverBar, lives, cells));

        ApplyBrickGlobalPost(host);
    }

    public void OnUnload()
    {
    }

    private static void ApplyBrickGlobalPost(GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.BrickBreaker — Post-process unavailable",
                "Host.Renderer was null; global HDR/bloom settings for the demo were not applied.");
            return;
        }

        r.SetGlobalPostProcess(new GlobalPostProcessSettings
        {
            BloomEnabled = true,
            BloomRadius = 1.1f,
            BloomGain = 0.3f,
            BloomExtractThreshold = 0.32f,
            BloomExtractKnee = 0.5f,
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
