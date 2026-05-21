using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Fixed-step: steer units toward formation targets, then circle separation so they never overlap.</summary>
public sealed class RtsUnitMoveSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<RtsUnitTag, Transform, RtsUnitState>();

    private World _world = null!;
    private EntityId[] _unitIds = [];
    private Vector2D<float>[] _positions = [];

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = query;
        _world = world;
        _unitIds = new EntityId[RtsConstants.UnitCount];
        _positions = new Vector2D<float>[RtsConstants.UnitCount];
    }

    /// <inheritdoc />
    public void OnFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds)
    {
        foreach (var chunk in query)
        {
            var transforms = chunk.Column<Transform>();
            var states = chunk.Column<RtsUnitState>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var state = ref states[i];
                if (!state.HasMoveOrder)
                    continue;

                ref var tf = ref transforms[i];
                var p = tf.WorldPosition;
                var target = state.MoveTargetWorld;
                var dx = target.X - p.X;
                var dy = target.Y - p.Y;
                var len = MathF.Sqrt(dx * dx + dy * dy);
                if (len <= RtsConstants.StopEpsilon)
                {
                    state.HasMoveOrder = false;
                    continue;
                }

                var step = RtsConstants.MoveSpeed * fixedDeltaSeconds;
                if (len <= step)
                {
                    tf.WorldPosition = target;
                    state.HasMoveOrder = false;
                }
                else
                    tf.WorldPosition = new Vector2D<float>(p.X + dx / len * step, p.Y + dy / len * step);
            }
        }

        var count = 0;
        foreach (var chunk in query)
        {
            var transforms = chunk.Column<Transform>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                _unitIds[count] = entities[i];
                _positions[count] = transforms[i].WorldPosition;
                count++;
            }
        }

        if (count < 2)
            return;

        RtsUnitCollision.ResolveSeparation(
            _positions.AsSpan(0, count),
            RtsConstants.SeparationRadius,
            RtsConstants.SeparationIterations);

        for (var i = 0; i < count; i++)
        {
            ref var tf = ref _world.Get<Transform>(_unitIds[i]);
            tf.WorldPosition = ClampToPlayfield(_positions[i]);
        }
    }

    private static Vector2D<float> ClampToPlayfield(Vector2D<float> p)
    {
        var s = RtsConstants.PlaySize;
        return new Vector2D<float>(Math.Clamp(p.X, 0f, s), Math.Clamp(p.Y, 0f, s));
    }
}
