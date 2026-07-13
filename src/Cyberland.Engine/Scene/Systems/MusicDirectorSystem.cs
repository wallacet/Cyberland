using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Picks the highest-priority active <see cref="MusicSource"/> and drives music playback / crossfade.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin host submit glue.")]
public sealed class MusicDirectorSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private string? _currentPath;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<MusicSource>();

    /// <summary>Creates the music director.</summary>
    public MusicDirectorSystem(GameHostServices host) => _host = host;

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
        var bestPri = int.MinValue;
        MusicSource best = default;

        foreach (var chunk in query)
        {
            var rows = chunk.Column<MusicSource>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var row = ref rows[i];
                if (!row.Active || string.IsNullOrWhiteSpace(row.ClipPath))
                    continue;
                if (found && row.Priority <= bestPri)
                    continue;
                bestPri = row.Priority;
                best = row;
                found = true;
            }
        }

        var audio = _host.Audio;
        if (!found)
        {
            if (_currentPath is not null)
            {
                audio.StopMusic(fadeOutSeconds: 1f);
                _currentPath = null;
            }
            return;
        }

        if (string.Equals(_currentPath, best.ClipPath, System.StringComparison.OrdinalIgnoreCase))
            return;

        var req = new MusicRequest
        {
            ClipPath = best.ClipPath,
            BusId = string.IsNullOrWhiteSpace(best.BusId) ? AudioBusIds.Music : best.BusId,
            Loop = best.Loop,
            FadeInSeconds = best.CrossfadeSeconds,
            Gain = 1f,
            PauseWithGameplay = best.PauseWithGameplay,
        };

        if (_currentPath is null)
            audio.PlayMusic(in req);
        else
            audio.CrossfadeMusic(in req, fadeOutSeconds: best.CrossfadeSeconds);

        _currentPath = best.ClipPath;
    }
}
