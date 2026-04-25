using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Positions segment sprites, food, and HUD from <see cref="Session"/>. Sequential late update; uses explicit entity ids, not chunk iteration.
/// </summary>
public sealed class VisualSyncSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _sessionEntity;
    private readonly EntityId _visualsEntity;
    private static readonly TextStyle TitleStyle = new(BuiltinFonts.UiSans, 24f, new Vector4D<float>(0.25f, 1f, 0.45f, 1f), Bold: true);
    private static readonly TextStyle HintStyle = new(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.55f, 0.65f, 0.6f, 0.9f));
    private static readonly TextStyle HudStyle = new(BuiltinFonts.UiSans, 16f, new Vector4D<float>(0.85f, 1f, 0.9f, 1f));
    private static readonly TextStyle ScoreStyle = new(BuiltinFonts.Mono, 18f, new Vector4D<float>(0.95f, 1f, 0.85f, 1f));
    private static readonly TextStyle GameOverStyle = new(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 0.45f, 0.35f, 1f), Italic: true, Underline: true);

    private World _world = null!;

    public VisualSyncSystem(GameHostServices host, EntityId sessionEntity, EntityId visualsEntity)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _visualsEntity = visualsEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        var renderer = _host.Renderer;
        if (renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake.VisualSyncSystem", "Renderer was null during OnStart.");
            throw new InvalidOperationException("Renderer is required by VisualSyncSystem.");
        }

        var visuals = _world.Get<VisualBundle>(_visualsEntity);
        var white = renderer.WhiteTextureId;
        var normal = renderer.DefaultNormalTextureId;

        for (var i = 0; i < visuals.Segments.Length; i++)
        {
            InitializeSprite(visuals.Segments[i], white, normal);
            ref var segmentSprite = ref _world.Get<Sprite>(visuals.Segments[i]);
            segmentSprite.Layer = (int)SpriteLayer.World;
            segmentSprite.EmissiveTint = new Vector3D<float>(0.2f, 1f, 0.4f);
            segmentSprite.Transparent = false;
            segmentSprite.Alpha = 1f;
        }

        InitializeSprite(visuals.Food, white, normal);
        ref var foodSprite = ref _world.Get<Sprite>(visuals.Food);
        foodSprite.Layer = (int)SpriteLayer.World;
        foodSprite.SortKey = 50f;
        foodSprite.ColorMultiply = new Vector4D<float>(1f, 0.2f, 0.25f, 1f);
        foodSprite.Alpha = 1f;
        foodSprite.EmissiveIntensity = 0.8f;
        foodSprite.EmissiveTint = new Vector3D<float>(1f, 0.3f, 0.35f);
        foodSprite.Transparent = false;

        InitializeSprite(visuals.TitleBar, white, normal);
        ref var titleSprite = ref _world.Get<Sprite>(visuals.TitleBar);
        titleSprite.Layer = (int)SpriteLayer.Ui;
        titleSprite.SortKey = 100f;
        titleSprite.ColorMultiply = new Vector4D<float>(0.3f, 1f, 0.5f, 1f);
        titleSprite.Alpha = 1f;
        titleSprite.EmissiveIntensity = 0.6f;
        titleSprite.EmissiveTint = new Vector3D<float>(0.3f, 1f, 0.5f);
        titleSprite.Transparent = false;

        InitializeSprite(visuals.GoPanel, white, normal);
        ref var gameOverSprite = ref _world.Get<Sprite>(visuals.GoPanel);
        gameOverSprite.Layer = (int)SpriteLayer.Ui;
        gameOverSprite.SortKey = 200f;
        gameOverSprite.ColorMultiply = new Vector4D<float>(0.15f, 0.15f, 0.18f, 1f);
        gameOverSprite.Alpha = 0.92f;
        gameOverSprite.EmissiveIntensity = 0f;
        gameOverSprite.Transparent = true;

        InitializeSprite(visuals.ScoreBar, white, normal);
        ref var scoreSprite = ref _world.Get<Sprite>(visuals.ScoreBar);
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
    }

    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var renderer = _host.Renderer;
        if (renderer is null) return;
        ref var session = ref _world.Get<Session>(_sessionEntity);
        var visuals = _world.Get<VisualBundle>(_visualsEntity);
        var fb = ModLayoutViewport.VirtualSizeForPresentation(renderer);
        if (fb.X <= 0 || fb.Y <= 0) return;
        session.UpdateLayout(fb.X, fb.Y);
        var cell = session.Cell;
        var segIdx = 0;
        if (session.Snake.Count > 0)
        {
            var headCell = session.Snake.First!.Value;
            foreach (var seg in session.Snake)
            {
                var e = visuals.Segments[segIdx++];
                var center = session.CellCenterWorld(seg.x, seg.y, fb);
                ref var transform = ref _world.Get<Transform>(e);
                transform.LocalPosition = center;
                transform.WorldPosition = center;
                ref var spr = ref _world.Get<Sprite>(e);
                var head = seg.x == headCell.x && seg.y == headCell.y;
                spr.Visible = true;
                spr.HalfExtents = new Vector2D<float>(cell * 0.45f, cell * 0.45f);
                spr.SortKey = 10f + seg.x;
                spr.ColorMultiply = head ? new Vector4D<float>(0.2f, 1f, 0.35f, 1f) : new Vector4D<float>(0.05f, 0.55f, 0.12f, 1f);
                spr.EmissiveIntensity = head ? 0.5f : 0.1f;
            }
        }
        for (var i = segIdx; i < visuals.Segments.Length; i++)
            _world.Get<Sprite>(visuals.Segments[i]).Visible = false;

        var foodCenter = session.CellCenterWorld(session.Food.x, session.Food.y, fb);
        ref var foodTransform = ref _world.Get<Transform>(visuals.Food);
        foodTransform.LocalPosition = foodCenter;
        foodTransform.WorldPosition = foodCenter;
        ref var foodSp = ref _world.Get<Sprite>(visuals.Food);
        foodSp.Visible = session.Phase == Phase.Playing;
        foodSp.HalfExtents = new Vector2D<float>(cell * 0.35f, cell * 0.35f);

        ref var titleSp = ref _world.Get<Sprite>(visuals.TitleBar);
        if (session.Phase == Phase.Title)
        {
            var titleBar = WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(fb.X * 0.5f, fb.Y - 48f), fb);
            ref var titleTransform = ref _world.Get<Transform>(visuals.TitleBar);
            titleTransform.LocalPosition = titleBar;
            titleTransform.WorldPosition = titleBar;
            titleSp.Visible = true;
            titleSp.HalfExtents = new Vector2D<float>(fb.X * 0.42f, 20f);
        }
        else titleSp.Visible = false;

        ref var gameOverSp = ref _world.Get<Sprite>(visuals.GoPanel);
        ref var scoreSp = ref _world.Get<Sprite>(visuals.ScoreBar);
        if (session.Phase is Phase.GameOver or Phase.Won)
        {
            var goPanel = WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f), fb);
            ref var gameOverTransform = ref _world.Get<Transform>(visuals.GoPanel);
            gameOverTransform.LocalPosition = goPanel;
            gameOverTransform.WorldPosition = goPanel;
            gameOverSp.Visible = true;
            gameOverSp.HalfExtents = new Vector2D<float>(fb.X * 0.45f, 80f);
            var scoreW = fb.X * 0.45f * Math.Min(1f, session.FoodsEaten / 30f);
            var scoreBar = WorldViewportSpace.ViewportPixelToWorldCenter(
                new Vector2D<float>(fb.X * 0.5f - (fb.X * 0.45f - scoreW) * 0.5f, fb.Y * 0.5f + 28f), fb);
            ref var scoreTransform = ref _world.Get<Transform>(visuals.ScoreBar);
            scoreTransform.LocalPosition = scoreBar;
            scoreTransform.WorldPosition = scoreBar;
            scoreSp.Visible = true;
            scoreSp.HalfExtents = new Vector2D<float>(Math.Max(0.5f, scoreW * 0.5f), 10f);
        }
        else
        {
            gameOverSp.Visible = false;
            scoreSp.Visible = false;
        }

        SetHudText(visuals, fb, session);
    }

    private void InitializeSprite(EntityId entity, TextureId whiteTextureId, TextureId normalTextureId)
    {
        _world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var sprite = ref _world.GetOrAdd<Sprite>(entity);
        sprite = Sprite.DefaultWhiteUnlit(whiteTextureId, normalTextureId, new Vector2D<float>(1f, 1f));
        sprite.Visible = false;
    }

    private void InitializeText(EntityId entity)
    {
        _world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var text = ref _world.GetOrAdd<BitmapText>(entity);
        text.Visible = false;
        text.Content = " ";
        text.SortKey = 450f;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
        text.Style = HudStyle;
        text.IsLocalizationKey = false;
    }

    private void SetHudText(VisualBundle visuals, Vector2D<int> framebufferSize, Session session)
    {
        HideAllHudText(visuals);
        if (session.Phase == Phase.Title)
        {
            SetHudRow(visuals.TxtTitle, TitleStyle, "demo.snake.title", true, 36f, framebufferSize.Y - 50f);
            SetHudRow(visuals.TxtHintTitle, HintStyle, "demo.snake.hint_title", true, 36f, 100f);
        }
        else if (session.Phase is Phase.GameOver or Phase.Won)
        {
            var titleKey = session.Phase == Phase.Won ? "demo.snake.you_win" : "demo.snake.game_over";
            SetHudRow(visuals.TxtGameOver, GameOverStyle, titleKey, true, framebufferSize.X * 0.5f - 120f, framebufferSize.Y * 0.5f + 52f);
            var hint = session.Phase == Phase.Won ? "demo.snake.hint_win" : "demo.snake.hint_gameover";
            SetHudRow(visuals.TxtHintGo, HintStyle, hint, true, 36f, 118f);
        }
        else if (session.Phase == Phase.Playing)
        {
            SetHudRow(visuals.TxtPlaying, HudStyle, "demo.snake.playing", true, 24f, framebufferSize.Y - 36f);
            SetHudRow(visuals.TxtScore, ScoreStyle, session.FoodsEaten.ToString(), false, 110f, framebufferSize.Y - 36f);
        }
    }

    private void HideAllHudText(VisualBundle v)
    {
        _world.Get<BitmapText>(v.TxtTitle).Visible = false;
        _world.Get<BitmapText>(v.TxtHintTitle).Visible = false;
        _world.Get<BitmapText>(v.TxtGameOver).Visible = false;
        _world.Get<BitmapText>(v.TxtHintGo).Visible = false;
        _world.Get<BitmapText>(v.TxtPlaying).Visible = false;
        _world.Get<BitmapText>(v.TxtScore).Visible = false;
    }

    private void SetHudRow(EntityId e, TextStyle style, string content, bool isKey, float screenX, float screenY)
    {
        ref var transform = ref _world.Get<Transform>(e);
        transform.LocalPosition = new Vector2D<float>(screenX, screenY);
        transform.WorldPosition = transform.LocalPosition;
        ref var bt = ref _world.Get<BitmapText>(e);
        bt.Visible = true;
        bt.Style = style;
        bt.Content = content;
        bt.IsLocalizationKey = isKey;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.SortKey = 450f;
    }
}
