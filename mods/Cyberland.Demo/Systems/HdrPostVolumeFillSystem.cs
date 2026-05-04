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
/// Volume authoring is created in <see cref="SceneSetupSystem"/>; this system only mutates what must track resize + gameplay.
/// Bloom coefficients live on <see cref="HdrDemoBloom"/> next to other HDR tuning constants.
/// </remarks>
public sealed class HdrPostVolumeFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform>();

    private World _world = null!;
    private readonly GameHostServices _host;
    private EntityId _volumeEntity;

    /// <summary>Creates the system.</summary>
    public HdrPostVolumeFillSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        _ = _host.Renderer
            ?? throw new InvalidOperationException("cyberland.demo/hdr-post-volume requires Host.Renderer during OnStart.");

        // Tag singleton from SceneSetupSystem so we never depend on entity ids baked into code.
        _volumeEntity = world.QueryChunks(SystemQuerySpec.All<HdrBloomVolumeTag>())
            .RequireSingleEntityWith<HdrBloomVolumeTag>("HDR bloom volume");
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = deltaSeconds;

        var renderer = _host.Renderer!;
        var frameBuffer = renderer.ActiveCameraViewportSize;
        var width = frameBuffer.X;
        if (width <= 0)
            return;

        // Normalize player X against viewport width for a 0..1 slider (demo assumes one PlayerTag row).
        var tNorm = Math.Clamp(archetype.GetFirst<Transform>().WorldPosition.X / width, 0f, 1f);
        var bloomGain = HdrDemoBloom.GainAtPlayerLeft - HdrDemoBloom.GainSpanAcrossPlayfield * tNorm;

        var cx = width * 0.5f;
        var cy = frameBuffer.Y * 0.5f;

        // Volume transform is local space; centering at half extents keeps this quad aligned with the camera target area.
        ref var tf = ref _world.Get<Transform>(_volumeEntity);
        tf.LocalPosition = new Vector2D<float>(cx, cy);

        // PostProcessVolume is a struct component: copy, edit fields, assign back (same pattern as engine volume producers).
        ref var volRef = ref _world.Get<PostProcessVolumeSource>(_volumeEntity);
        var volume = volRef.Volume;
        volume.HalfExtentsLocal = new Vector2D<float>(cx, cy);
        volume.Overrides.BloomGain = bloomGain;
        volRef.Volume = volume;
    }
}