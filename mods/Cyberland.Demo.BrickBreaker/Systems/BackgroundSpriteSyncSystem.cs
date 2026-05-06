using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Late parallel: fullscreen backdrop sprite tracks presentation viewport pixels.</summary>
public sealed class BackgroundSpriteSyncSystem : IParallelSystem, IParallelLateUpdate
{
    private const int SmallBatchSerialThreshold = 8;
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BackgroundTag, Transform, Sprite>();

    private readonly GameHostServices _host;

    public BackgroundSpriteSyncSystem(GameHostServices host) => _host = host;

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = archetype;
    }

    public void OnParallelLateUpdate(ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        foreach (var chunk in archetype)
        {
            if (chunk.Count <= SmallBatchSerialThreshold)
            {
                for (var i = 0; i < chunk.Count; i++)
                    ApplyBackground(chunk, i, fb);
                continue;
            }

            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ApplyBackground(chunk, i, fb);
            });
        }
    }

    private static void ApplyBackground(in MultiComponentChunkView chunk, int index, in Vector2D<int> framebufferSize)
    {
        ref var tr = ref chunk.Column<Transform>()[index];
        tr.LocalPosition = new Vector2D<float>(framebufferSize.X * 0.5f, framebufferSize.Y * 0.5f);
        ref var spr = ref chunk.Column<Sprite>()[index];
        spr.HalfExtents = new Vector2D<float>(framebufferSize.X * 0.5f, framebufferSize.Y * 0.5f);
    }
}
