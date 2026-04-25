using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Keeps sprites and HUD text aligned with <see cref="State"/> each frame. Sequential late update (session + explicit entity ids).
/// Static sprite/text setup lives in the <c>VisualSyncSystem.Bootstrap.cs</c> partial.
/// </summary>
public sealed partial class VisualSyncSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private static readonly TextStyle TitleStyle = new(BuiltinFonts.UiSans, 26f, new Vector4D<float>(0.25f, 0.92f, 1f, 1f), Bold: true);
    private static readonly TextStyle HintStyle = new(BuiltinFonts.UiSans, 15f, new Vector4D<float>(0.52f, 0.58f, 0.68f, 0.92f));
    private static readonly TextStyle HudStyle = new(BuiltinFonts.UiSans, 17f, new Vector4D<float>(0.72f, 0.88f, 1f, 1f));
    private static readonly TextStyle NumberStyle = new(BuiltinFonts.Mono, 20f, new Vector4D<float>(0.92f, 0.96f, 1f, 1f));
    private static readonly TextStyle GameOverStyle = new(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 0.42f, 0.48f, 1f), Underline: true);
    private static readonly TextStyle FpsStyle = new(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f));

    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly VisualIds _v;
    private readonly HudTextIds _t;
    private int _cachedPlayerPoints = int.MinValue;
    private int _cachedCpuPoints = int.MinValue;
    private string _cachedPlayerPointsText = "0";
    private string _cachedCpuPointsText = "0";
    private readonly FpsMovingAverage _fpsAverage = new(FpsMovingAverage.DefaultWindowSeconds);
    private World _world = null!;

    public VisualSyncSystem(GameHostServices host, EntityId session, VisualIds visuals, HudTextIds texts)
    {
        _host = host;
        _session = session;
        _v = visuals;
        _t = texts;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        var renderer = _host.Renderer;
        if (renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Pong.VisualSyncSystem startup failed", "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("Cyberland.Demo.Pong VisualSyncSystem requires a renderer.");
        }

        ConfigureSpritesOnStart(renderer.WhiteTextureId, renderer.DefaultNormalTextureId);
        ConfigureTextRowsOnStart();
    }

    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        var frameSeconds = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fpsAverage.AddFrameDeltaSeconds(frameSeconds);
        var r = _host.Renderer!;
        ref readonly var st = ref _world.Get<State>(_session);
        // Layout on the same virtual rect as the active camera.
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        SetBg(fb);
        SetTitle(fb, st);
        SetHint(fb, st);
        SetScores(fb, st);
        SetPlaying(st);
        SyncHudText(fb, in st);
        UpdateFpsHud(fb);
    }

    private void SyncHudText(Vector2D<int> fb, in State st)
    {
        HideAllText();
        if (st.Phase is Phase.Title or Phase.GameOver)
        {
            SetRow(_t.Title, "demo.pong.title", true, PongLayout.HudMarginX, fb.Y - PongLayout.TitleTextOffsetY);
            if (st.Phase == Phase.GameOver) SetRow(_t.GameOverLine, "demo.pong.game_over", true, PongLayout.HudMarginX, fb.Y - PongLayout.GameOverTextOffsetY);
            var hintKey = st.Phase == Phase.Title ? "demo.pong.hint_title" : "demo.pong.hint_gameover";
            var hy = st.Phase == Phase.GameOver ? PongLayout.HintSpriteYGameOver : PongLayout.HintSpriteYTitle;
            SetRow(_t.Hint, hintKey, true, PongLayout.HudMarginX, hy);
        }

        if (st.Phase != Phase.Playing) return;
        if (st.PlayerPoints != _cachedPlayerPoints) { _cachedPlayerPoints = st.PlayerPoints; _cachedPlayerPointsText = st.PlayerPoints.ToString(); }
        if (st.CpuPoints != _cachedCpuPoints) { _cachedCpuPoints = st.CpuPoints; _cachedCpuPointsText = st.CpuPoints.ToString(); }
        SetRow(_t.ScoreYou, "demo.pong.score_you", true, 32f, fb.Y - 40f);
        SetRow(_t.ScorePlayerNum, _cachedPlayerPointsText, false, 118f, fb.Y - 40f);
        SetRow(_t.ScoreCpuLabel, "demo.pong.score_cpu", true, fb.X - PongLayout.ScoreColumnCpuLabelOffset, fb.Y - 40f);
        SetRow(_t.ScoreCpuNum, _cachedCpuPointsText, false, fb.X - PongLayout.ScoreColumnCpuNumOffset, fb.Y - 40f);
    }

    private void UpdateFpsHud(Vector2D<int> fb)
    {
        var label = _fpsAverage.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
        ref var transform = ref _world.Get<Transform>(_t.Fps);
        transform.LocalPosition = new Vector2D<float>(fb.X - 120f, fb.Y - 26f);
        ref var text = ref _world.Get<BitmapText>(_t.Fps);
        text.Visible = true;
        text.Content = label;
        text.IsLocalizationKey = false;
        text.Style = FpsStyle;
    }

    private void HideAllText()
    {
        HideText(_t.Title);
        HideText(_t.GameOverLine);
        HideText(_t.Hint);
        HideText(_t.ScoreYou);
        HideText(_t.ScorePlayerNum);
        HideText(_t.ScoreCpuLabel);
        HideText(_t.ScoreCpuNum);
    }

    private void HideText(EntityId entity)
    {
        ref var text = ref _world.Get<BitmapText>(entity);
        text.Visible = false;
    }

    private void SetRow(EntityId entity, string content, bool isLocalizationKey, float x, float y)
    {
        ref var transform = ref _world.Get<Transform>(entity);
        transform.LocalPosition = new Vector2D<float>(x, y);

        ref var text = ref _world.Get<BitmapText>(entity);
        text.Visible = true;
        text.Content = content;
        text.IsLocalizationKey = isLocalizationKey;
    }

    private void SetBg(Vector2D<int> fb)
    {
        ref var transform = ref _world.Get<Transform>(_v.Background);
        transform.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);

        ref var spr = ref _world.Get<Sprite>(_v.Background);
        spr.Visible = true;
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
    }

    private void SetTitle(Vector2D<int> fb, in State st)
    {
        var titlePulse = 0.85f + 0.15f * MathF.Sin(st.Pulse);
        ref var transform = ref _world.Get<Transform>(_v.TitleBar);
        transform.LocalPosition = new Vector2D<float>(
            fb.X * 0.5f,
            st.Phase == Phase.Playing ? fb.Y - PongLayout.TitleBarYPlaying : fb.Y - PongLayout.TitleBarYMenu);

        ref var spr = ref _world.Get<Sprite>(_v.TitleBar);
        var widthScale = st.Phase == Phase.Playing ? PongLayout.TitleBarWidthPlaying : PongLayout.TitleBarWidthMenu;
        spr.Visible = true;
        spr.HalfExtents = new Vector2D<float>(fb.X * widthScale * titlePulse, st.Phase == Phase.Playing ? PongLayout.TitleBarHalfHPlaying : PongLayout.TitleBarHalfHMenu);
        spr.EmissiveIntensity = 1.6f;
    }

    private void SetHint(Vector2D<int> fb, in State st)
    {
        ref var transform = ref _world.Get<Transform>(_v.HintBar);
        transform.LocalPosition = new Vector2D<float>(
            fb.X * 0.5f,
            st.Phase == Phase.GameOver ? PongLayout.HintSpriteYGameOver : PongLayout.HintSpriteYTitle);

        ref var spr = ref _world.Get<Sprite>(_v.HintBar);
        spr.Visible = st.Phase is Phase.Title or Phase.GameOver;
        spr.HalfExtents = new Vector2D<float>(fb.X * PongLayout.HintBarWidthFrac, PongLayout.HintBarHalfH);
    }

    private void SetScores(Vector2D<int> fb, in State st)
    {
        var maxH = fb.Y * 0.25f;
        var ph = maxH * (st.PlayerPoints / (float)Constants.WinScore);
        var ch = maxH * (st.CpuPoints / (float)Constants.WinScore);
        var playing = st.Phase == Phase.Playing;
        ref var ps = ref _world.Get<Sprite>(_v.ScorePlayer);
        ref var playerTransform = ref _world.Get<Transform>(_v.ScorePlayer);
        playerTransform.LocalPosition = new Vector2D<float>(PongLayout.ScoreColumnPlayerX, PongLayout.ScoreBarBaseY + ph * 0.5f);
        ps.Visible = playing;
        ps.HalfExtents = new Vector2D<float>(PongLayout.ScoreBarHalfWidth, ph * 0.5f + PongLayout.ScoreBarMinHalfHeight);

        ref var cs = ref _world.Get<Sprite>(_v.ScoreCpu);
        ref var cpuTransform = ref _world.Get<Transform>(_v.ScoreCpu);
        cpuTransform.LocalPosition = new Vector2D<float>(fb.X - PongLayout.ScoreColumnPlayerX, PongLayout.ScoreBarBaseY + ch * 0.5f);
        cs.Visible = playing;
        cs.HalfExtents = new Vector2D<float>(PongLayout.ScoreBarHalfWidth, ch * 0.5f + PongLayout.ScoreBarMinHalfHeight);
    }

    private void SetPlaying(in State st)
    {
        var playing = st.Phase == Phase.Playing;
        var acc = _host.FixedAccumulatorSeconds;
        var leftY = st.LeftPaddleY + st.LeftPaddleVelY * acc;
        var rightY = st.RightPaddleY + st.RightPaddleVelY * acc;
        var ballX = st.BallPos.X + st.BallVel.X * acc;
        var ballY = st.BallPos.Y + st.BallVel.Y * acc;
        ref var leftTransform = ref _world.Get<Transform>(_v.LeftPad);
        leftTransform.LocalPosition = new Vector2D<float>(st.ArenaMinX, leftY);
        ref var ls = ref _world.Get<Sprite>(_v.LeftPad);
        ls.Visible = playing;
        ls.HalfExtents = new Vector2D<float>(Constants.PaddleHalfW, Constants.PaddleHalfH);

        ref var rightTransform = ref _world.Get<Transform>(_v.RightPad);
        rightTransform.LocalPosition = new Vector2D<float>(st.ArenaMaxX, rightY);
        ref var rs = ref _world.Get<Sprite>(_v.RightPad);
        rs.Visible = playing;
        rs.HalfExtents = new Vector2D<float>(Constants.PaddleHalfW, Constants.PaddleHalfH);

        ref var ballTransform = ref _world.Get<Transform>(_v.Ball);
        ballTransform.LocalPosition = new Vector2D<float>(ballX, ballY);
        ref var bs = ref _world.Get<Sprite>(_v.Ball);
        bs.Visible = playing;
        bs.HalfExtents = new Vector2D<float>(Constants.BallR, Constants.BallR);
    }
}
