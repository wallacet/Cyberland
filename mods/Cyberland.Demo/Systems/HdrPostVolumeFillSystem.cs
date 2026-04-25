using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>Updates fullscreen <see cref="PostProcessVolumeSource"/> bloom from player horizontal position.</summary>
public sealed class HdrPostVolumeFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform>();


    private World _world = null!;
    private readonly GameHostServices _host;
    private EntityId _volumeEntity;
    private bool _resolved;
    /// <summary>Creates the system.</summary>
    public HdrPostVolumeFillSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        _ = _host.Renderer
            ?? throw new InvalidOperationException("cyberland.demo/hdr-post-volume requires Host.Renderer during OnStart.");
        _volumeEntity = world.QueryChunks(SystemQuerySpec.All<HdrBloomVolumeTag>())
            .RequireSingleEntityWith<HdrBloomVolumeTag>("HDR bloom volume");
        _resolved = true;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = deltaSeconds;
        if (!_resolved)
            return;

        var renderer = _host.Renderer!;
        var frameBuffer = renderer.ActiveCameraViewportSize;

        var tNorm = 0f;
        foreach (var chunk in archetype)
        {
            if (chunk.Count == 0)
                continue;
            tNorm = frameBuffer.X > 0 ? chunk.Column<Transform>()[0].WorldPosition.X / frameBuffer.X : 0f;
            tNorm = Math.Clamp(tNorm, 0f, 1f);
            break;
        }

        var bloomGain = 2.35f - 1.85f * tNorm;

        var cx = frameBuffer.X * 0.5f;
        var cy = frameBuffer.Y * 0.5f;
        ref var tf = ref _world.Get<Transform>(_volumeEntity);
        tf.LocalPosition = new Vector2D<float>(cx, cy);
        tf.LocalRotationRadians = 0f;
        tf.LocalScale = new Vector2D<float>(1f, 1f);

        ref var vol = ref _world.Get<PostProcessVolumeSource>(_volumeEntity);
        vol.Active = true;
        vol.Volume = new PostProcessVolume
        {
            HalfExtentsLocal = new Vector2D<float>(cx, cy),
            Priority = 1,
            Overrides = new PostProcessOverrides
            {
                HasBloomGain = true,
                BloomGain = bloomGain,
                HasExposure = false,
                Exposure = 1f,
                HasSaturation = false,
                Saturation = 1f
            }
        };
    }
}
