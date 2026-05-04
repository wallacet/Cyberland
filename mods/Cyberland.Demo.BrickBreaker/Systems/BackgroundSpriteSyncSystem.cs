using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Late parallel: fullscreen backdrop sprite tracks presentation viewport pixels.</summary>
public sealed class BackgroundSpriteSyncSystem : IParallelSystem, IParallelLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BackgroundTag, Transform, Sprite>();

    private readonly GameHostServices _host;

    public BackgroundSpriteSyncSystem(GameHostServices host) => _host = host;

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = archetype;
        _ = _host.Renderer ?? throw new InvalidOperationException("brick/background-sprite requires Host.Renderer.");
    }

    public void OnParallelLateUpdate(ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer!;
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        foreach (var chunk in archetype)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref var tr = ref chunk.Column<Transform>()[i];
                tr.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
                ref var spr = ref chunk.Column<Sprite>()[i];
                spr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
            });
        }
    }
}
