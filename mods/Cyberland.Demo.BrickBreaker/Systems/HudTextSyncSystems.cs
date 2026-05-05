using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Shared typography for HUD BitmapText rows.</summary>
internal static class HudTextStyles
{
    internal static readonly TextStyle Title = new(BuiltinFonts.UiSans, 22f, new Vector4D<float>(0.45f, 0.78f, 1f, 1f), Bold: true);
    internal static readonly TextStyle Hint = new(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.55f, 0.62f, 0.72f, 0.9f));
    internal static readonly TextStyle Hud = new(BuiltinFonts.UiSans, 16f, new Vector4D<float>(0.8f, 0.9f, 1f, 1f));
    internal static readonly TextStyle Score = new(BuiltinFonts.Mono, 20f, new Vector4D<float>(1f, 0.85f, 0.35f, 1f));
    internal static readonly TextStyle GameOver = new(BuiltinFonts.UiSans, 19f, new Vector4D<float>(1f, 0.5f, 0.35f, 1f), Italic: true);
}

/// <summary>Late: localized title line on the title screen.</summary>
public sealed class HudTitleTextSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudTitleTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public HudTitleTextSystem(GameHostServices host) => _host = host;

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
        ref var bt = ref row.Get<BitmapText>();
        var show = game.Phase == Phase.Title;
        bt.Visible = show;
        if (!show) return;
        ApplyRow(ref t, ref bt, HudTextStyles.Title, "demo.brick.title", true, 36f, fb.Y - 58f);
    }

    private static void ApplyRow(
        ref Transform t,
        ref BitmapText bt,
        TextStyle style,
        string content,
        bool isKey,
        float x,
        float y)
    {
        t.LocalPosition = new Vector2D<float>(x, y);
        if (bt.Style != style) bt.Style = style;
        if (bt.IsLocalizationKey != isKey) bt.IsLocalizationKey = isKey;
        if (bt.Content != content) bt.Content = content;
    }
}

/// <summary>Late: hint line under the title.</summary>
public sealed class HudHintTitleTextSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudHintTitleTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public HudHintTitleTextSystem(GameHostServices host) => _host = host;

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
        ref var bt = ref row.Get<BitmapText>();
        var show = game.Phase == Phase.Title;
        bt.Visible = show;
        if (!show) return;
        t.LocalPosition = new Vector2D<float>(36f, 100f);
        if (bt.Style != HudTextStyles.Hint) bt.Style = HudTextStyles.Hint;
        bt.IsLocalizationKey = true;
        bt.Content = "demo.brick.hint_title";
    }
}

/// <summary>Late: win or game-over headline row.</summary>
public sealed class HudGameOverTextSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudGameOverTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public HudGameOverTextSystem(GameHostServices host) => _host = host;

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
        ref var bt = ref row.Get<BitmapText>();
        var end = game.Phase is Phase.GameOver or Phase.Won;
        bt.Visible = end;
        if (!end) return;
        var key = game.Phase == Phase.Won ? "demo.brick.you_win" : "demo.brick.game_over";
        t.LocalPosition = new Vector2D<float>(fb.X * 0.5f - 100f, fb.Y * 0.45f - 28f);
        if (bt.Style != HudTextStyles.GameOver) bt.Style = HudTextStyles.GameOver;
        bt.IsLocalizationKey = true;
        bt.Content = key;
    }
}

/// <summary>Late: hint under end-game headline.</summary>
public sealed class HudHintEndTextSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudHintEndTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public HudHintEndTextSystem(GameHostServices host) => _host = host;

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
        ref var bt = ref row.Get<BitmapText>();
        var end = game.Phase is Phase.GameOver or Phase.Won;
        bt.Visible = end;
        if (!end) return;
        var key = game.Phase == Phase.Won ? "demo.brick.hint_win" : "demo.brick.hint_gameover";
        t.LocalPosition = new Vector2D<float>(36f, 118f);
        if (bt.Style != HudTextStyles.Hint) bt.Style = HudTextStyles.Hint;
        bt.IsLocalizationKey = true;
        bt.Content = key;
    }
}

/// <summary>Late: “Score” label while playing.</summary>
public sealed class HudPlayingScoreTextSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudPlayingScoreTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public HudPlayingScoreTextSystem(GameHostServices host) => _host = host;

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
        ref var bt = ref row.Get<BitmapText>();
        var playing = game.Phase == Phase.Playing;
        bt.Visible = playing;
        if (!playing) return;
        t.LocalPosition = new Vector2D<float>(24f, fb.Y - 32f);
        if (bt.Style != HudTextStyles.Hud) bt.Style = HudTextStyles.Hud;
        bt.IsLocalizationKey = true;
        bt.Content = "demo.brick.playing_score";
    }
}

/// <summary>Late: numeric score while playing.</summary>
public sealed class HudScoreNumTextSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudScoreNumTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;
    private int _lastScore = int.MinValue;
    private string _scoreText = "0";

    public HudScoreNumTextSystem(GameHostServices host) => _host = host;

    public void OnSingletonStart(in SingletonEntity row)
    {
        _sessionEntity = Session.RequireStateEntity(row.World);
    }

    public void OnSingletonLateUpdate(in SingletonEntity row, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var game = ref row.World.Get<GameState>(_sessionEntity);
        var fb = ModLayoutViewport.VirtualSizeForPresentation(_host.Renderer);
        if (game.Score != _lastScore)
        {
            _lastScore = game.Score;
            _scoreText = game.Score.ToString();
        }

        ref var t = ref row.Get<Transform>();
        ref var bt = ref row.Get<BitmapText>();
        var playing = game.Phase == Phase.Playing;
        bt.Visible = playing;
        if (!playing) return;
        t.LocalPosition = new Vector2D<float>(130f, fb.Y - 32f);
        if (bt.Style != HudTextStyles.Score) bt.Style = HudTextStyles.Score;
        bt.IsLocalizationKey = false;
        bt.Content = _scoreText;
    }
}
