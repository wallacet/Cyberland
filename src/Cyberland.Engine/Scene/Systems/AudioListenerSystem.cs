using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Publishes listener pose: active <see cref="AudioListenerOverride"/> or camera runtime state.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin host submit glue; merge/math covered by unit tests.")]
public sealed class AudioListenerSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<AudioListenerOverride, Transform>();

    /// <summary>Creates the listener system.</summary>
    public AudioListenerSystem(GameHostServices host) => _host = host;

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
        var listener = new ListenerState
        {
            PositionWorld = _host.CameraRuntimeState.PositionWorld,
            RotationRadians = _host.CameraRuntimeState.RotationRadians,
        };

        foreach (var chunk in query)
        {
            var overrides = chunk.Column<AudioListenerOverride>();
            var transforms = chunk.Column<Transform>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var o = ref overrides[i];
                if (!o.Active || (found && o.Priority <= bestPri))
                    continue;
                bestPri = o.Priority;
                TransformMath.DecomposeToPRS(transforms[i].WorldMatrix, out var pos, out var rad, out _);
                listener.PositionWorld = pos;
                listener.RotationRadians = rad;
                found = true;
            }
        }

        _host.Audio.SetListener(in listener);
    }
}
