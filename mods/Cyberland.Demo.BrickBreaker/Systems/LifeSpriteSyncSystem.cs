using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Late: life pips—visibility and anchor position in presentation space (one row per <see cref="LifePipSlot"/>).</summary>
public sealed class LifeSpriteSyncSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<LifePipSlot, Transform, Sprite>();

    private readonly GameHostServices _host;
    private World _world = null!;
    private EntityId _sessionEntity;

    public LifeSpriteSyncSystem(GameHostServices host) => _host = host;

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _sessionEntity = Session.RequireStateEntity(world);
        _ = archetype;
        _ = _host.Renderer ?? throw new InvalidOperationException("brick/life-sprites requires Host.Renderer.");
    }

    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var game = ref _world.Get<GameState>(_sessionEntity);
        var fb = ModLayoutViewport.VirtualSizeForPresentation(_host.Renderer!);
        var playing = game.Phase == Phase.Playing;
        foreach (var chunk in archetype)
        {
            var slots = chunk.Column<LifePipSlot>();
            var sprites = chunk.Column<Sprite>();
            var transforms = chunk.Column<Transform>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var slot = slots[i].Index;
                sprites[i].Visible = playing && game.Lives > slot;
                transforms[i].LocalPosition = new Vector2D<float>(30f + slot * 28f, fb.Y - 28f);
            }
        }
    }
}
