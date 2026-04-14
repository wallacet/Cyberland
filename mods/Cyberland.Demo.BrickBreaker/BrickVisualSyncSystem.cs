using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Maps <see cref="BrickSession"/> to ECS sprites and HUD <see cref="BitmapText"/> for the engine draw passes.</summary>
public sealed class BrickVisualSyncSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private readonly BrickSession _session;
    private readonly EntityId _background;
    private readonly EntityId _paddle;
    private readonly EntityId _ball;
    private readonly EntityId _titleUi;
    private readonly EntityId _gameOverPanel;
    private readonly EntityId _gameOverBar;
    private readonly EntityId[] _lives;
    private readonly EntityId[,] _cells;
    private readonly BrickHudTextIds _texts;

    /// <summary>Exponential moving average of instantaneous FPS for a stable HUD readout.</summary>
    private float _fpsSmoothed;

    public BrickVisualSyncSystem(
        GameHostServices host,
        BrickSession session,
        EntityId background,
        EntityId paddle,
        EntityId ball,
        EntityId titleUi,
        EntityId gameOverPanel,
        EntityId gameOverBar,
        EntityId[] lives,
        EntityId[,] cells,
        BrickHudTextIds texts)
    {
        _host = host;
        _session = session;
        _background = background;
        _paddle = paddle;
        _ball = ball;
        _titleUi = titleUi;
        _gameOverPanel = gameOverPanel;
        _gameOverBar = gameOverBar;
        _lives = lives;
        _cells = cells;
        _texts = texts;
    }

    public void OnLateUpdate(World world, float deltaSeconds)
    {
        var r = _host.Renderer;
        if (r is null)
            return;

        // Prefer time between draw/present callbacks; raw per-tick dt can differ from visible frame time.
        // "FPS" than the user sees when the window runs many updates per rendered frame.
        var frameSeconds = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        if (frameSeconds > 1e-6f)
        {
            var instant = 1f / frameSeconds;
            const float blend = 0.15f;
            _fpsSmoothed = _fpsSmoothed <= 0f
                ? instant
                : _fpsSmoothed + (instant - _fpsSmoothed) * blend;
        }

        var s = _session;
        var fb = r.SwapchainPixelSize;
        var white = r.WhiteTextureId;
        var n = r.DefaultNormalTextureId;

        {
            ref var pos = ref world.Components<Position>().Get(_background);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y * 0.5f;
            ref var spr = ref world.Components<Sprite>().Get(_background);
            spr.Visible = true;
            spr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
            spr.Layer = (int)SpriteLayer.Background;
            spr.SortKey = 0f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.04f, 0.04f, 0.08f, 1f);
            spr.EmissiveIntensity = 0f;
        }

        for (var cx = 0; cx < BrickConstants.Cols; cx++)
        for (var cy = 0; cy < BrickConstants.Rows; cy++)
        {
            var e = _cells[cx, cy];
            var on = s.Bricks[cx, cy];
            ref var spr = ref world.Components<Sprite>().Get(e);
            spr.Visible = on;
            if (!on)
                continue;

            var bx = s.BrickOriginX + (cx + 0.5f) * s.BrickW;
            var by = s.BrickTopY - (cy + 0.5f) * s.BrickH;
            var hue = (cx + cy * 0.7f) * 0.08f;
            ref var pos = ref world.Components<Position>().Get(e);
            pos.X = bx;
            pos.Y = by;
            spr.HalfExtents = new Vector2D<float>(s.BrickW * 0.46f, s.BrickH * 0.45f);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = cx + cy * 0.1f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.3f + hue, 0.5f, 1f - hue, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.4f, 0.6f, 1f);
            spr.EmissiveIntensity = 0.15f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_paddle);
            pos.X = s.PaddleCenterX;
            pos.Y = s.PaddleY;
            ref var spr = ref world.Components<Sprite>().Get(_paddle);
            spr.Visible = s.Phase == BrickPhase.Playing;
            spr.HalfExtents = new Vector2D<float>(s.PaddleHalfW, s.PaddleHalfH);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 5f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.4f, 0.85f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.4f, 0.9f, 1f);
            spr.EmissiveIntensity = 0.2f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_ball);
            pos.X = s.BallPos.X;
            pos.Y = s.BallPos.Y;
            ref var spr = ref world.Components<Sprite>().Get(_ball);
            spr.Visible = s.Phase == BrickPhase.Playing;
            spr.HalfExtents = new Vector2D<float>(BrickConstants.BallR, BrickConstants.BallR);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 8f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 1f, 1f);
            spr.EmissiveIntensity = 0.7f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_titleUi);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y - 56f;
            ref var spr = ref world.Components<Sprite>().Get(_titleUi);
            spr.Visible = s.Phase == BrickPhase.Title;
            spr.HalfExtents = new Vector2D<float>(fb.X * 0.4f, 18f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 20f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.5f, 0.75f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.5f, 0.8f, 1f);
            spr.EmissiveIntensity = 0.5f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_gameOverPanel);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y * 0.45f;
            ref var spr = ref world.Components<Sprite>().Get(_gameOverPanel);
            spr.Visible = s.Phase == BrickPhase.GameOver;
            spr.HalfExtents = new Vector2D<float>(fb.X * 0.42f, 70f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 200f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.12f, 0.12f, 0.16f, 1f);
            spr.Alpha = 0.95f;
            spr.Transparent = true;
            spr.EmissiveIntensity = 0f;
        }

        {
            var wBar = fb.X * 0.4f * Math.Min(1f, s.Score / 600f);
            ref var pos = ref world.Components<Position>().Get(_gameOverBar);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y * 0.45f + 36f;
            ref var spr = ref world.Components<Sprite>().Get(_gameOverBar);
            spr.Visible = s.Phase == BrickPhase.GameOver;
            spr.HalfExtents = new Vector2D<float>(Math.Max(8f, wBar * 0.5f), 10f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 201f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(1f, 0.75f, 0.2f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 0.8f, 0.2f);
            spr.EmissiveIntensity = 0.35f;
        }

        var lifeW = 14f;
        for (var i = 0; i < BrickConstants.StartingLives; i++)
        {
            ref var pos = ref world.Components<Position>().Get(_lives[i]);
            pos.X = 30f + i * (lifeW * 2f);
            pos.Y = fb.Y - 28f;
            ref var spr = ref world.Components<Sprite>().Get(_lives[i]);
            spr.Visible = s.Phase == BrickPhase.Playing && i < s.Lives;
            spr.HalfExtents = new Vector2D<float>(lifeW, 6f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 50f + i;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.9f, 0.3f, 0.35f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 0.35f, 0.4f);
            spr.EmissiveIntensity = 0.2f;
        }

        SyncBrickHudText(world, fb, in s);
    }

    private void SyncBrickHudText(World world, Vector2D<int> fb, in BrickSession s)
    {
        var titleSt = new TextStyle(BuiltinFonts.UiSans, 22f, new Vector4D<float>(0.45f, 0.78f, 1f, 1f), Bold: true);
        var hintSt = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.55f, 0.62f, 0.72f, 0.9f));
        var hudSt = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(0.8f, 0.9f, 1f, 1f));
        var numSt = new TextStyle(BuiltinFonts.Mono, 20f, new Vector4D<float>(1f, 0.85f, 0.35f, 1f));
        var goSt = new TextStyle(BuiltinFonts.UiSans, 19f, new Vector4D<float>(1f, 0.5f, 0.35f, 1f), Italic: true);

        HideAllBrickText(world);

        if (s.Phase == BrickPhase.Title)
        {
            SetBrickRow(world, _texts.Title, titleSt, "demo.brick.title", true, 36f, fb.Y - 58f);
            SetBrickRow(world, _texts.HintTitle, hintSt, "demo.brick.hint_title", true, 36f, 100f);
        }
        else if (s.Phase == BrickPhase.GameOver)
        {
            SetBrickRow(world, _texts.GameOver, goSt, "demo.brick.game_over", true, fb.X * 0.5f - 100f, fb.Y * 0.45f - 28f);
            SetBrickRow(world, _texts.HintGameOver, hintSt, "demo.brick.hint_gameover", true, 36f, 118f);
        }
        else if (s.Phase == BrickPhase.Playing)
        {
            SetBrickRow(world, _texts.PlayingScore, hudSt, "demo.brick.playing_score", true, 24f, fb.Y - 32f);
            SetBrickRow(world, _texts.ScoreNum, numSt, s.Score.ToString(), false, 130f, fb.Y - 32f);
        }

        if (fb.X > 0 && fb.Y > 0)
        {
            var fpsSt = new TextStyle(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f));
            var line = _fpsSmoothed > 0f ? $"FPS {_fpsSmoothed:0}" : "FPS —";
            SetBrickRow(world, _texts.Fps, fpsSt, line, false, fb.X - 120f, fb.Y - 26f);
        }
    }

    private void HideAllBrickText(World world)
    {
        foreach (var e in new[]
                 {
                     _texts.Title, _texts.HintTitle, _texts.GameOver, _texts.HintGameOver, _texts.PlayingScore,
                     _texts.ScoreNum, _texts.Fps
                 })
        {
            ref var bt = ref world.Components<BitmapText>().Get(e);
            bt.Visible = false;
        }
    }

    private static void SetBrickRow(World world, EntityId e, TextStyle style, string content, bool isKey, float sx, float sy)
    {
        ref var pos = ref world.Components<Position>().Get(e);
        pos.X = sx;
        pos.Y = sy;
        ref var bt = ref world.Components<BitmapText>().Get(e);
        bt.Visible = true;
        bt.Style = style;
        bt.Content = content;
        bt.IsLocalizationKey = isKey;
        bt.BaselineWorldSpace = false;
        bt.SortKey = 450f;
    }
}
