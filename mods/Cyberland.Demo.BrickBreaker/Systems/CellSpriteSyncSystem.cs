using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Late parallel: block sprite half extents and visibility from <see cref="GameState"/> layout and <see cref="ArenaCellState"/>.
/// </summary>
/// <remarks>
/// Session <see cref="GameState"/> is read once per callback; values are copied into locals before <see cref="Parallel.For"/>
/// so workers do not capture the session row by ref.
/// </remarks>
public sealed class CellSpriteSyncSystem : IParallelSystem, IParallelLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ArenaCellState, Cell, Sprite>();

    private World _world = null!;
    private EntityId _sessionEntity;
    private float _lastBrickW = -1f;
    private float _lastBrickH = -1f;

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _sessionEntity = Session.RequireStateEntity(world);
        _ = archetype;
    }

    public void OnParallelLateUpdate(ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        ref readonly var game = ref _world.Get<GameState>(_sessionEntity);
        var brickW = game.BrickW;
        var brickH = game.BrickH;
        var half = new Vector2D<float>(brickW * 0.46f, brickH * 0.45f);
        var layoutDirty = _lastBrickW != brickW || _lastBrickH != brickH;
        if (layoutDirty)
        {
            _lastBrickW = brickW;
            _lastBrickH = brickH;
        }

        foreach (var chunk in archetype)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref var spr = ref chunk.Column<Sprite>()[i];
                ref readonly var bs = ref chunk.Column<ArenaCellState>()[i];
                if (layoutDirty)
                    spr.HalfExtents = half;
                spr.Visible = bs.Active;
            });
        }
    }
}
