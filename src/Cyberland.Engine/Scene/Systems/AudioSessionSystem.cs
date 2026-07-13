using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Mirrors <see cref="RuntimeScenes.GlobalSessionClock"/> pause and time scale into <see cref="Audio.IAudioService"/>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin host glue.")]
public sealed class AudioSessionSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private bool? _lastPaused;
    private float _lastScale = float.NaN;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    /// <summary>Creates the session sync system.</summary>
    public AudioSessionSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = query;
        _ = deltaSeconds;
        var clock = _host.SessionClock;
        var paused = clock.Paused;
        if (_lastPaused != paused)
        {
            _host.Audio.SetGameplayAudioPaused(paused);
            _lastPaused = paused;
        }

        var scale = clock.TimeScale;
        if (float.IsNaN(_lastScale) || System.MathF.Abs(_lastScale - scale) > 1e-5f)
        {
            _host.Audio.SetTimeScale(scale);
            _lastScale = scale;
        }
    }
}
