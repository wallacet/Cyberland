using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Resolves the highest-priority active global audio environment and submits it to the mixer.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin host submit glue.")]
public sealed class GlobalAudioEnvironmentSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GlobalAudioEnvironmentSource>();

    /// <summary>Creates the system.</summary>
    public GlobalAudioEnvironmentSystem(GameHostServices host) => _host = host;

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
        var found = false;
        var bestPriority = int.MinValue;
        var best = AudioEnvironmentSettings.Default;

        foreach (var chunk in query)
        {
            var rows = chunk.Column<GlobalAudioEnvironmentSource>();
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

        _host.Audio.SetGlobalAudioEnvironment(in best);
    }
}
