using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Writes stationary HDR rig light sources from framebuffer size (entities created in <see cref="SceneSetupSystem"/>).
/// </summary>
public sealed class HdrStationaryLightsFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;


    private World _world = null!;
    private readonly GameHostServices _host;
    private EntityId _ambient;
    private EntityId _directional;
    private EntityId _spot;
    private EntityId _warmPoint;
    private bool _resolved;
    /// <summary>Creates the system.</summary>
    public HdrStationaryLightsFillSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        _ = _host.Renderer
            ?? throw new InvalidOperationException("cyberland.demo/hdr-stationary-lights requires Host.Renderer during OnStart.");

        _ambient = world.QueryChunks(SystemQuerySpec.All<AmbientLightSource>())
            .RequireSingleEntityWith<AmbientLightSource>("HDR ambient");
        _directional = world.QueryChunks(SystemQuerySpec.All<DirectionalLightSource>())
            .RequireSingleEntityWith<DirectionalLightSource>("HDR directional");
        _spot = world.QueryChunks(SystemQuerySpec.All<SpotLightSource>())
            .RequireSingleEntityWith<SpotLightSource>("HDR spot");
        _warmPoint = world.QueryChunks(SystemQuerySpec.All<HdrWarmPointTag>())
            .RequireSingleEntityWith<HdrWarmPointTag>("HDR warm point");
        _resolved = true;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        if (!_resolved)
            return;

        var r = _host.Renderer!;
        var fb = r.ActiveCameraViewportSize;
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;

        ref var amb = ref _world.Get<AmbientLightSource>(_ambient);
        amb.Active = true;
        amb.Color = new Vector3D<float>(0.38f, 0.4f, 0.48f);
        amb.Intensity = 0.11f;

        ref var dirTransform = ref _world.Get<Transform>(_directional);
        dirTransform.LocalRotationRadians = MathF.Atan2(-0.55f, 0.4f);
        dirTransform.WorldRotationRadians = dirTransform.LocalRotationRadians;
        ref var dir = ref _world.Get<DirectionalLightSource>(_directional);
        dir.Active = true;
        dir.Color = new Vector3D<float>(0.5f, 0.48f, 0.44f);
        dir.Intensity = 0.14f;
        dir.CastsShadow = false;

        ref var warmTransform = ref _world.Get<Transform>(_warmPoint);
        warmTransform.LocalPosition = new Vector2D<float>(w * 0.76f, h * 0.3f);
        warmTransform.WorldPosition = warmTransform.LocalPosition;
        ref var warm = ref _world.Get<PointLightSource>(_warmPoint);
        warm.Active = true;
        warm.Radius = w * 0.4f;
        warm.Color = new Vector3D<float>(1f, 0.65f, 0.38f);
        warm.Intensity = 0.48f;
        warm.FalloffExponent = 2.1f;
        warm.CastsShadow = false;

        var spotPos = new Vector2D<float>(w * 0.2f, h * 0.58f);
        var center = new Vector2D<float>(w * 0.5f, h * 0.5f);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(1f, 0f);

        ref var spotTransform = ref _world.Get<Transform>(_spot);
        spotTransform.LocalPosition = spotPos;
        spotTransform.WorldPosition = spotPos;
        spotTransform.LocalRotationRadians = MathF.Atan2(dirVec.Y, dirVec.X);
        spotTransform.WorldRotationRadians = spotTransform.LocalRotationRadians;
        ref var sp = ref _world.Get<SpotLightSource>(_spot);
        sp.Active = true;
        sp.Radius = w * 0.46f;
        sp.InnerConeRadians = MathF.PI / 3.5f;
        sp.OuterConeRadians = MathF.PI / 2.15f;
        sp.Color = new Vector3D<float>(0.42f, 0.62f, 1f);
        sp.Intensity = 0.42f;
        sp.CastsShadow = false;
    }
}
