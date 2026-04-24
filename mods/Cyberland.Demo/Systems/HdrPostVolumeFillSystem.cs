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
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag>();

    private readonly GameHostServices _host;
    private EntityId _volumeEntity;
    private bool _resolved;
    private World _world;

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

        var world = _world;
        var renderer = _host.Renderer!;
        var frameBuffer = renderer.ActiveCameraViewportSize;

        var player = archetype.RequireSingleEntityWith<PlayerTag>("player");
        ref readonly var playerTransform = ref world.Components<Transform>().Get(player);
        var t = frameBuffer.X > 0 ? playerTransform.WorldPosition.X / frameBuffer.X : 0f;
        t = Math.Clamp(t, 0f, 1f);
        var bloomGain = 2.35f - 1.85f * t;

        var cx = frameBuffer.X * 0.5f;
        var cy = frameBuffer.Y * 0.5f;
        ref var tf = ref world.Components<Transform>().Get(_volumeEntity);
        tf.LocalPosition = new Vector2D<float>(cx, cy);
        tf.LocalRotationRadians = 0f;
        tf.LocalScale = new Vector2D<float>(1f, 1f);

        ref var vol = ref world.Components<PostProcessVolumeSource>().Get(_volumeEntity);
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
