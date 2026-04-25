using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Maps ECS BrickBreaker gameplay components to sprites and HUD <see cref="BitmapText"/>.</summary>
public sealed class VisualSyncSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _stateEntity;
    private readonly EntityId _background;
    private readonly EntityId _paddle;
    private readonly EntityId _ball;
    private readonly EntityId _titleUi;
    private readonly EntityId _gameOverPanel;
    private readonly EntityId _gameOverBar;
    private readonly EntityId[] _lives;
    private readonly EntityId[,] _cells;
    private readonly HudTextIds _texts;
    private readonly TextStyle _titleStyle;
    private readonly TextStyle _hintStyle;
    private readonly TextStyle _hudStyle;
    private readonly TextStyle _scoreStyle;
    private readonly TextStyle _gameOverStyle;
    private readonly TextStyle _fpsStyle;

    private float _lastBrickW = -1f;
    private float _lastBrickH = -1f;
    private int _lastFbX = -1;
    private int _lastFbY = -1;
    private int _lastScore = int.MinValue;
    private string _scoreText = "0";
    private string _fpsText = "FPS —";
    private readonly FpsMovingAverage _fpsAverage = new(Constants.FpsAverageWindowSeconds);
    private World _world = null!;

    public VisualSyncSystem(GameHostServices host, EntityId stateEntity, EntityId background, EntityId paddle, EntityId ball, EntityId titleUi, EntityId gameOverPanel, EntityId gameOverBar, EntityId[] lives, EntityId[,] cells, HudTextIds texts)
    {
        _host = host;
        _stateEntity = stateEntity;
        _background = background;
        _paddle = paddle;
        _ball = ball;
        _titleUi = titleUi;
        _gameOverPanel = gameOverPanel;
        _gameOverBar = gameOverBar;
        _lives = lives;
        _cells = cells;
        _texts = texts;
        _titleStyle = new TextStyle(BuiltinFonts.UiSans, 22f, new Vector4D<float>(0.45f, 0.78f, 1f, 1f), Bold: true);
        _hintStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.55f, 0.62f, 0.72f, 0.9f));
        _hudStyle = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(0.8f, 0.9f, 1f, 1f));
        _scoreStyle = new TextStyle(BuiltinFonts.Mono, 20f, new Vector4D<float>(1f, 0.85f, 0.35f, 1f));
        _gameOverStyle = new TextStyle(BuiltinFonts.UiSans, 19f, new Vector4D<float>(1f, 0.5f, 0.35f, 1f), Italic: true);
        _fpsStyle = new TextStyle(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f));
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        var renderer = _host.Renderer;
        if (renderer is null)
        {
            EngineDiagnostics.Report(
                EngineErrorSeverity.Major,
                "Cyberland.Demo.BrickBreaker — VisualSync init failed",
                "Host.Renderer was null during VisualSyncSystem.OnStart; static visual setup cannot proceed.");
            throw new InvalidOperationException("VisualSyncSystem requires Host.Renderer during OnStart.");
        }

        InitializeStaticVisualState(_world, renderer.WhiteTextureId, renderer.DefaultNormalTextureId);
    }

    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        var frameSeconds = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fpsAverage.AddFrameDeltaSeconds(frameSeconds);
        if (_fpsAverage.TryGetAverageFps(out var avgFps))
        {
            _fpsText = $"FPS {MathF.Round(avgFps)}";
        }
        else
        {
            _fpsText = "FPS —";
        }

        ref readonly var s = ref _world.Get<GameState>(_stateEntity);
        // Match the fixed Camera2D viewport (see Constants); late-phase reads of ActiveCameraViewportSize can
        // still be inconsistent with early arena layout on the first frame if keyed off swapchain fallback.
        var fb = new Vector2D<int>(Constants.CanvasWidth, Constants.CanvasHeight);
        var w = _world;

        if (_lastFbX != fb.X || _lastFbY != fb.Y)
        {
            _lastFbX = fb.X;
            _lastFbY = fb.Y;
            UpdateResizeSensitivePositionsAndExtents(w, fb, s.Score);
        }
        else
        {
            UpdateGameOverBarExtentOnly(w, fb, s.Score);
        }

        UpdateBrickVisuals(w, s);

        ref readonly var paddleBody = ref w.Get<PaddleBody>(_paddle);
        ref var paddleSpr = ref w.Get<Sprite>(_paddle);
        paddleSpr.Visible = s.Phase == Phase.Playing;
        paddleSpr.HalfExtents = new Vector2D<float>(paddleBody.HalfWidth, paddleBody.HalfHeight);

        ref var ballSpr = ref w.Get<Sprite>(_ball);
        ballSpr.Visible = s.Phase == Phase.Playing;

        ref var titleSpr = ref w.Get<Sprite>(_titleUi);
        titleSpr.Visible = s.Phase == Phase.Title;
        ref var overPanelSpr = ref w.Get<Sprite>(_gameOverPanel);
        overPanelSpr.Visible = s.Phase is Phase.GameOver or Phase.Won;
        ref var overBarSpr = ref w.Get<Sprite>(_gameOverBar);
        overBarSpr.Visible = s.Phase is Phase.GameOver or Phase.Won;

        var playing = s.Phase == Phase.Playing;
        for (var i = 0; i < Constants.StartingLives; i++)
        {
            ref var spr = ref w.Get<Sprite>(_lives[i]);
            spr.Visible = playing && i < s.Lives;
        }

        SyncHudText(w, fb, in s);
    }

    private void InitializeStaticVisualState(World w, TextureId white, TextureId normal)
    {
        ref var bgSpr = ref w.Get<Sprite>(_background);
        bgSpr.Visible = true;
        bgSpr.Layer = (int)SpriteLayer.Background;
        bgSpr.SortKey = 0f;
        bgSpr.AlbedoTextureId = white;
        bgSpr.NormalTextureId = normal;
        bgSpr.ColorMultiply = new Vector4D<float>(0.04f, 0.04f, 0.08f, 1f);
        bgSpr.EmissiveIntensity = 0f;

        ref var paddleSpr = ref w.Get<Sprite>(_paddle);
        paddleSpr.Layer = (int)SpriteLayer.World;
        paddleSpr.SortKey = 5f;
        paddleSpr.AlbedoTextureId = white;
        paddleSpr.NormalTextureId = normal;
        paddleSpr.ColorMultiply = new Vector4D<float>(0.4f, 0.85f, 1f, 1f);
        paddleSpr.EmissiveTint = new Vector3D<float>(0.4f, 0.9f, 1f);
        paddleSpr.EmissiveIntensity = 0.2f;

        ref var ballSpr = ref w.Get<Sprite>(_ball);
        ballSpr.HalfExtents = new Vector2D<float>(Constants.BallR, Constants.BallR);
        ballSpr.Layer = (int)SpriteLayer.World;
        ballSpr.SortKey = 8f;
        ballSpr.AlbedoTextureId = white;
        ballSpr.NormalTextureId = normal;
        ballSpr.ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f);
        ballSpr.EmissiveTint = new Vector3D<float>(1f, 1f, 1f);
        ballSpr.EmissiveIntensity = 0.7f;

        ref var titleSpr = ref w.Get<Sprite>(_titleUi);
        titleSpr.Layer = (int)SpriteLayer.Ui;
        titleSpr.SortKey = 20f;
        titleSpr.AlbedoTextureId = white;
        titleSpr.NormalTextureId = normal;
        titleSpr.ColorMultiply = new Vector4D<float>(0.5f, 0.75f, 1f, 1f);
        titleSpr.EmissiveTint = new Vector3D<float>(0.5f, 0.8f, 1f);
        titleSpr.EmissiveIntensity = 0.5f;

        ref var panelSpr = ref w.Get<Sprite>(_gameOverPanel);
        panelSpr.Layer = (int)SpriteLayer.Ui;
        panelSpr.SortKey = 200f;
        panelSpr.AlbedoTextureId = white;
        panelSpr.NormalTextureId = normal;
        panelSpr.ColorMultiply = new Vector4D<float>(0.12f, 0.12f, 0.16f, 1f);
        panelSpr.Alpha = 0.95f;
        panelSpr.Transparent = true;
        panelSpr.EmissiveIntensity = 0f;

        ref var barSpr = ref w.Get<Sprite>(_gameOverBar);
        barSpr.Layer = (int)SpriteLayer.Ui;
        barSpr.SortKey = 201f;
        barSpr.AlbedoTextureId = white;
        barSpr.NormalTextureId = normal;
        barSpr.ColorMultiply = new Vector4D<float>(1f, 0.75f, 0.2f, 1f);
        barSpr.EmissiveTint = new Vector3D<float>(1f, 0.8f, 0.2f);
        barSpr.EmissiveIntensity = 0.35f;

        for (var i = 0; i < Constants.StartingLives; i++)
        {
            ref var life = ref w.Get<Sprite>(_lives[i]);
            life.HalfExtents = new Vector2D<float>(14f, 6f);
            life.Layer = (int)SpriteLayer.Ui;
            life.SortKey = 50f + i;
            life.AlbedoTextureId = white;
            life.NormalTextureId = normal;
            life.ColorMultiply = new Vector4D<float>(0.9f, 0.3f, 0.35f, 1f);
            life.EmissiveTint = new Vector3D<float>(1f, 0.35f, 0.4f);
            life.EmissiveIntensity = 0.2f;
        }

        for (var cx = 0; cx < Constants.Cols; cx++)
        for (var cy = 0; cy < Constants.Rows; cy++)
        {
            ref var spr = ref w.Get<Sprite>(_cells[cx, cy]);
            var hue = (cx + cy * 0.7f) * 0.08f;
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = cx + cy * 0.1f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = normal;
            spr.ColorMultiply = new Vector4D<float>(0.3f + hue, 0.5f, 1f - hue, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.4f, 0.6f, 1f);
            spr.EmissiveIntensity = 0.15f;
        }
    }

    private void UpdateBrickVisuals(World w, in GameState state)
    {
        if (_lastBrickW != state.BrickW || _lastBrickH != state.BrickH)
        {
            _lastBrickW = state.BrickW;
            _lastBrickH = state.BrickH;
            var half = new Vector2D<float>(state.BrickW * 0.46f, state.BrickH * 0.45f);
            for (var cx = 0; cx < Constants.Cols; cx++)
            for (var cy = 0; cy < Constants.Rows; cy++)
            {
                ref var spr = ref w.Get<Sprite>(_cells[cx, cy]);
                spr.HalfExtents = half;
            }
        }

        for (var cx = 0; cx < Constants.Cols; cx++)
        for (var cy = 0; cy < Constants.Rows; cy++)
        {
            var e = _cells[cx, cy];
            ref var spr = ref w.Get<Sprite>(e);
            spr.Visible = w.TryGet<BrickState>(e, out var bs) && bs.Active;
        }
    }

    private void UpdateResizeSensitivePositionsAndExtents(
        World w,
        Vector2D<int> fb,
        int score)
    {
        ref var bgTransform = ref w.Get<Transform>(_background);
        bgTransform.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
        bgTransform.WorldPosition = bgTransform.LocalPosition;
        ref var bgSpr = ref w.Get<Sprite>(_background);
        bgSpr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);

        ref var titleTransform = ref w.Get<Transform>(_titleUi);
        titleTransform.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y - 56f);
        titleTransform.WorldPosition = titleTransform.LocalPosition;
        ref var titleSpr = ref w.Get<Sprite>(_titleUi);
        titleSpr.HalfExtents = new Vector2D<float>(fb.X * 0.4f, 18f);

        ref var panelTransform = ref w.Get<Transform>(_gameOverPanel);
        panelTransform.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.45f);
        panelTransform.WorldPosition = panelTransform.LocalPosition;
        ref var panelSpr = ref w.Get<Sprite>(_gameOverPanel);
        panelSpr.HalfExtents = new Vector2D<float>(fb.X * 0.42f, 70f);

        ref var barTransform = ref w.Get<Transform>(_gameOverBar);
        barTransform.LocalPosition = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.45f + 36f);
        barTransform.WorldPosition = barTransform.LocalPosition;
        UpdateGameOverBarExtentOnly(w, fb, score);

        for (var i = 0; i < Constants.StartingLives; i++)
        {
            ref var lifeTransform = ref w.Get<Transform>(_lives[i]);
            lifeTransform.LocalPosition = new Vector2D<float>(30f + i * 28f, fb.Y - 28f);
            lifeTransform.WorldPosition = lifeTransform.LocalPosition;
        }
    }

    private void UpdateGameOverBarExtentOnly(World w, Vector2D<int> fb, int score)
    {
        ref var bar = ref w.Get<Sprite>(_gameOverBar);
        var wBar = fb.X * 0.4f * Math.Min(1f, score / 600f);
        bar.HalfExtents = new Vector2D<float>(Math.Max(8f, wBar * 0.5f), 10f);
    }

    private void SyncHudText(World w, Vector2D<int> fb, in GameState s)
    {
        if (s.Score != _lastScore)
        {
            _lastScore = s.Score;
            _scoreText = s.Score.ToString();
        }

        SetTextVisible(w, _texts.Title, false);
        SetTextVisible(w, _texts.HintTitle, false);
        SetTextVisible(w, _texts.GameOver, false);
        SetTextVisible(w, _texts.HintGameOver, false);
        SetTextVisible(w, _texts.PlayingScore, false);
        SetTextVisible(w, _texts.ScoreNum, false);
        SetTextVisible(w, _texts.Fps, false);

        if (s.Phase == Phase.Title)
        {
            SetTextRow(w, _texts.Title, _titleStyle, "demo.brick.title", true, 36f, fb.Y - 58f);
            SetTextRow(w, _texts.HintTitle, _hintStyle, "demo.brick.hint_title", true, 36f, 100f);
        }
        else if (s.Phase == Phase.Won)
        {
            SetTextRow(w, _texts.GameOver, _gameOverStyle, "demo.brick.you_win", true, fb.X * 0.5f - 100f, fb.Y * 0.45f - 28f);
            SetTextRow(w, _texts.HintGameOver, _hintStyle, "demo.brick.hint_win", true, 36f, 118f);
        }
        else if (s.Phase == Phase.GameOver)
        {
            SetTextRow(w, _texts.GameOver, _gameOverStyle, "demo.brick.game_over", true, fb.X * 0.5f - 100f, fb.Y * 0.45f - 28f);
            SetTextRow(w, _texts.HintGameOver, _hintStyle, "demo.brick.hint_gameover", true, 36f, 118f);
        }
        else if (s.Phase == Phase.Playing)
        {
            SetTextRow(w, _texts.PlayingScore, _hudStyle, "demo.brick.playing_score", true, 24f, fb.Y - 32f);
            SetTextRow(w, _texts.ScoreNum, _scoreStyle, _scoreText, false, 130f, fb.Y - 32f);
        }

        if (fb.X > 0 && fb.Y > 0)
            SetTextRow(w, _texts.Fps, _fpsStyle, _fpsText, false, fb.X - 120f, fb.Y - 26f);
    }

    private static void SetTextVisible(World w, EntityId id, bool visible)
    {
        ref var bt = ref w.Get<BitmapText>(id);
        bt.Visible = visible;
    }

    private static void SetTextRow(
        World w,
        EntityId id,
        TextStyle style,
        string content,
        bool isLocalizationKey,
        float x,
        float y)
    {
        ref var transform = ref w.Get<Transform>(id);
        transform.LocalPosition = new Vector2D<float>(x, y);
        transform.WorldPosition = transform.LocalPosition;
        ref var bt = ref w.Get<BitmapText>(id);
        bt.Visible = true;
        if (bt.Style != style)
            bt.Style = style;
        if (bt.IsLocalizationKey != isLocalizationKey)
            bt.IsLocalizationKey = isLocalizationKey;
        if (bt.Content != content)
            bt.Content = content;
    }
}
