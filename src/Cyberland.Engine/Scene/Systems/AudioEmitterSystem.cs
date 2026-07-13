using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Drives <see cref="AudioEmitterSource"/> rows: play-on-enable one-shots/cues and looping beds.
/// Serial because it mutates runtime voice ids on component rows.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin host submit glue.")]
public sealed class AudioEmitterSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<AudioEmitterSource, Transform>();

    /// <summary>Creates the system.</summary>
    public AudioEmitterSystem(GameHostServices host) => _host = host;

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
        var audio = _host.Audio;
        foreach (var chunk in query)
        {
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var row = ref chunk.Column<AudioEmitterSource>()[i];
                if (!row.Active)
                {
                    if (row.RuntimeVoice.IsValid)
                    {
                        audio.Stop(row.RuntimeVoice, fadeOutSeconds: row.FadeInSeconds);
                        row.RuntimeVoice = VoiceId.None;
                        row.RuntimeStarted = false;
                    }
                    continue;
                }

                ref readonly var tf = ref chunk.Column<Transform>()[i];
                TransformMath.DecomposeToPRS(tf.WorldMatrix, out var worldPos, out _, out _);

                if (row.Loop)
                {
                    if (!row.RuntimeVoice.IsValid)
                    {
                        row.RuntimeVoice = audio.PlayLoop(new LoopRequest
                        {
                            ClipPath = row.ClipPath,
                            Space = row.Space,
                            BusId = row.BusId,
                            Gain = row.Gain <= 0f ? 1f : row.Gain,
                            Pitch = row.Pitch <= 0f ? 1f : row.Pitch,
                            Priority = row.Priority,
                            FadeInSeconds = row.FadeInSeconds,
                            PauseWithGameplay = row.PauseWithGameplay,
                            ApplyTimeScale = true,
                            PositionWorld = worldPos,
                            RefDistance = row.RefDistance > 0f ? row.RefDistance : 64f,
                            MaxDistance = row.MaxDistance > 0f ? row.MaxDistance : 480f,
                            Rolloff = row.Rolloff > 0f ? row.Rolloff : 1f,
                        });
                        row.RuntimeStarted = true;
                    }
                    else
                    {
                        audio.SetVoiceParams(row.RuntimeVoice, positionWorld: worldPos);
                    }
                    continue;
                }

                if (row.PlayOnEnable && !row.RuntimeStarted)
                {
                    row.RuntimeStarted = true;
                    if (!string.IsNullOrWhiteSpace(row.CueId))
                    {
                        audio.PlayCue(row.CueId, new PlayCueRequest
                        {
                            HasSpace = true,
                            Space = row.Space,
                            PositionWorld = worldPos,
                            BusId = row.BusId,
                            Gain = row.Gain <= 0f ? 1f : row.Gain,
                            Priority = row.Priority,
                        });
                    }
                    else if (!string.IsNullOrWhiteSpace(row.ClipPath))
                    {
                        audio.PlayOneShot(new OneShotRequest
                        {
                            ClipPath = row.ClipPath,
                            Space = row.Space,
                            BusId = row.BusId,
                            Gain = row.Gain <= 0f ? 1f : row.Gain,
                            Pitch = row.Pitch <= 0f ? 1f : row.Pitch,
                            Priority = row.Priority,
                            FadeInSeconds = row.FadeInSeconds,
                            PauseWithGameplay = row.PauseWithGameplay,
                            ApplyTimeScale = true,
                            PositionWorld = worldPos,
                            RefDistance = row.RefDistance > 0f ? row.RefDistance : 64f,
                            MaxDistance = row.MaxDistance > 0f ? row.MaxDistance : 480f,
                            Rolloff = row.Rolloff > 0f ? row.Rolloff : 1f,
                        });
                    }
                }
            }
        }
    }
}
