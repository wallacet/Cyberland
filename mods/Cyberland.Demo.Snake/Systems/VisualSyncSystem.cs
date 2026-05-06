using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Late phase: positions segment sprites, food, and HUD from <see cref="Session"/>; uses the singleton <see cref="VisualBundle"/> row.
/// </summary>
public sealed class VisualSyncSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<VisualBundle>();

    private EntityId _sessionEntity;
    private readonly GameHostServices _host;
    private static readonly TextStyle TitleStyle = new(BuiltinFonts.UiSans, 24f, new Vector4D<float>(0.25f, 1f, 0.45f, 1f), Bold: true);
    private static readonly TextStyle HintStyle = new(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.55f, 0.65f, 0.6f, 0.9f));
    private static readonly TextStyle HudStyle = new(BuiltinFonts.UiSans, 16f, new Vector4D<float>(0.85f, 1f, 0.9f, 1f));
    private static readonly TextStyle ScoreStyle = new(BuiltinFonts.Mono, 18f, new Vector4D<float>(0.95f, 1f, 0.85f, 1f));
    private static readonly TextStyle GameOverStyle = new(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 0.45f, 0.35f, 1f), Italic: true, Underline: true);
    private static readonly TextStyle FpsStyle = new(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f));

    private readonly FpsMovingAverage _fpsAverage = new(FpsMovingAverage.DefaultWindowSeconds);
    private World _world = null!;

    /// <summary>Creates the visual / HUD sync pass.</summary>
    public VisualSyncSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity visualsRow)
    {
        _world = visualsRow.World;
        _sessionEntity = _world.RequireSingleEntityWith<Session>("Snake session");
        var world = _world;
        var renderer = _host.Renderer;
        var visuals = world.Get<VisualBundle>(visualsRow.Entity);
        var white = renderer.WhiteTextureId;
        var normal = renderer.DefaultNormalTextureId;

        for (var i = 0; i < visuals.Segments.Length; i++)
        {
            InitializeSprite(visuals.Segments[i], white, normal);
            ref var segmentSprite = ref world.Get<Sprite>(visuals.Segments[i]);
            segmentSprite.Layer = (int)SpriteLayer.World;
            segmentSprite.EmissiveTint = new Vector3D<float>(0.2f, 1f, 0.4f);
            segmentSprite.Transparent = false;
            segmentSprite.Alpha = 1f;
        }

        InitializeSprite(visuals.Food, white, normal);
        ref var foodSprite = ref world.Get<Sprite>(visuals.Food);
        foodSprite.Layer = (int)SpriteLayer.World;
        foodSprite.SortKey = 50f;
        foodSprite.ColorMultiply = new Vector4D<float>(1f, 0.2f, 0.25f, 1f);
        foodSprite.Alpha = 1f;
        foodSprite.EmissiveIntensity = 0.8f;
        foodSprite.EmissiveTint = new Vector3D<float>(1f, 0.3f, 0.35f);
        foodSprite.Transparent = false;

        InitializeSprite(visuals.TitleBar, white, normal);
        ref var titleSprite = ref world.Get<Sprite>(visuals.TitleBar);
        titleSprite.Layer = (int)SpriteLayer.Ui;
        titleSprite.SortKey = 100f;
        titleSprite.ColorMultiply = new Vector4D<float>(0.3f, 1f, 0.5f, 1f);
        titleSprite.Alpha = 1f;
        titleSprite.EmissiveIntensity = 0.6f;
        titleSprite.EmissiveTint = new Vector3D<float>(0.3f, 1f, 0.5f);
        titleSprite.Transparent = false;

        InitializeSprite(visuals.GoPanel, white, normal);
        ref var gameOverSprite = ref world.Get<Sprite>(visuals.GoPanel);
        gameOverSprite.Layer = (int)SpriteLayer.Ui;
        gameOverSprite.SortKey = 200f;
        gameOverSprite.ColorMultiply = new Vector4D<float>(0.15f, 0.15f, 0.18f, 1f);
        gameOverSprite.Alpha = 0.92f;
        gameOverSprite.EmissiveIntensity = 0f;
        gameOverSprite.Transparent = true;

        InitializeSprite(visuals.ScoreBar, white, normal);
        ref var scoreSprite = ref world.Get<Sprite>(visuals.ScoreBar);
        scoreSprite.Layer = (int)SpriteLayer.Ui;
        scoreSprite.SortKey = 201f;
        scoreSprite.ColorMultiply = new Vector4D<float>(1f, 0.85f, 0.2f, 1f);
        scoreSprite.Alpha = 1f;
        scoreSprite.EmissiveIntensity = 0.4f;
        scoreSprite.EmissiveTint = new Vector3D<float>(1f, 0.9f, 0.2f);
        scoreSprite.Transparent = false;
        InitializeText(visuals.TxtTitle);
        InitializeText(visuals.TxtHintTitle);
        InitializeText(visuals.TxtGameOver);
        InitializeText(visuals.TxtHintGo);
        InitializeText(visuals.TxtPlaying);
        InitializeText(visuals.TxtScore);
        InitializeText(visuals.TxtFps);
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity visualsRow, float deltaSeconds)
    {
        _world = visualsRow.World;
        var frameSeconds = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fpsAverage.AddFrameDeltaSeconds(frameSeconds);
        var renderer = _host.Renderer;
        ref var session = ref _world.Get<Session>(_sessionEntity);
        var visuals = _world.Get<VisualBundle>(visualsRow.Entity);
        var fb = ModLayoutViewport.VirtualSizeForPresentation(renderer);
        if (fb.X <= 0 || fb.Y <= 0) return;
        session.UpdateLayout(fb.X, fb.Y);
        UpdateSnakeAndFoodSprites(visuals, in session, in fb);
        UpdateOverlayPanels(visuals, in session, in fb);

        SetHudText(visuals, fb, session);
        UpdateFpsHud(visuals, fb);
    }

    private void UpdateSnakeAndFoodSprites(VisualBundle visuals, in Session session, in Vector2D<int> framebufferSize)
    {
        var cell = session.Cell;
        var segIdx = 0;
        if (session.Snake.Count > 0)
        {
            var headCell = session.Snake.First!.Value;
            foreach (var seg in session.Snake)
            {
                var entity = visuals.Segments[segIdx++];
                var center = session.CellCenterWorld(seg.x, seg.y, framebufferSize);
                ref var transform = ref _world.Get<Transform>(entity);
                transform.LocalPosition = center;
                ref var sprite = ref _world.Get<Sprite>(entity);
                var isHead = seg.x == headCell.x && seg.y == headCell.y;
                sprite.Visible = true;
                sprite.HalfExtents = new Vector2D<float>(cell * 0.45f, cell * 0.45f);
                sprite.SortKey = 10f + seg.x;
                sprite.ColorMultiply = isHead
                    ? new Vector4D<float>(0.2f, 1f, 0.35f, 1f)
                    : new Vector4D<float>(0.05f, 0.55f, 0.12f, 1f);
                sprite.EmissiveIntensity = isHead ? 0.5f : 0.1f;
            }
        }

        for (var i = segIdx; i < visuals.Segments.Length; i++)
            _world.Get<Sprite>(visuals.Segments[i]).Visible = false;

        var foodCenter = session.CellCenterWorld(session.Food.x, session.Food.y, framebufferSize);
        ref var foodTransform = ref _world.Get<Transform>(visuals.Food);
        foodTransform.LocalPosition = foodCenter;
        ref var foodSprite = ref _world.Get<Sprite>(visuals.Food);
        foodSprite.Visible = session.Phase == Phase.Playing;
        foodSprite.HalfExtents = new Vector2D<float>(cell * 0.35f, cell * 0.35f);
    }

    private void UpdateOverlayPanels(VisualBundle visuals, in Session session, in Vector2D<int> framebufferSize)
    {
        ref var titleSprite = ref _world.Get<Sprite>(visuals.TitleBar);
        if (session.Phase == Phase.Title)
        {
            var titleBarCenter = WorldViewportSpace.ViewportPixelToWorldCenter(
                new Vector2D<float>(framebufferSize.X * 0.5f, framebufferSize.Y - 48f),
                framebufferSize);
            ref var titleTransform = ref _world.Get<Transform>(visuals.TitleBar);
            titleTransform.LocalPosition = titleBarCenter;
            titleSprite.Visible = true;
            titleSprite.HalfExtents = new Vector2D<float>(framebufferSize.X * 0.42f, 20f);
        }
        else
        {
            titleSprite.Visible = false;
        }

        ref var gameOverSprite = ref _world.Get<Sprite>(visuals.GoPanel);
        ref var scoreSprite = ref _world.Get<Sprite>(visuals.ScoreBar);
        if (session.Phase is not (Phase.GameOver or Phase.Won))
        {
            gameOverSprite.Visible = false;
            scoreSprite.Visible = false;
            return;
        }

        var panelCenter = WorldViewportSpace.ViewportPixelToWorldCenter(
            new Vector2D<float>(framebufferSize.X * 0.5f, framebufferSize.Y * 0.5f),
            framebufferSize);
        ref var panelTransform = ref _world.Get<Transform>(visuals.GoPanel);
        panelTransform.LocalPosition = panelCenter;
        gameOverSprite.Visible = true;
        gameOverSprite.HalfExtents = new Vector2D<float>(framebufferSize.X * 0.45f, 80f);

        var scoreWidth = framebufferSize.X * 0.45f * Math.Min(1f, session.FoodsEaten / 30f);
        var scoreCenter = WorldViewportSpace.ViewportPixelToWorldCenter(
            new Vector2D<float>(framebufferSize.X * 0.5f - (framebufferSize.X * 0.45f - scoreWidth) * 0.5f, framebufferSize.Y * 0.5f + 28f),
            framebufferSize);
        ref var scoreTransform = ref _world.Get<Transform>(visuals.ScoreBar);
        scoreTransform.LocalPosition = scoreCenter;
        scoreSprite.Visible = true;
        scoreSprite.HalfExtents = new Vector2D<float>(Math.Max(0.5f, scoreWidth * 0.5f), 10f);
    }

    private void UpdateFpsHud(VisualBundle visuals, Vector2D<int> fb)
    {
        var label = _fpsAverage.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
        ref var transform = ref _world.Get<Transform>(visuals.TxtFps);
        transform.LocalPosition = new Vector2D<float>(fb.X - 120f, fb.Y - 26f);
        ref var bt = ref _world.Get<BitmapText>(visuals.TxtFps);
        bt.Visible = true;
        bt.Content = label;
        bt.IsLocalizationKey = false;
        bt.Style = FpsStyle;
    }

    private void InitializeSprite(EntityId entity, TextureId whiteTextureId, TextureId normalTextureId)
    {
        var world = _world;
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var sprite = ref world.GetOrAdd<Sprite>(entity);
        sprite = Sprite.DefaultWhiteUnlit(whiteTextureId, normalTextureId, new Vector2D<float>(1f, 1f));
        sprite.Visible = false;
    }

    private void InitializeText(EntityId entity)
    {
        var world = _world;
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var text = ref world.GetOrAdd<BitmapText>(entity);
        text.Visible = false;
        text.Content = " ";
        text.SortKey = 450f;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
        text.Style = HudStyle;
        text.IsLocalizationKey = false;
    }

    private void SetHudText(VisualBundle visuals, Vector2D<int> framebufferSize, Session session)
    {
        HideAllHudText(_world, visuals);
        if (session.Phase == Phase.Title)
        {
            SetHudRow(_world, visuals.TxtTitle, TitleStyle, "demo.snake.title", true, 36f, framebufferSize.Y - 50f);
            SetHudRow(_world, visuals.TxtHintTitle, HintStyle, "demo.snake.hint_title", true, 36f, 100f);
        }
        else if (session.Phase is Phase.GameOver or Phase.Won)
        {
            var titleKey = session.Phase == Phase.Won ? "demo.snake.you_win" : "demo.snake.game_over";
            SetHudRow(_world, visuals.TxtGameOver, GameOverStyle, titleKey, true, framebufferSize.X * 0.5f - 120f, framebufferSize.Y * 0.5f + 52f);
            var hint = session.Phase == Phase.Won ? "demo.snake.hint_win" : "demo.snake.hint_gameover";
            SetHudRow(_world, visuals.TxtHintGo, HintStyle, hint, true, 36f, 118f);
        }
        else if (session.Phase == Phase.Playing)
        {
            SetHudRow(_world, visuals.TxtPlaying, HudStyle, "demo.snake.playing", true, 24f, framebufferSize.Y - 36f);
            SetHudRow(_world, visuals.TxtScore, ScoreStyle, session.FoodsEaten.ToString(), false, 110f, framebufferSize.Y - 36f);
        }
    }

    private static void HideAllHudText(World world, VisualBundle v)
    {
        world.Get<BitmapText>(v.TxtTitle).Visible = false;
        world.Get<BitmapText>(v.TxtHintTitle).Visible = false;
        world.Get<BitmapText>(v.TxtGameOver).Visible = false;
        world.Get<BitmapText>(v.TxtHintGo).Visible = false;
        world.Get<BitmapText>(v.TxtPlaying).Visible = false;
        world.Get<BitmapText>(v.TxtScore).Visible = false;
    }

    private static void SetHudRow(World world, EntityId e, TextStyle style, string content, bool isKey, float screenX, float screenY)
    {
        ref var transform = ref world.Get<Transform>(e);
        transform.LocalPosition = new Vector2D<float>(screenX, screenY);
        ref var bt = ref world.Get<BitmapText>(e);
        bt.Visible = true;
        bt.Style = style;
        bt.Content = content;
        bt.IsLocalizationKey = isKey;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.SortKey = 450f;
    }
}
