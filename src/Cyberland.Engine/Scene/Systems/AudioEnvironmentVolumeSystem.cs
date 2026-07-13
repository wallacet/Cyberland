using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Submits <see cref="AudioEnvironmentVolumeSource"/> rows to the audio mixer each frame.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin host submit glue.")]
public sealed class AudioEnvironmentVolumeSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<AudioEnvironmentVolumeSource, Transform>();

    /// <summary>Creates the system.</summary>
    public AudioEnvironmentVolumeSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var audio = _host.Audio;
        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref readonly var row = ref chunk.Column<AudioEnvironmentVolumeSource>()[i];
                if (!row.Active)
                    return;

                ref readonly var tf = ref chunk.Column<Transform>()[i];
                TransformMath.DecomposeToPRS(tf.WorldMatrix, out var worldPos, out var worldRad, out var worldScale);
                audio.SubmitAudioEnvironmentVolume(in row.Volume, worldPos, worldRad, worldScale);
            });
        }
    }
}
