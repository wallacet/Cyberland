using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Fixed-step: move the unit toward <see cref="RtsSessionState.MoveTargetWorld"/> until within stopping epsilon.</summary>
public sealed class RtsUnitMoveSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<RtsUnitTag, Transform>();

    private EntityId _sessionEntity;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity unitRow)
    {
        _sessionEntity = unitRow.World.RequireSingleEntityWith<RtsSessionState>("rts session");
    }

    /// <inheritdoc />
    public void OnSingletonFixedUpdate(in SingletonEntity unitRow, float fixedDeltaSeconds)
    {
        var world = unitRow.World;
        ref var session = ref world.Get<RtsSessionState>(_sessionEntity);
        if (!session.HasMoveTarget)
            return;

        ref var ut = ref unitRow.Get<Transform>();
        var p = ut.WorldPosition;
        var target = session.MoveTargetWorld;
        var dx = target.X - p.X;
        var dy = target.Y - p.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len <= RtsConstants.StopEpsilon)
        {
            session.HasMoveTarget = false;
            return;
        }

        var step = RtsConstants.MoveSpeed * fixedDeltaSeconds;
        if (len <= step)
        {
            ut.WorldPosition = target;
            session.HasMoveTarget = false;
            return;
        }

        ut.WorldPosition = new Vector2D<float>(p.X + dx / len * step, p.Y + dy / len * step);
    }
}
