using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Grid demo: deferred G-buffer lighting with multiple point lights, emissive sprites, bloom,
/// and WBOIT on the semi-transparent game-over panel (<see cref="SnakeVisualSyncSystem"/>).
/// </summary>
public sealed class SnakeMod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        context.LocalizedContent.MergeStringTableAsync("snake.json").GetAwaiter().GetResult();

        var session = new SnakeSession();
        var w = context.World;
        var controlEntity = w.CreateEntity();
        w.Components<SnakeControl>().GetOrAdd(controlEntity);

        var arena = w.CreateEntity();
        var grid = new int[SnakeConstants.GridW * SnakeConstants.GridH];
        for (var i = 0; i < grid.Length; i++)
            grid[i] = 1;

        var host = context.Host;
        var ren = host.Renderer;
        var white = ren?.WhiteTextureId ?? 0;
        var defN = ren?.DefaultNormalTextureId ?? 0;

        var visuals = new SnakeVisualBundle();
        for (var i = 0; i < visuals.Segments.Length; i++)
        {
            var e = w.CreateEntity();
            w.Components<Position>().GetOrAdd(e);
            ref var spr = ref w.Components<Sprite>().GetOrAdd(e);
            spr = Sprite.DefaultWhiteUnlit(white, defN, new Vector2D<float>(1f, 1f));
            spr.Visible = false;
            visuals.Segments[i] = e;
        }

        visuals.Food = CreateSpriteEntity(w, white, defN);
        visuals.TitleBar = CreateSpriteEntity(w, white, defN);
        visuals.GoPanel = CreateSpriteEntity(w, white, defN);
        visuals.ScoreBar = CreateSpriteEntity(w, white, defN);

        visuals.TxtTitle = CreateHudTextEntity(w);
        visuals.TxtHintTitle = CreateHudTextEntity(w);
        visuals.TxtGameOver = CreateHudTextEntity(w);
        visuals.TxtHintGo = CreateHudTextEntity(w);
        visuals.TxtPlaying = CreateHudTextEntity(w);
        visuals.TxtScore = CreateHudTextEntity(w);

        if (host.Tilemaps is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Minor, "Cyberland.Demo.Snake — Tilemap store missing",
                "GameHostServices.Tilemaps was null; the arena grid was not registered with the tilemap data store.");
        }
        else
        {
            host.Tilemaps.Register(arena, grid, SnakeConstants.GridW, SnakeConstants.GridH);
        }

        w.Components<Tilemap>().GetOrAdd(arena);

        context.RegisterSequential("cyberland.demo.snake/input", new SnakeInputSystem(host, session, controlEntity));
        context.RegisterSequential("cyberland.demo.snake/tick", new SnakeTickSystem(host, session, controlEntity));
        context.RegisterSequential("cyberland.demo.snake/tilemap-layout", new SnakeTilemapLayoutSystem(host, session, arena));
        context.RegisterSequential("cyberland.demo.snake/lights", new SnakeLightsSystem(host, session));
        context.RegisterSequential("cyberland.demo.snake/visual-sync", new SnakeVisualSyncSystem(host, session, visuals));

        ApplySnakeGlobalPost(host);
    }

    public void OnUnload()
    {
    }

    private static EntityId CreateSpriteEntity(World world, int whiteTextureId, int normalTextureId)
    {
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        ref var spr = ref world.Components<Sprite>().GetOrAdd(e);
        spr = Sprite.DefaultWhiteUnlit(whiteTextureId, normalTextureId, new Vector2D<float>(1f, 1f));
        spr.Visible = false;
        return e;
    }

    private static EntityId CreateHudTextEntity(World world)
    {
        var e = world.CreateEntity();
        world.Components<Position>().GetOrAdd(e);
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = false;
        bt.Content = " ";
        bt.SortKey = 450f;
        bt.BaselineWorldSpace = false;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        bt.IsLocalizationKey = false;
        return e;
    }

    private static void ApplySnakeGlobalPost(GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake — Post-process unavailable",
                "Host.Renderer was null; global HDR/bloom settings for the demo were not applied.");
            return;
        }

        r.SetGlobalPostProcess(new GlobalPostProcessSettings
        {
            BloomEnabled = true,
            BloomRadius = 1.1f,
            BloomGain = 0.26f,
            BloomExtractThreshold = 0.32f,
            BloomExtractKnee = 0.5f,
            EmissiveToHdrGain = 0.48f,
            EmissiveToBloomGain = 0.45f,
            Exposure = 1f,
            Saturation = 1.08f,
            TonemapEnabled = true,
            ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f),
            ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f),
            ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
        });
    }
}
