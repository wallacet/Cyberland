using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Late: title-screen overlay sprite position and visibility.</summary>
public sealed class TitleUiSpriteSyncSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<TitleUiTag, Transform, Sprite>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public TitleUiSpriteSyncSystem(GameHostServices host) => _host = host;

    public void OnSingletonStart(in SingletonEntity row)
    {
        _sessionEntity = Session.RequireStateEntity(row.World);
    }

    public void OnSingletonLateUpdate(in SingletonEntity row, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var game = ref row.World.Get<GameState>(_sessionEntity);
        var fb = ModLayoutViewport.VirtualSizeForPresentation(_host.Renderer);
        ref var t = ref row.Get<Transform>();
        ref var spr = ref row.Get<Sprite>();
        spr.Visible = game.Phase == Phase.Title;
        t.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y - 56f);
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.4f, 18f);
    }
}

/// <summary>Late: win or lose dim panel sprite.</summary>
public sealed class GameOverPanelSpriteSyncSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GameOverPanelTag, Transform, Sprite>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public GameOverPanelSpriteSyncSystem(GameHostServices host) => _host = host;

    public void OnSingletonStart(in SingletonEntity row)
    {
        _sessionEntity = Session.RequireStateEntity(row.World);
    }

    public void OnSingletonLateUpdate(in SingletonEntity row, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var game = ref row.World.Get<GameState>(_sessionEntity);
        var fb = ModLayoutViewport.VirtualSizeForPresentation(_host.Renderer);
        ref var t = ref row.Get<Transform>();
        ref var spr = ref row.Get<Sprite>();
        spr.Visible = game.Phase is Phase.GameOver or Phase.Won;
        t.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.45f);
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.42f, 70f);
    }
}

/// <summary>Late: accent bar under end-game copy (width scales slightly with score).</summary>
public sealed class GameOverBarSpriteSyncSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GameOverBarTag, Transform, Sprite>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public GameOverBarSpriteSyncSystem(GameHostServices host) => _host = host;

    public void OnSingletonStart(in SingletonEntity row)
    {
        _sessionEntity = Session.RequireStateEntity(row.World);
    }

    public void OnSingletonLateUpdate(in SingletonEntity row, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var game = ref row.World.Get<GameState>(_sessionEntity);
        var fb = ModLayoutViewport.VirtualSizeForPresentation(_host.Renderer);
        ref var t = ref row.Get<Transform>();
        ref var spr = ref row.Get<Sprite>();
        spr.Visible = game.Phase is Phase.GameOver or Phase.Won;
        t.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.45f + 36f);
        var wBar = fb.X * 0.4f * Math.Min(1f, game.Score / 600f);
        spr.HalfExtents = new Vector2D<float>(Math.Max(8f, wBar * 0.5f), 10f);
    }
}
