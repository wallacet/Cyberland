using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

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

    public VisualSyncSystem(GameHostServices host, EntityId sessionEntity, EntityId visualsEntity)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _visualsEntity = visualsEntity;
    }
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = archetype;
        var renderer = _host.Renderer;
        if (renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake.VisualSyncSystem", "Renderer was null during OnStart.");
            throw new InvalidOperationException("Renderer is required by VisualSyncSystem.");
        }

        var visuals = world.Components<VisualBundle>().Get(_visualsEntity);
        var white = renderer.WhiteTextureId;
        var normal = renderer.DefaultNormalTextureId;

        for (var i = 0; i < visuals.Segments.Length; i++)
        {
            InitializeSprite(world, visuals.Segments[i], white, normal);
            ref var segmentSprite = ref world.Components<Sprite>().Get(visuals.Segments[i]);
            segmentSprite.Layer = (int)SpriteLayer.World;
            segmentSprite.EmissiveTint = new Vector3D<float>(0.2f, 1f, 0.4f);
            segmentSprite.Transparent = false;
            segmentSprite.Alpha = 1f;
        }

        InitializeSprite(world, visuals.Food, white, normal);
        ref var foodSprite = ref world.Components<Sprite>().Get(visuals.Food);
        foodSprite.Layer = (int)SpriteLayer.World;
        foodSprite.SortKey = 50f;
        foodSprite.ColorMultiply = new Vector4D<float>(1f, 0.2f, 0.25f, 1f);
        foodSprite.Alpha = 1f;
        foodSprite.EmissiveIntensity = 0.8f;
        foodSprite.EmissiveTint = new Vector3D<float>(1f, 0.3f, 0.35f);
        foodSprite.Transparent = false;

        InitializeSprite(world, visuals.TitleBar, white, normal);
        ref var titleSprite = ref world.Components<Sprite>().Get(visuals.TitleBar);
        titleSprite.Layer = (int)SpriteLayer.Ui;
        titleSprite.SortKey = 100f;
        titleSprite.ColorMultiply = new Vector4D<float>(0.3f, 1f, 0.5f, 1f);
        titleSprite.Alpha = 1f;
        titleSprite.EmissiveIntensity = 0.6f;
        titleSprite.EmissiveTint = new Vector3D<float>(0.3f, 1f, 0.5f);
        titleSprite.Transparent = false;

        InitializeSprite(world, visuals.GoPanel, white, normal);
        ref var gameOverSprite = ref world.Components<Sprite>().Get(visuals.GoPanel);
        gameOverSprite.Layer = (int)SpriteLayer.Ui;
        gameOverSprite.SortKey = 200f;
        gameOverSprite.ColorMultiply = new Vector4D<float>(0.15f, 0.15f, 0.18f, 1f);
        gameOverSprite.Alpha = 0.92f;
        gameOverSprite.EmissiveIntensity = 0f;
        gameOverSprite.Transparent = true;

        InitializeSprite(world, visuals.ScoreBar, white, normal);
        ref var scoreSprite = ref world.Components<Sprite>().Get(visuals.ScoreBar);
        scoreSprite.Layer = (int)SpriteLayer.Ui;
        scoreSprite.SortKey = 201f;
        scoreSprite.ColorMultiply = new Vector4D<float>(1f, 0.85f, 0.2f, 1f);
        scoreSprite.Alpha = 1f;
        scoreSprite.EmissiveIntensity = 0.4f;
        scoreSprite.EmissiveTint = new Vector3D<float>(1f, 0.9f, 0.2f);
        scoreSprite.Transparent = false;
        InitializeText(world, visuals.TxtTitle);
        InitializeText(world, visuals.TxtHintTitle);
        InitializeText(world, visuals.TxtGameOver);
        InitializeText(world, visuals.TxtHintGo);
        InitializeText(world, visuals.TxtPlaying);
        InitializeText(world, visuals.TxtScore);
    }
    public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var renderer = _host.Renderer;
        if (renderer is null) return;
        ref var session = ref world.Components<Session>().Get(_sessionEntity);
        var visuals = world.Components<VisualBundle>().Get(_visualsEntity);
        var fb = renderer.SwapchainPixelSize;
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
                ref var pos = ref world.Components<Position>().Get(e); pos.X = center.X; pos.Y = center.Y;
                ref var spr = ref world.Components<Sprite>().Get(e);
                var head = seg.x == headCell.x && seg.y == headCell.y;
                spr.Visible = true;
                spr.HalfExtents = new Vector2D<float>(cell * 0.45f, cell * 0.45f);
                spr.SortKey = 10f + seg.x;
                spr.ColorMultiply = head ? new Vector4D<float>(0.2f, 1f, 0.35f, 1f) : new Vector4D<float>(0.05f, 0.55f, 0.12f, 1f);
                spr.EmissiveIntensity = head ? 0.5f : 0.1f;
            }
        }
        for (var i = segIdx; i < visuals.Segments.Length; i++) world.Components<Sprite>().Get(visuals.Segments[i]).Visible = false;

        var foodCenter = session.CellCenterWorld(session.Food.x, session.Food.y, fb);
        ref var foodPosition = ref world.Components<Position>().Get(visuals.Food); foodPosition.X = foodCenter.X; foodPosition.Y = foodCenter.Y;
        ref var foodSprite = ref world.Components<Sprite>().Get(visuals.Food);
        foodSprite.Visible = true;
        foodSprite.HalfExtents = new Vector2D<float>(cell * 0.35f, cell * 0.35f);

        ref var titleSprite = ref world.Components<Sprite>().Get(visuals.TitleBar);
        if (session.Phase == Phase.Title)
        {
            var titleBar = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(fb.X * 0.5f, fb.Y - 48f), fb);
            ref var titlePosition = ref world.Components<Position>().Get(visuals.TitleBar); titlePosition.X = titleBar.X; titlePosition.Y = titleBar.Y;
            titleSprite.Visible = true;
            titleSprite.HalfExtents = new Vector2D<float>(fb.X * 0.42f, 20f);
        }
        else titleSprite.Visible = false;

        ref var gameOverSprite = ref world.Components<Sprite>().Get(visuals.GoPanel);
        ref var scoreSprite = ref world.Components<Sprite>().Get(visuals.ScoreBar);
        if (session.Phase == Phase.GameOver)
        {
            var goPanel = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f), fb);
            ref var gameOverPosition = ref world.Components<Position>().Get(visuals.GoPanel); gameOverPosition.X = goPanel.X; gameOverPosition.Y = goPanel.Y;
            gameOverSprite.Visible = true;
            gameOverSprite.HalfExtents = new Vector2D<float>(fb.X * 0.45f, 80f);
            var scoreW = fb.X * 0.45f * Math.Min(1f, session.FoodsEaten / 30f);
            var scoreBar = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(fb.X * 0.5f - (fb.X * 0.45f - scoreW) * 0.5f, fb.Y * 0.5f + 28f), fb);
            ref var scorePosition = ref world.Components<Position>().Get(visuals.ScoreBar); scorePosition.X = scoreBar.X; scorePosition.Y = scoreBar.Y;
            scoreSprite.Visible = true;
            scoreSprite.HalfExtents = new Vector2D<float>(Math.Max(0.5f, scoreW * 0.5f), 10f);
        }
        else
        {
            gameOverSprite.Visible = false;
            scoreSprite.Visible = false;
        }

        SetHudText(world, visuals, fb, session);
    }
    private static void InitializeSprite(World world, EntityId entity, int whiteTextureId, int normalTextureId)
    {
        world.Components<Position>().GetOrAdd(entity);
        ref var sprite = ref world.Components<Sprite>().GetOrAdd(entity);
        sprite = Sprite.DefaultWhiteUnlit(whiteTextureId, normalTextureId, new Vector2D<float>(1f, 1f));
        sprite.Visible = false;
    }
    private static void InitializeText(World world, EntityId entity)
    {
        world.Components<Position>().GetOrAdd(entity);
        ref var text = ref world.Components<BitmapText>().GetOrAdd(entity);
        text.Visible = false;
        text.Content = " ";
        text.SortKey = 450f;
        text.CoordinateSpace = TextCoordinateSpace.ScreenPixels;
        text.Style = HudStyle;
        text.IsLocalizationKey = false;
    }
    private static void SetHudText(World world, VisualBundle visuals, Vector2D<int> framebufferSize, Session session)
    {
        HideAllHudText(world, visuals);
        if (session.Phase == Phase.Title)
        {
            SetHudRow(world, visuals.TxtTitle, TitleStyle, "demo.snake.title", true, 36f, framebufferSize.Y - 50f);
            SetHudRow(world, visuals.TxtHintTitle, HintStyle, "demo.snake.hint_title", true, 36f, 100f);
        }
        else if (session.Phase == Phase.GameOver)
        {
            SetHudRow(world, visuals.TxtGameOver, GameOverStyle, "demo.snake.game_over", true, framebufferSize.X * 0.5f - 120f, framebufferSize.Y * 0.5f + 52f);
            SetHudRow(world, visuals.TxtHintGo, HintStyle, "demo.snake.hint_gameover", true, 36f, 118f);
        }
        else if (session.Phase == Phase.Playing)
        {
            SetHudRow(world, visuals.TxtPlaying, HudStyle, "demo.snake.playing", true, 24f, framebufferSize.Y - 36f);
            SetHudRow(world, visuals.TxtScore, ScoreStyle, session.FoodsEaten.ToString(), false, 110f, framebufferSize.Y - 36f);
        }
    }
    private static void HideAllHudText(World world, VisualBundle v)
    {
        world.Components<BitmapText>().Get(v.TxtTitle).Visible = false;
        world.Components<BitmapText>().Get(v.TxtHintTitle).Visible = false;
        world.Components<BitmapText>().Get(v.TxtGameOver).Visible = false;
        world.Components<BitmapText>().Get(v.TxtHintGo).Visible = false;
        world.Components<BitmapText>().Get(v.TxtPlaying).Visible = false;
        world.Components<BitmapText>().Get(v.TxtScore).Visible = false;
    }
    private static void SetHudRow(World world, EntityId e, TextStyle style, string content, bool isKey, float screenX, float screenY)
    {
        ref var pos = ref world.Components<Position>().Get(e); pos.X = screenX; pos.Y = screenY;
        ref var bt = ref world.Components<BitmapText>().Get(e); bt.Visible = true; bt.Style = style; bt.Content = content; bt.IsLocalizationKey = isKey; bt.CoordinateSpace = TextCoordinateSpace.ScreenPixels; bt.SortKey = 450f;
    }
}
