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
    public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        if (!_resolved)
            return;

        var r = _host.Renderer!;
        var fb = r.SwapchainPixelSize;
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;

        ref var amb = ref world.Components<AmbientLightSource>().Get(_ambient);
        amb.Active = true;
        amb.Light = new AmbientLight
        {
            Color = new Vector3D<float>(0.38f, 0.4f, 0.48f),
            Intensity = 0.11f
        };

        ref var dir = ref world.Components<DirectionalLightSource>().Get(_directional);
        dir.Active = true;
        dir.Light = new DirectionalLight
        {
            DirectionWorld = new Vector2D<float>(0.4f, -0.55f),
            Color = new Vector3D<float>(0.5f, 0.48f, 0.44f),
            Intensity = 0.14f,
            CastsShadow = false
        };

        ref var warm = ref world.Components<PointLightSource>().Get(_warmPoint);
        warm.Active = true;
        warm.Light = new PointLight
        {
            PositionWorld = new Vector2D<float>(w * 0.76f, h * 0.3f),
            Radius = w * 0.4f,
            Color = new Vector3D<float>(1f, 0.65f, 0.38f),
            Intensity = 0.48f,
            FalloffExponent = 2.1f,
            CastsShadow = false
        };

        var spotPos = new Vector2D<float>(w * 0.2f, h * 0.58f);
        var center = new Vector2D<float>(w * 0.5f, h * 0.5f);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(1f, 0f);

        ref var sp = ref world.Components<SpotLightSource>().Get(_spot);
        sp.Active = true;
        sp.Light = new SpotLight
        {
            PositionWorld = spotPos,
            DirectionWorld = dirVec,
            Radius = w * 0.46f,
            InnerConeRadians = MathF.PI / 3.5f,
            OuterConeRadians = MathF.PI / 2.15f,
            Color = new Vector3D<float>(0.42f, 0.62f, 1f),
            Intensity = 0.42f,
            CastsShadow = false
        };
    }
}
