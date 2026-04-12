using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>Copies <see cref="PongState"/> into <see cref="Position"/> + <see cref="Sprite"/> for the engine sprite pass.</summary>
public sealed class PongVisualSyncSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly PongVisualIds _v;
    private readonly LocalizationManager _localization;
    private readonly FontLibrary _fonts;
    private readonly TextGlyphCache _textCache;

    private int _cachedPlayerPoints = int.MinValue;
    private int _cachedCpuPoints = int.MinValue;
    private string _cachedPlayerPointsText = "0";
    private string _cachedCpuPointsText = "0";

    public PongVisualSyncSystem(
        GameHostServices host,
        EntityId session,
        PongVisualIds visuals,
        LocalizationManager localization,
        FontLibrary fonts,
        TextGlyphCache textCache)
    {
        _host = host;
        _session = session;
        _v = visuals;
        _localization = localization;
        _fonts = fonts;
        _textCache = textCache;
    }

    public void OnLateUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        ref readonly var st = ref world.Components<PongState>().Get(_session);
        var fb = r.SwapchainPixelSize;
        var white = r.WhiteTextureId;
        var n = r.DefaultNormalTextureId;

        SetBg(world, fb, white, n);
        SetTitle(world, fb, st, white, n);
        SetHint(world, fb, st, white, n);
        SetScores(world, fb, st, white, n);
        SetPlaying(world, st, white, n);
        DrawPongText(r, fb, in st);
    }

    private void DrawPongText(IRenderer r, Vector2D<int> fb, in PongState st)
    {
        var titleSt = new TextStyle(BuiltinFonts.UiSans, 26f, new Vector4D<float>(0.25f, 0.92f, 1f, 1f), Bold: true);
        var hintSt = new TextStyle(BuiltinFonts.UiSans, 15f, new Vector4D<float>(0.52f, 0.58f, 0.68f, 0.92f));
        var hudSt = new TextStyle(BuiltinFonts.UiSans, 17f, new Vector4D<float>(0.72f, 0.88f, 1f, 1f));
        var numSt = new TextStyle(BuiltinFonts.Mono, 20f, new Vector4D<float>(0.92f, 0.96f, 1f, 1f));

        if (st.Phase is PongPhase.Title or PongPhase.GameOver)
        {
            TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, titleSt, "demo.pong.title",
                new Vector2D<float>(36f, fb.Y - 48f));
            if (st.Phase == PongPhase.GameOver)
            {
                var go = titleSt with
                {
                    SizePixels = 20f,
                    Color = new Vector4D<float>(1f, 0.42f, 0.48f, 1f),
                    Underline = true
                };
                TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, go, "demo.pong.game_over",
                    new Vector2D<float>(36f, fb.Y - 78f));
            }

            var hintKey = st.Phase == PongPhase.Title ? "demo.pong.hint_title" : "demo.pong.hint_gameover";
            var hy = st.Phase == PongPhase.GameOver ? 130f : 100f;
            TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, hintSt, hintKey,
                new Vector2D<float>(36f, hy));
        }

        if (st.Phase != PongPhase.Playing)
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

        TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, hudSt, "demo.pong.score_you",
            new Vector2D<float>(32f, fb.Y - 40f));
        TextRenderer.DrawLiteral(r, _fonts, _textCache, numSt, _cachedPlayerPointsText,
            new Vector2D<float>(118f, fb.Y - 40f));
        TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, hudSt, "demo.pong.score_cpu",
            new Vector2D<float>(fb.X - 210f, fb.Y - 40f));
        TextRenderer.DrawLiteral(r, _fonts, _textCache, numSt, _cachedCpuPointsText,
            new Vector2D<float>(fb.X - 88f, fb.Y - 40f));
    }

    private void SetBg(World world, Vector2D<int> fb, int white, int defN)
    {
        ref var pos = ref world.Components<Position>().Get(_v.Background);
        pos.X = fb.X * 0.5f;
        pos.Y = fb.Y * 0.5f;
        ref var spr = ref world.Components<Sprite>().Get(_v.Background);
        spr.Visible = true;
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
        spr.Layer = (int)SpriteLayer.Background;
        spr.SortKey = 0f;
        spr.AlbedoTextureId = white;
        spr.NormalTextureId = defN;
        spr.ColorMultiply = new Vector4D<float>(0.04f, 0.05f, 0.08f, 1f);
        spr.Alpha = 1f;
        spr.EmissiveIntensity = 0f;
    }

    private void SetTitle(World world, Vector2D<int> fb, in PongState st, int white, int defN)
    {
        // Same Sin(st.Pulse) animation in every phase so you can compare smoothness vs world-layer ball/paddles (Playing).
        var titlePulse = 0.85f + 0.15f * MathF.Sin(st.Pulse);
        ref var pos = ref world.Components<Position>().Get(_v.TitleBar);
        pos.X = fb.X * 0.5f;
        // During play, sit the strip slightly higher so it does not sit on the same row as the score HUD (fb.Y - 40).
        pos.Y = st.Phase == PongPhase.Playing ? fb.Y - 28f : fb.Y - 42f;
        ref var spr = ref world.Components<Sprite>().Get(_v.TitleBar);
        spr.Visible = true;
        var widthScale = st.Phase == PongPhase.Playing ? 0.38f : 0.45f;
        spr.HalfExtents = new Vector2D<float>(fb.X * widthScale * titlePulse, st.Phase == PongPhase.Playing ? 12f : 18f);
        spr.Layer = (int)SpriteLayer.Ui;
        spr.SortKey = 1f;
        spr.AlbedoTextureId = white;
        spr.NormalTextureId = defN;
        spr.ColorMultiply = new Vector4D<float>(0.1f, 0.85f, 0.95f, 1f);
        spr.Alpha = 1f;
        spr.EmissiveTint = new Vector3D<float>(0.2f, 0.9f, 1f);
        spr.EmissiveIntensity = 1.6f;
    }

    private void SetHint(World world, Vector2D<int> fb, in PongState st, int white, int defN)
    {
        var y = st.Phase == PongPhase.GameOver ? 130f : 100f;
        ref var pos = ref world.Components<Position>().Get(_v.HintBar);
        pos.X = fb.X * 0.5f;
        pos.Y = y;
        ref var spr = ref world.Components<Sprite>().Get(_v.HintBar);
        spr.Visible = st.Phase is PongPhase.Title or PongPhase.GameOver;
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.4f, 14f);
        spr.Layer = (int)SpriteLayer.Ui;
        spr.SortKey = 5f;
        spr.AlbedoTextureId = white;
        spr.NormalTextureId = defN;
        spr.ColorMultiply = new Vector4D<float>(0.5f, 0.55f, 0.65f, 1f);
        spr.Alpha = 0.85f;
        spr.Transparent = true;
        spr.EmissiveIntensity = 0f;
    }

    private void SetScores(World world, Vector2D<int> fb, in PongState st, int white, int defN)
    {
        var maxH = fb.Y * 0.25f;
        var ph = maxH * (st.PlayerPoints / (float)PongConstants.WinScore);
        var ch = maxH * (st.CpuPoints / (float)PongConstants.WinScore);
        var playing = st.Phase == PongPhase.Playing;

        {
            ref var pos = ref world.Components<Position>().Get(_v.ScorePlayer);
            pos.X = 18f;
            pos.Y = 80f + ph * 0.5f;
            ref var spr = ref world.Components<Sprite>().Get(_v.ScorePlayer);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(10f, ph * 0.5f + 2f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 4f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(0.2f, 0.9f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.2f, 0.85f, 1f);
            spr.EmissiveIntensity = 0.3f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_v.ScoreCpu);
            pos.X = fb.X - 18f;
            pos.Y = 80f + ch * 0.5f;
            ref var spr = ref world.Components<Sprite>().Get(_v.ScoreCpu);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(10f, ch * 0.5f + 2f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 4f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(1f, 0.35f, 0.4f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 0.4f, 0.45f);
            spr.EmissiveIntensity = 0.25f;
        }
    }

    private void SetPlaying(World world, in PongState st, int white, int defN)
    {
        var playing = st.Phase == PongPhase.Playing;
        var acc = _host.FixedAccumulatorSeconds;
        var leftY = st.LeftPaddleY + st.LeftPaddleVelY * acc;
        var rightY = st.RightPaddleY + st.RightPaddleVelY * acc;
        var ballX = st.BallPos.X + st.BallVel.X * acc;
        var ballY = st.BallPos.Y + st.BallVel.Y * acc;
        {
            ref var pos = ref world.Components<Position>().Get(_v.LeftPad);
            pos.X = st.ArenaMinX;
            pos.Y = leftY;
            ref var spr = ref world.Components<Sprite>().Get(_v.LeftPad);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(PongConstants.PaddleHalfW, PongConstants.PaddleHalfH);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 2f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(0.3f, 0.85f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.3f, 0.9f, 1f);
            spr.EmissiveIntensity = 0.4f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_v.RightPad);
            pos.X = st.ArenaMaxX;
            pos.Y = rightY;
            ref var spr = ref world.Components<Sprite>().Get(_v.RightPad);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(PongConstants.PaddleHalfW, PongConstants.PaddleHalfH);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 2f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(1f, 0.35f, 0.45f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 0.4f, 0.5f);
            spr.EmissiveIntensity = 0.25f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_v.Ball);
            pos.X = ballX;
            pos.Y = ballY;
            ref var spr = ref world.Components<Sprite>().Get(_v.Ball);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(PongConstants.BallR, PongConstants.BallR);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 3f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 1f, 1f);
            spr.EmissiveIntensity = 0.9f;
        }
    }
}
