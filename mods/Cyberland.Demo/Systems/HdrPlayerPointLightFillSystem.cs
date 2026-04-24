using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>Updates the player-follow <see cref="PointLightSource"/> from <see cref="PlayerTag"/> transform.</summary>
public sealed class HdrPlayerPointLightFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag>();

    private readonly GameHostServices _host;
    private EntityId _playerPointEntity;
    private bool _resolved;
    private World _world;

    /// <summary>Creates the system.</summary>
    public HdrPlayerPointLightFillSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        _ = _host.Renderer
            ?? throw new InvalidOperationException("cyberland.demo/hdr-player-point requires Host.Renderer during OnStart.");
        _playerPointEntity = world.QueryChunks(SystemQuerySpec.All<HdrPlayerPointTag>())
            .RequireSingleEntityWith<HdrPlayerPointTag>("HDR player point light");
        _resolved = true;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = deltaSeconds;
        if (!_resolved)
            return;

        var world = _world;
        var player = archetype.RequireSingleEntityWith<PlayerTag>("player");
        ref readonly var playerTransform = ref world.Components<Transform>().Get(player);
        ref var lightTransform = ref world.Components<Transform>().Get(_playerPointEntity);
        ref var pl = ref world.Components<PointLightSource>().Get(_playerPointEntity);
        pl.Active = true;
        lightTransform.LocalPosition = playerTransform.WorldPosition;
        lightTransform.WorldPosition = playerTransform.WorldPosition;
        pl.Radius = 180f;
        pl.Color = new Vector3D<float>(0.35f, 0.95f, 0.55f);
        pl.Intensity = 1.2f;
        pl.FalloffExponent = 2.25f;
        pl.CastsShadow = false;
    }
}
