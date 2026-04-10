using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Grid demo: deferred G-buffer lighting with multiple point lights, emissive sprites, bloom,
/// and WBOIT on the semi-transparent game-over panel (<see cref="SnakeRenderSystem"/>).
/// </summary>
public sealed class SnakeMod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        var session = new SnakeSession();
        var w = context.World;
        var controlEntity = w.CreateEntity();
        w.Components<SnakeControl>().GetOrAdd(controlEntity);

        var arena = w.CreateEntity();
        var grid = new int[SnakeConstants.GridW * SnakeConstants.GridH];
        for (var i = 0; i < grid.Length; i++)
            grid[i] = 1;

        var host = context.Host;
        host.Tilemaps?.Register(arena, grid, SnakeConstants.GridW, SnakeConstants.GridH);
        w.Components<Tilemap>().GetOrAdd(arena);

        context.RegisterSequential("cyberland.demo.snake/input", new SnakeInputSystem(host, session, controlEntity));
        context.RegisterSequential("cyberland.demo.snake/tick", new SnakeTickSystem(host, session, controlEntity));
        context.RegisterSequential("cyberland.demo.snake/tilemap-layout", new SnakeTilemapLayoutSystem(host, session, arena));
        context.RegisterSequential("cyberland.demo.snake/lights", new SnakeLightsSystem(host, session));
        context.RegisterSequential("cyberland.demo.snake/render", new SnakeRenderSystem(host, session));

        ApplySnakeGlobalPost(host);
    }

    public void OnUnload()
    {
    }

    private static void ApplySnakeGlobalPost(GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
            return;

        r.SetGlobalPostProcess(new GlobalPostProcessSettings
        {
            BloomEnabled = true,
            BloomRadius = 1.1f,
            BloomGain = 0.26f,
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
