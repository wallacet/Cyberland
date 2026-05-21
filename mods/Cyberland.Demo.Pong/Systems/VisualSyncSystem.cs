using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Keeps sprites aligned with <see cref="State"/> and updates retained HUD text from <c>Content/Ui/pong_hud.json</c>.
/// </summary>
[RunBefore("cyberland.engine/ui-document-frame")]
public sealed class VisualSyncSystem : ISingletonSystem, ISingletonLateUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<State, Control>();

    private readonly GameHostServices _host;
    private readonly LocalizationManager _strings;
    private readonly HudDocumentRefs _hud;
    private VisualIds _v;
    private int _cachedPlayerPoints = int.MinValue;
    private int _cachedCpuPoints = int.MinValue;
    private string _cachedPlayerPointsText = "0";
    private string _cachedCpuPointsText = "0";
    private readonly FpsMovingAverage _fpsAverage = new(FpsMovingAverage.DefaultWindowSeconds);
    private World _world = null!;

    public VisualSyncSystem(GameHostServices host, LocalizationManager strings, HudDocumentRefs hud)
    {
        _host = host;
        _strings = strings;
        _hud = hud;
    }

    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _world = sessionRow.World;
        _v = SceneWire.ResolveVisuals(_world);
        var renderer = _host.Renderer;
        ConfigureSpritesOnStart(renderer.WhiteTextureId, renderer.DefaultNormalTextureId);
    }

    public void OnSingletonLateUpdate(in SingletonEntity sessionRow, float deltaSeconds)
    {
        _world = sessionRow.World;
        var frameSeconds = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fpsAverage.AddFrameDeltaSeconds(frameSeconds);
        var r = _host.Renderer;
        ref readonly var st = ref sessionRow.Get<State>();
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        SetBg(fb);
        SetTitle(fb, st);
        SetHint(fb, st);
        SetScores(fb, st);
        SetPlaying(st);
        SyncHudText(in st);
        _hud.Fps.Text = _fpsAverage.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
    }

    private void SyncHudText(in State st)
    {
        var showMenu = st.Phase is Phase.Title or Phase.GameOver;
        _hud.Title.Visible = showMenu;
        _hud.GameOver.Visible = st.Phase == Phase.GameOver;
        _hud.Hint.Visible = showMenu;
        if (showMenu)
        {
            _hud.Title.Text = _strings.Get("demo.pong.title");
            if (st.Phase == Phase.GameOver)
                _hud.GameOver.Text = _strings.Get("demo.pong.game_over");
            var hintKey = st.Phase == Phase.Title ? "demo.pong.hint_title" : "demo.pong.hint_gameover";
            _hud.Hint.Text = _strings.Get(hintKey);
        }

        var playing = st.Phase == Phase.Playing;
        _hud.ScoreYou.Visible = playing;
        _hud.ScorePlayer.Visible = playing;
        _hud.ScoreCpuLabel.Visible = playing;
        _hud.ScoreCpu.Visible = playing;
        if (!playing)
            return;

        if (st.PlayerPoints != _cachedPlayerPoints)
        {
            _cachedPlayerPoints = st.PlayerPoints;
            _cachedPlayerPointsText = st.PlayerPoints.ToString();
        }

        if (st.CpuPoints != _cachedCpuPoints)
        {
            _cachedCpuPoints = st.CpuPoints;
            _cachedCpuPointsText = st.CpuPoints.ToString();
        }

        _hud.ScoreYou.Text = _strings.Get("demo.pong.score_you");
        _hud.ScorePlayer.Text = _cachedPlayerPointsText;
        _hud.ScoreCpuLabel.Text = _strings.Get("demo.pong.score_cpu");
        _hud.ScoreCpu.Text = _cachedCpuPointsText;
    }

    private void ConfigureSpritesOnStart(TextureId whiteTextureId, TextureId normalTextureId)
    {
        ConfigureSprite(_v.Background, (int)SpriteLayer.Background, 0f, whiteTextureId, normalTextureId, new Vector4D<float>(0.04f, 0.05f, 0.08f, 1f));
        ConfigureSprite(_v.TitleBar, (int)SpriteLayer.Ui, 1f, whiteTextureId, normalTextureId, new Vector4D<float>(0.1f, 0.85f, 0.95f, 1f));
        ConfigureSprite(_v.HintBar, (int)SpriteLayer.Ui, 5f, whiteTextureId, normalTextureId, new Vector4D<float>(0.5f, 0.55f, 0.65f, 1f), transparent: true, alpha: 0.85f);
        ConfigureSprite(_v.ScorePlayer, (int)SpriteLayer.Ui, 4f, whiteTextureId, normalTextureId, new Vector4D<float>(0.2f, 0.9f, 1f, 1f), new Vector3D<float>(0.2f, 0.85f, 1f), 0.3f);
        ConfigureSprite(_v.ScoreCpu, (int)SpriteLayer.Ui, 4f, whiteTextureId, normalTextureId, new Vector4D<float>(1f, 0.35f, 0.4f, 1f), new Vector3D<float>(1f, 0.4f, 0.45f), 0.25f);
        ConfigureSprite(_v.LeftPad, (int)SpriteLayer.World, 2f, whiteTextureId, normalTextureId, new Vector4D<float>(0.3f, 0.85f, 1f, 1f), new Vector3D<float>(0.3f, 0.9f, 1f), 0.4f);
        ConfigureSprite(_v.RightPad, (int)SpriteLayer.World, 2f, whiteTextureId, normalTextureId, new Vector4D<float>(1f, 0.35f, 0.45f, 1f), new Vector3D<float>(1f, 0.4f, 0.5f), 0.25f);
        ConfigureSprite(_v.Ball, (int)SpriteLayer.World, 3f, whiteTextureId, normalTextureId, new Vector4D<float>(1f, 1f, 1f, 1f), new Vector3D<float>(1f, 1f, 1f), 0.9f);
    }

    private void ConfigureSprite(
        EntityId entity,
        int layer,
        float sortKey,
        TextureId albedoTextureId,
        TextureId normalTextureId,
        Vector4D<float> colorMultiply,
        Vector3D<float>? emissiveTint = null,
        float emissiveIntensity = 0f,
        bool transparent = false,
        float alpha = 1f)
    {
        ref var sprite = ref _world.Get<Sprite>(entity);
        sprite.Visible = false;
        sprite.Layer = layer;
        sprite.SortKey = sortKey;
        sprite.AlbedoTextureId = albedoTextureId;
        sprite.NormalTextureId = normalTextureId;
        sprite.ColorMultiply = colorMultiply;
        sprite.Transparent = transparent;
        sprite.Alpha = alpha;
        sprite.EmissiveTint = emissiveTint ?? default;
        sprite.EmissiveIntensity = emissiveIntensity;
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
            st.Phase == Phase.Playing ? fb.Y - Layout.TitleBarYPlaying : fb.Y - Layout.TitleBarYMenu);
        ref var spr = ref _world.Get<Sprite>(_v.TitleBar);
        var widthScale = st.Phase == Phase.Playing ? Layout.TitleBarWidthPlaying : Layout.TitleBarWidthMenu;
        spr.Visible = true;
        spr.HalfExtents = new Vector2D<float>(fb.X * widthScale * titlePulse, st.Phase == Phase.Playing ? Layout.TitleBarHalfHPlaying : Layout.TitleBarHalfHMenu);
        spr.EmissiveIntensity = 1.6f;
    }

    private void SetHint(Vector2D<int> fb, in State st)
    {
        ref var transform = ref _world.Get<Transform>(_v.HintBar);
        transform.LocalPosition = new Vector2D<float>(
            fb.X * 0.5f,
            st.Phase == Phase.GameOver ? Layout.HintSpriteYGameOver : Layout.HintSpriteYTitle);
        ref var spr = ref _world.Get<Sprite>(_v.HintBar);
        spr.Visible = st.Phase is Phase.Title or Phase.GameOver;
        spr.HalfExtents = new Vector2D<float>(fb.X * Layout.HintBarWidthFrac, Layout.HintBarHalfH);
    }

    private void SetScores(Vector2D<int> fb, in State st)
    {
        var maxH = fb.Y * 0.25f;
        var ph = maxH * (st.PlayerPoints / (float)Constants.WinScore);
        var ch = maxH * (st.CpuPoints / (float)Constants.WinScore);
        var playing = st.Phase == Phase.Playing;
        ref var ps = ref _world.Get<Sprite>(_v.ScorePlayer);
        ref var playerTransform = ref _world.Get<Transform>(_v.ScorePlayer);
        playerTransform.LocalPosition = new Vector2D<float>(Layout.ScoreColumnPlayerX, Layout.ScoreBarBaseY + ph * 0.5f);
        ps.Visible = playing;
        ps.HalfExtents = new Vector2D<float>(Layout.ScoreBarHalfWidth, ph * 0.5f + Layout.ScoreBarMinHalfHeight);
        ref var cs = ref _world.Get<Sprite>(_v.ScoreCpu);
        ref var cpuTransform = ref _world.Get<Transform>(_v.ScoreCpu);
        cpuTransform.LocalPosition = new Vector2D<float>(fb.X - Layout.ScoreColumnPlayerX, Layout.ScoreBarBaseY + ch * 0.5f);
        cs.Visible = playing;
        cs.HalfExtents = new Vector2D<float>(Layout.ScoreBarHalfWidth, ch * 0.5f + Layout.ScoreBarMinHalfHeight);
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
