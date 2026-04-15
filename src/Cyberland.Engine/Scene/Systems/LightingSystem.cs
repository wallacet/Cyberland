using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Submits deferred lights from ECS light-source components to <see cref="IRenderer"/>.
/// </summary>
/// <remarks>
/// Point lights are submitted in parallel over chunks. Ambient, directional, and spot use at most one each per frame:
/// among active sources, the row on the entity with the greatest <see cref="EntityId.Raw"/> wins.
/// </remarks>
public sealed class LightingSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;
    private readonly List<MultiComponentChunkView> _pointChunks = new();

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    /// <summary>Creates the system.</summary>
    public LightingSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Engine.LightingSystem",
                "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("LightingSystem requires Host.Renderer during OnStart.");
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

        DeferredSubmissionQueries.SubmitBestAmbientLight(world, r);
        DeferredSubmissionQueries.SubmitBestDirectionalLight(world, r);
        DeferredSubmissionQueries.SubmitBestSpotLight(world, r);

        _pointChunks.Clear();
        DeferredSubmissionQueries.CollectPointLightChunks(world, _pointChunks);

        if (_pointChunks.Count == 0)
            return;

        Parallel.For(0, _pointChunks.Count, parallelOptions, idx =>
        {
            var chunk = _pointChunks[idx];
            var cols = chunk.Column<PointLightSource>(0);
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var s = ref cols[i];
                if (!s.Active)
                    continue;
                r.SubmitPointLight(in s.Light);
            }
        });
    }
}
