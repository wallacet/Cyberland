using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Late update: recenters the fullscreen <see cref="PostProcessVolumeSource"/> on the active camera viewport and drives bloom
/// gain from how far right the player has moved—cheap HDR “feel” without scripting materials per sprite.
/// </summary>
/// <remarks>
/// Volume authoring is created in <see cref="SceneSetup"/>; this system only mutates what must track resize + gameplay.
/// Registered as <see cref="ISingletonSystem"/> for the bloom volume row; the player entity is resolved once in
/// <see cref="OnSingletonStart"/> for cross-entity reads (see **cyberland-mod-patterns-hdr**).
/// Bloom coefficients live on <see cref="HdrDemoBloom"/> next to other HDR tuning constants.
/// </remarks>
public sealed class HdrPostVolumeFillSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HdrBloomVolumeTag, PostProcessVolumeSource, Transform>();

    private readonly GameHostServices _host;
    private EntityId _player;

    /// <summary>Creates the system.</summary>
    public HdrPostVolumeFillSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity volume)
    {
        _ = volume.Get<PostProcessVolumeSource>();
        _ = _host.Renderer
            ?? throw new InvalidOperationException("cyberland.demo/hdr-post-volume requires Host.Renderer when the singleton entry starts.");

        _player = volume.World.QueryChunks(SystemQuerySpec.All<PlayerTag>())
            .RequireSingleEntity("HDR demo player");
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity volume, float deltaSeconds)
    {
        _ = deltaSeconds;

        var renderer = _host.Renderer!;
        var frameBuffer = renderer.ActiveCameraViewportSize;
        var width = frameBuffer.X;
        if (width <= 0)
            return;

        // Normalize player X against viewport width for a 0..1 slider (demo assumes one PlayerTag row).
        var playerX = volume.World.Get<Transform>(_player).WorldPosition.X;
        var tNorm = Math.Clamp(playerX / width, 0f, 1f);
        var bloomGain = HdrDemoBloom.GainAtPlayerLeft - HdrDemoBloom.GainSpanAcrossPlayfield * tNorm;

        var cx = width * 0.5f;
        var cy = frameBuffer.Y * 0.5f;

        // Volume transform is local space; centering at half extents keeps this quad aligned with the camera target area.
        ref var tf = ref volume.Get<Transform>();
        tf.LocalPosition = new Vector2D<float>(cx, cy);

        // PostProcessVolume is a struct component: copy, edit fields, assign back (same pattern as engine volume producers).
        ref var volRef = ref volume.Get<PostProcessVolumeSource>();
        var v = volRef.Volume;
        v.HalfExtentsLocal = new Vector2D<float>(cx, cy);
        v.Overrides.BloomGain = bloomGain;
        volRef.Volume = v;
    }
}
