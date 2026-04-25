using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Resolves active global post-process settings from ECS and submits one deterministic winner per frame.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class GlobalPostProcessSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GlobalPostProcessSource>();

    /// <summary>Creates the global post-process resolver.</summary>
    public GlobalPostProcessSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = deltaSeconds;
        var renderer = _host.RendererRequired;
        var found = false;
        var bestPriority = int.MinValue;
        GlobalPostProcessSettings best = default;

        foreach (var chunk in query)
        {
            var rows = chunk.Column<GlobalPostProcessSource>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var row = ref rows[i];
                if (!row.Active || (found && row.Priority <= bestPriority))
                    continue;
                bestPriority = row.Priority;
                best = row.Settings;
                found = true;
            }
        }

        if (found)
            renderer.SetGlobalPostProcess(best);
    }
}
