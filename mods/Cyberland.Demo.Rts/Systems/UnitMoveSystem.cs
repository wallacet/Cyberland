using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Fixed-step: steer units toward formation targets, then circle separation so they never overlap.</summary>
public sealed class UnitMoveSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<UnitTag, Transform, UnitState>();

    private World _world = null!;
    private EntityId[] _unitIds = [];
    private Vector2D<float>[] _positions = [];

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = query;
        _world = world;
        _unitIds = new EntityId[Constants.UnitCount];
        _positions = new Vector2D<float>[Constants.UnitCount];
    }

    /// <inheritdoc />
    public void OnFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds)
    {
        foreach (var chunk in query)
        {
            var transforms = chunk.Column<Transform>();
            var states = chunk.Column<UnitState>();
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
                if (len <= Constants.StopEpsilon)
                {
                    state.HasMoveOrder = false;
                    continue;
                }

                var step = Constants.MoveSpeed * fixedDeltaSeconds;
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

        UnitCollision.ResolveSeparation(
            _positions.AsSpan(0, count),
            Constants.SeparationRadius,
            Constants.SeparationIterations);

        for (var i = 0; i < count; i++)
        {
            ref var tf = ref _world.Get<Transform>(_unitIds[i]);
            tf.WorldPosition = ClampToPlayfield(_positions[i]);
        }
    }

    private static Vector2D<float> ClampToPlayfield(Vector2D<float> p)
    {
        var s = Constants.PlaySize;
        return new Vector2D<float>(Math.Clamp(p.X, 0f, s), Math.Clamp(p.Y, 0f, s));
    }
}
