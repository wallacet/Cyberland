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
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform>();


    private World _world = null!;
    private readonly GameHostServices _host;
    private EntityId _playerPointEntity;
    private bool _resolved;
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

        Vector2D<float> playerPos = default;
        var found = false;
        foreach (var chunk in archetype)
        {
            var tCol = chunk.Column<Transform>();
            for (var i = 0; i < chunk.Count; i++)
            {
                playerPos = tCol[i].WorldPosition;
                found = true;
                break;
            }

            if (found) break;
        }

        if (!found)
            return;

        ref var lightTransform = ref _world.Get<Transform>(_playerPointEntity);
        ref var pl = ref _world.Get<PointLightSource>(_playerPointEntity);
        pl.Active = true;
        lightTransform.LocalPosition = playerPos;
        lightTransform.WorldPosition = playerPos;
        pl.Radius = 180f;
        pl.Color = new Vector3D<float>(0.35f, 0.95f, 0.55f);
        pl.Intensity = 1.2f;
        pl.FalloffExponent = 2.25f;
        pl.CastsShadow = false;
    }
}
