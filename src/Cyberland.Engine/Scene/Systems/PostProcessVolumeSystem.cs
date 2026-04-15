using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Submits <see cref="PostProcessVolumeSource"/> rows to <see cref="IRenderer.SubmitPostProcessVolume"/>.
/// </summary>
public sealed class PostProcessVolumeSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;
    private readonly List<MultiComponentChunkView> _chunks = new();

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    /// <summary>Creates the system.</summary>
    public PostProcessVolumeSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Engine.PostProcessVolumeSystem",
                "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("PostProcessVolumeSystem requires Host.Renderer during OnStart.");
        }
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = archetype;
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        _chunks.Clear();
        DeferredSubmissionQueries.CollectPostProcessVolumeChunks(world, _chunks);

        if (_chunks.Count == 0)
            return;

        Parallel.For(0, _chunks.Count, parallelOptions, idx =>
        {
            var chunk = _chunks[idx];
            var col = chunk.Column<PostProcessVolumeSource>(0);
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var row = ref col[i];
                if (!row.Active)
                    continue;
                r.SubmitPostProcessVolume(in row.Volume);
            }
        });
    }
}
