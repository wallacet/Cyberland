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

    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly VisualIds _v;
    private readonly HudTextIds _t;
    private int _cachedPlayerPoints = int.MinValue;
    private int _cachedCpuPoints = int.MinValue;
    private string _cachedPlayerPointsText = "0";
    private string _cachedCpuPointsText = "0";
    private World _world;

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

        ConfigureSpritesOnStart(world, renderer.WhiteTextureId, renderer.DefaultNormalTextureId);
        ConfigureTextRowsOnStart(world);
    }

    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var world = _world;
        var r = _host.Renderer!;
        ref readonly var st = ref world.Components<State>().Get(_session);
        var fb = r.SwapchainPixelSize;
        SetBg(world, fb);
        SetTitle(world, fb, st);
        SetHint(world, fb, st);
        SetScores(world, fb, st);
        SetPlaying(world, st);
        SyncHudText(world, fb, in st);
    }

    private void SyncHudText(World world, Vector2D<int> fb, in State st)
    {
        HideAllText(world);
        if (st.Phase is Phase.Title or Phase.GameOver)
        {
            SetRow(world, _t.Title, "demo.pong.title", true, PongLayout.HudMarginX, fb.Y - PongLayout.TitleTextOffsetY);
            if (st.Phase == Phase.GameOver) SetRow(world, _t.GameOverLine, "demo.pong.game_over", true, PongLayout.HudMarginX, fb.Y - PongLayout.GameOverTextOffsetY);
            var hintKey = st.Phase == Phase.Title ? "demo.pong.hint_title" : "demo.pong.hint_gameover";
            var hy = st.Phase == Phase.GameOver ? PongLayout.HintSpriteYGameOver : PongLayout.HintSpriteYTitle;
            SetRow(world, _t.Hint, hintKey, true, PongLayout.HudMarginX, hy);
        }

        if (st.Phase != Phase.Playing) return;
        if (st.PlayerPoints != _cachedPlayerPoints) { _cachedPlayerPoints = st.PlayerPoints; _cachedPlayerPointsText = st.PlayerPoints.ToString(); }
        if (st.CpuPoints != _cachedCpuPoints) { _cachedCpuPoints = st.CpuPoints; _cachedCpuPointsText = st.CpuPoints.ToString(); }
        SetRow(world, _t.ScoreYou, "demo.pong.score_you", true, 32f, fb.Y - 40f);
        SetRow(world, _t.ScorePlayerNum, _cachedPlayerPointsText, false, 118f, fb.Y - 40f);
        SetRow(world, _t.ScoreCpuLabel, "demo.pong.score_cpu", true, fb.X - PongLayout.ScoreColumnCpuLabelOffset, fb.Y - 40f);
        SetRow(world, _t.ScoreCpuNum, _cachedCpuPointsText, false, fb.X - PongLayout.ScoreColumnCpuNumOffset, fb.Y - 40f);
    }

    private void HideAllText(World world)
    {
        HideText(world, _t.Title);
        HideText(world, _t.GameOverLine);
        HideText(world, _t.Hint);
        HideText(world, _t.ScoreYou);
        HideText(world, _t.ScorePlayerNum);
        HideText(world, _t.ScoreCpuLabel);
        HideText(world, _t.ScoreCpuNum);
    }

    private static void HideText(World world, EntityId entity)
    {
        ref var text = ref world.Components<BitmapText>().Get(entity);
        text.Visible = false;
    }

    private static void SetRow(World world, EntityId entity, string content, bool isLocalizationKey, float x, float y)
    {
        ref var transform = ref world.Components<Transform>().Get(entity);
        transform.LocalPosition = new Vector2D<float>(x, y);
        transform.WorldPosition = transform.LocalPosition;

        ref var text = ref world.Components<BitmapText>().Get(entity);
        text.Visible = true;
        text.Content = content;
        text.IsLocalizationKey = isLocalizationKey;
    }

    private void SetBg(World world, Vector2D<int> fb)
    {
        ref var transform = ref world.Components<Transform>().Get(_v.Background);
        transform.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
        transform.WorldPosition = transform.LocalPosition;

        ref var spr = ref world.Components<Sprite>().Get(_v.Background);
        spr.Visible = true;
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
    }

    private void SetTitle(World world, Vector2D<int> fb, in State st)
    {
        var titlePulse = 0.85f + 0.15f * MathF.Sin(st.Pulse);
        ref var transform = ref world.Components<Transform>().Get(_v.TitleBar);
        transform.LocalPosition = new Vector2D<float>(
            fb.X * 0.5f,
            st.Phase == Phase.Playing ? fb.Y - PongLayout.TitleBarYPlaying : fb.Y - PongLayout.TitleBarYMenu);
        transform.WorldPosition = transform.LocalPosition;

        ref var spr = ref world.Components<Sprite>().Get(_v.TitleBar);
        var widthScale = st.Phase == Phase.Playing ? PongLayout.TitleBarWidthPlaying : PongLayout.TitleBarWidthMenu;
        spr.Visible = true;
        spr.HalfExtents = new Vector2D<float>(fb.X * widthScale * titlePulse, st.Phase == Phase.Playing ? PongLayout.TitleBarHalfHPlaying : PongLayout.TitleBarHalfHMenu);
        spr.EmissiveIntensity = 1.6f;
    }

    private void SetHint(World world, Vector2D<int> fb, in State st)
    {
        ref var transform = ref world.Components<Transform>().Get(_v.HintBar);
        transform.LocalPosition = new Vector2D<float>(
            fb.X * 0.5f,
            st.Phase == Phase.GameOver ? PongLayout.HintSpriteYGameOver : PongLayout.HintSpriteYTitle);
        transform.WorldPosition = transform.LocalPosition;

        ref var spr = ref world.Components<Sprite>().Get(_v.HintBar);
        spr.Visible = st.Phase is Phase.Title or Phase.GameOver;
        spr.HalfExtents = new Vector2D<float>(fb.X * PongLayout.HintBarWidthFrac, PongLayout.HintBarHalfH);
    }

    private void SetScores(World world, Vector2D<int> fb, in State st)
    {
        var maxH = fb.Y * 0.25f;
        var ph = maxH * (st.PlayerPoints / (float)Constants.WinScore);
        var ch = maxH * (st.CpuPoints / (float)Constants.WinScore);
        var playing = st.Phase == Phase.Playing;
        ref var ps = ref world.Components<Sprite>().Get(_v.ScorePlayer);
        ref var playerTransform = ref world.Components<Transform>().Get(_v.ScorePlayer);
        playerTransform.LocalPosition = new Vector2D<float>(PongLayout.ScoreColumnPlayerX, PongLayout.ScoreBarBaseY + ph * 0.5f);
        playerTransform.WorldPosition = playerTransform.LocalPosition;
        ps.Visible = playing;
        ps.HalfExtents = new Vector2D<float>(PongLayout.ScoreBarHalfWidth, ph * 0.5f + PongLayout.ScoreBarMinHalfHeight);

        ref var cs = ref world.Components<Sprite>().Get(_v.ScoreCpu);
        ref var cpuTransform = ref world.Components<Transform>().Get(_v.ScoreCpu);
        cpuTransform.LocalPosition = new Vector2D<float>(fb.X - PongLayout.ScoreColumnPlayerX, PongLayout.ScoreBarBaseY + ch * 0.5f);
        cpuTransform.WorldPosition = cpuTransform.LocalPosition;
        cs.Visible = playing;
        cs.HalfExtents = new Vector2D<float>(PongLayout.ScoreBarHalfWidth, ch * 0.5f + PongLayout.ScoreBarMinHalfHeight);
    }

    private void SetPlaying(World world, in State st)
    {
        var playing = st.Phase == Phase.Playing;
        var acc = _host.FixedAccumulatorSeconds;
        var leftY = st.LeftPaddleY + st.LeftPaddleVelY * acc;
        var rightY = st.RightPaddleY + st.RightPaddleVelY * acc;
        var ballX = st.BallPos.X + st.BallVel.X * acc;
        var ballY = st.BallPos.Y + st.BallVel.Y * acc;
        ref var leftTransform = ref world.Components<Transform>().Get(_v.LeftPad);
        leftTransform.LocalPosition = new Vector2D<float>(st.ArenaMinX, leftY);
        leftTransform.WorldPosition = leftTransform.LocalPosition;
        ref var ls = ref world.Components<Sprite>().Get(_v.LeftPad);
        ls.Visible = playing;
        ls.HalfExtents = new Vector2D<float>(Constants.PaddleHalfW, Constants.PaddleHalfH);

        ref var rightTransform = ref world.Components<Transform>().Get(_v.RightPad);
        rightTransform.LocalPosition = new Vector2D<float>(st.ArenaMaxX, rightY);
        rightTransform.WorldPosition = rightTransform.LocalPosition;
        ref var rs = ref world.Components<Sprite>().Get(_v.RightPad);
        rs.Visible = playing;
        rs.HalfExtents = new Vector2D<float>(Constants.PaddleHalfW, Constants.PaddleHalfH);

        ref var ballTransform = ref world.Components<Transform>().Get(_v.Ball);
        ballTransform.LocalPosition = new Vector2D<float>(ballX, ballY);
        ballTransform.WorldPosition = ballTransform.LocalPosition;
        ref var bs = ref world.Components<Sprite>().Get(_v.Ball);
        bs.Visible = playing;
        bs.HalfExtents = new Vector2D<float>(Constants.BallR, Constants.BallR);
    }
}
