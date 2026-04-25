using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel text runtime builder: resolves/localizes and shapes text into cached sprite runs.
/// </summary>
public sealed class TextBuildSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec =>
        SystemQuerySpec.All<BitmapText, Transform, TextBuildFingerprint, TextSpriteCache>();

    /// <summary>Creates the parallel text build stage.</summary>
    public TextBuildSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions options)
    {
        _ = deltaSeconds;
        var renderer = _host.Renderer;
        if (renderer is null)
            return;

        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, options, i =>
            {
                ref var bt = ref chunk.Column<BitmapText>()[i];
                ref var fingerprint = ref chunk.Column<TextBuildFingerprint>()[i];
                ref var cache = ref chunk.Column<TextSpriteCache>()[i];
                ref readonly var transform = ref chunk.Column<Transform>()[i];
                TextRuntimeBuilder.TryPrepare(ref bt, ref fingerprint, ref cache, in transform, _host, renderer, out _, out _);
            });
        }
    }
}
