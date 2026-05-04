using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Late: paddle sprite visibility and half extents from <see cref="GameState"/> and <see cref="PaddleBody"/>.</summary>
public sealed class PaddleSpriteSyncSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Paddle, PaddleBody, Sprite>();

    private EntityId _sessionEntity;

    public void OnSingletonStart(in SingletonEntity paddleRow)
    {
        _sessionEntity = Session.RequireStateEntity(paddleRow.World);
    }

    public void OnSingletonLateUpdate(in SingletonEntity paddleRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var game = ref paddleRow.World.Get<GameState>(_sessionEntity);
        ref var paddleBody = ref paddleRow.Get<PaddleBody>();
        ref var spr = ref paddleRow.Get<Sprite>();
        spr.Visible = game.Phase == Phase.Playing;
        spr.HalfExtents = new Vector2D<float>(paddleBody.HalfWidth, paddleBody.HalfHeight);
    }
}

/// <summary>Late: ball sprite visibility from session phase.</summary>
public sealed class BallSpriteSyncSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BallTag, Sprite>();

    private EntityId _sessionEntity;

    public void OnSingletonStart(in SingletonEntity ballRow)
    {
        _sessionEntity = Session.RequireStateEntity(ballRow.World);
    }

    public void OnSingletonLateUpdate(in SingletonEntity ballRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var game = ref ballRow.World.Get<GameState>(_sessionEntity);
        ballRow.Get<Sprite>().Visible = game.Phase == Phase.Playing;
    }
}
