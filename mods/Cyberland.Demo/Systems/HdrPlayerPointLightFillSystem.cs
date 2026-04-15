using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>Updates the player-follow <see cref="PointLightSource"/> from <see cref="PlayerTag"/> position.</summary>
public sealed class HdrPlayerPointLightFillSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag>();

    private readonly GameHostServices _host;
    private EntityId _playerPointEntity;
    private bool _resolved;

    /// <summary>Creates the system.</summary>
    public HdrPlayerPointLightFillSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = archetype;
        _ = _host.Renderer
            ?? throw new InvalidOperationException("cyberland.demo/hdr-player-point requires Host.Renderer during OnStart.");
        _playerPointEntity = world.QueryChunks(SystemQuerySpec.All<HdrPlayerPointTag>())
            .RequireSingleEntityWith<HdrPlayerPointTag>("HDR player point light");
        _resolved = true;
    }

    /// <inheritdoc />
    public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = deltaSeconds;
        if (!_resolved)
            return;

        var player = archetype.RequireSingleEntityWith<PlayerTag>("player");
        ref readonly var position = ref world.Components<Position>().Get(player);
        ref var pl = ref world.Components<PointLightSource>().Get(_playerPointEntity);
        pl.Active = true;
        pl.Light = new PointLight
        {
            PositionWorld = new Vector2D<float>(position.X, position.Y),
            Radius = 180f,
            Color = new Vector3D<float>(0.35f, 0.95f, 0.55f),
            Intensity = 1.2f,
            FalloffExponent = 2.25f,
            CastsShadow = false
        };
    }
}
