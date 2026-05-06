using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Cold-start authoring for the breakout sample: camera, session/control entities, playfield, HUD, lights, and global post stack entry.
/// </summary>
/// <remarks>
/// Marker tags (see <see cref="SessionTag"/>, <see cref="BallTag"/>, etc.) let runtime systems resolve singletons without embedding
/// <see cref="EntityId"/> values in <see cref="Mod.OnLoadAsync"/>. Call <see cref="SetupSceneAsync"/> from mod load before registering ECS systems.
/// </remarks>
public static class SceneSetup
{
    /// <summary>
    /// Spawns the BrickBreaker ECS scene into <see cref="ModLoadContext.World"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Task.CompletedTask"/> is a placeholder await for future async scene definition I/O (e.g. reading layout from disk).
    /// </remarks>
    public static async ValueTask SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;

        var host = context.Host;

        var w = context.World;

        var camera = w.CreateEntity();
        var camTransform = Transform.Identity;
        camTransform.WorldPosition = new Vector2D<float>(Constants.CanvasWidth * 0.5f, Constants.CanvasHeight * 0.5f);
        w.GetOrAdd<Transform>(camera) = camTransform;
        w.GetOrAdd<Camera2D>(camera) = Camera2D.Create(new Vector2D<int>(Constants.CanvasWidth, Constants.CanvasHeight));

        var stateEntity = w.CreateEntity();
        w.GetOrAdd<SessionTag>(stateEntity) = default;
        w.GetOrAdd<GameState>(stateEntity) = new GameState
        {
            Phase = Phase.Title,
            Lives = Constants.StartingLives,
            BallDocked = true
        };

        var controlEntity = w.CreateEntity();
        w.GetOrAdd<ControlTag>(controlEntity) = default;
        w.GetOrAdd<Control>(controlEntity);

        static EntityId Sprite(World world)
        {
            var e = world.CreateEntity();
            world.GetOrAdd<Transform>(e) = Transform.Identity;
            world.GetOrAdd<Sprite>(e);
            return e;
        }

        var background = Sprite(w);
        w.GetOrAdd<BackgroundTag>(background) = default;

        var paddle = Sprite(w);
        w.GetOrAdd<Paddle>(paddle);
        w.GetOrAdd<PaddleBody>(paddle) = new PaddleBody { HalfWidth = 72f, HalfHeight = 10f };
        w.GetOrAdd<Trigger>(paddle) = new Trigger
        {
            Enabled = false,
            Shape = TriggerShapeKind.Rectangle,
            HalfExtents = new Vector2D<float>(72f, 10f)
        };

        var ball = Sprite(w);
        w.GetOrAdd<BallTag>(ball) = default;
        w.GetOrAdd<Velocity>(ball);
        w.GetOrAdd<Trigger>(ball) = new Trigger
        {
            Enabled = false,
            Shape = TriggerShapeKind.Circle,
            Radius = Constants.BallR
        };

        var titleUi = Sprite(w);
        w.GetOrAdd<TitleUiTag>(titleUi) = default;
        var gameOverPanel = Sprite(w);
        w.GetOrAdd<GameOverPanelTag>(gameOverPanel) = default;
        var gameOverBar = Sprite(w);
        w.GetOrAdd<GameOverBarTag>(gameOverBar) = default;

        var life0 = Sprite(w);
        w.GetOrAdd<LifePipSlot>(life0) = new LifePipSlot { Index = 0 };
        var life1 = Sprite(w);
        w.GetOrAdd<LifePipSlot>(life1) = new LifePipSlot { Index = 1 };
        var life2 = Sprite(w);
        w.GetOrAdd<LifePipSlot>(life2) = new LifePipSlot { Index = 2 };

        var cells = new EntityId[Constants.Cols, Constants.Rows];
        for (var cx = 0; cx < Constants.Cols; cx++)
        for (var cy = 0; cy < Constants.Rows; cy++)
        {
            var cellEntity = Sprite(w);
            cells[cx, cy] = cellEntity;
            w.GetOrAdd<Cell>(cellEntity) = new Cell { X = cx, Y = cy };
            w.GetOrAdd<ArenaCellState>(cellEntity) = new ArenaCellState { Active = false };
            w.GetOrAdd<Trigger>(cellEntity) = new Trigger
            {
                Enabled = false,
                Shape = TriggerShapeKind.Rectangle,
                HalfExtents = new Vector2D<float>(1f, 1f)
            };
        }

        static EntityId HudTextRow<TTag>(World world, float sortKey) where TTag : struct, IComponent
        {
            var e = world.CreateEntity();
            world.GetOrAdd<Transform>(e) = Transform.Identity;
            world.GetOrAdd<TTag>(e) = default;
            ref var bt = ref world.GetOrAdd<BitmapText>(e);
            bt.Visible = false;
            bt.Content = " ";
            bt.SortKey = sortKey;
            bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
            bt.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
            bt.IsLocalizationKey = false;
            return e;
        }

        _ = HudTextRow<HudTitleTag>(w, 450f);
        _ = HudTextRow<HudHintTitleTag>(w, 451f);
        _ = HudTextRow<HudGameOverTag>(w, 452f);
        _ = HudTextRow<HudHintEndTag>(w, 453f);
        _ = HudTextRow<HudPlayingScoreTag>(w, 454f);
        _ = HudTextRow<HudScoreNumTag>(w, 455f);
        _ = HudTextRow<HudFpsTag>(w, 456f);

        var white = host.Renderer.WhiteTextureId;
        var normal = host.Renderer.DefaultNormalTextureId;
        ApplyStaticSpriteAuthoring(
            w,
            white,
            normal,
            background,
            paddle,
            ball,
            titleUi,
            gameOverPanel,
            gameOverBar,
            life0,
            life1,
            life2,
            cells);

        var amb = w.CreateEntity();
        w.GetOrAdd<AmbientLightTag>(amb) = default;
        w.GetOrAdd<AmbientLightSource>(amb) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.22f, 0.24f, 0.32f),
            Intensity = 0.14f
        };
        var dirL = w.CreateEntity();
        w.GetOrAdd<DirectionalLightTag>(dirL) = default;
        w.GetOrAdd<Transform>(dirL) = Transform.Identity;
        w.GetOrAdd<DirectionalLightSource>(dirL) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.55f, 0.52f, 0.48f),
            Intensity = 0.22f,
            CastsShadow = false
        };
        var spotE = w.CreateEntity();
        w.GetOrAdd<ArenaSpotLightTag>(spotE) = default;
        w.GetOrAdd<Transform>(spotE) = Transform.Identity;
        w.GetOrAdd<SpotLightSource>(spotE) = new SpotLightSource
        {
            Active = true,
            Radius = Constants.CanvasWidth * 0.55f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.2f,
            Color = new Vector3D<float>(0.35f, 0.55f, 0.95f),
            Intensity = 0.38f,
            CastsShadow = false
        };
        var paddlePt = w.CreateEntity();
        w.GetOrAdd<PaddlePointLightTag>(paddlePt) = default;
        w.GetOrAdd<Transform>(paddlePt) = Transform.Identity;
        w.GetOrAdd<PointLightSource>(paddlePt) = new PointLightSource
        {
            Active = true,
            Radius = Constants.CanvasWidth * 0.5f,
            Color = new Vector3D<float>(1f, 0.55f, 0.28f),
            Intensity = 0.32f,
            FalloffExponent = 2.2f,
            CastsShadow = false
        };
        var ballPt = w.CreateEntity();
        w.GetOrAdd<BallPointLightTag>(ballPt) = default;
        w.GetOrAdd<Transform>(ballPt) = Transform.Identity;
        w.GetOrAdd<PointLightSource>(ballPt) = new PointLightSource
        {
            Active = true,
            Radius = 140f,
            Color = new Vector3D<float>(0.85f, 0.95f, 1f),
            Intensity = 0.55f,
            FalloffExponent = 2f,
            CastsShadow = false
        };

        ApplyArenaLightColdDefaults(w, dirL);

        w.GetOrAdd<ArenaLightRuntime>(stateEntity) = new ArenaLightRuntime
        {
            Paddle = paddle,
            Ball = ball,
            Ambient = amb,
            Directional = dirL,
            Spot = spotE,
            PaddlePoint = paddlePt,
            BallPoint = ballPt
        };

        ApplyGlobalPost(w);
    }

    /// <summary>One-shot sprite layers, textures, and tints—runtime systems only drive visibility, layout, and phase text.</summary>
    private static void ApplyStaticSpriteAuthoring(
        World w,
        TextureId white,
        TextureId normal,
        EntityId background,
        EntityId paddle,
        EntityId ball,
        EntityId titleUi,
        EntityId gameOverPanel,
        EntityId gameOverBar,
        EntityId life0,
        EntityId life1,
        EntityId life2,
        EntityId[,] cells)
    {
        ref var bgSpr = ref w.Get<Sprite>(background);
        bgSpr.Visible = true;
        bgSpr.Layer = (int)SpriteLayer.Background;
        bgSpr.SortKey = 0f;
        bgSpr.AlbedoTextureId = white;
        bgSpr.NormalTextureId = normal;
        bgSpr.ColorMultiply = new Vector4D<float>(0.04f, 0.04f, 0.08f, 1f);
        bgSpr.EmissiveIntensity = 0f;

        ref var paddleSpr = ref w.Get<Sprite>(paddle);
        paddleSpr.Layer = (int)SpriteLayer.World;
        paddleSpr.SortKey = 5f;
        paddleSpr.AlbedoTextureId = white;
        paddleSpr.NormalTextureId = normal;
        paddleSpr.ColorMultiply = new Vector4D<float>(0.4f, 0.85f, 1f, 1f);
        paddleSpr.EmissiveTint = new Vector3D<float>(0.4f, 0.9f, 1f);
        paddleSpr.EmissiveIntensity = 0.2f;

        ref var ballSpr = ref w.Get<Sprite>(ball);
        ballSpr.HalfExtents = new Vector2D<float>(Constants.BallR, Constants.BallR);
        ballSpr.Layer = (int)SpriteLayer.World;
        ballSpr.SortKey = 8f;
        ballSpr.AlbedoTextureId = white;
        ballSpr.NormalTextureId = normal;
        ballSpr.ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f);
        ballSpr.EmissiveTint = new Vector3D<float>(1f, 1f, 1f);
        ballSpr.EmissiveIntensity = 0.7f;

        ref var titleSpr = ref w.Get<Sprite>(titleUi);
        titleSpr.Layer = (int)SpriteLayer.Ui;
        titleSpr.SortKey = 20f;
        titleSpr.AlbedoTextureId = white;
        titleSpr.NormalTextureId = normal;
        titleSpr.ColorMultiply = new Vector4D<float>(0.5f, 0.75f, 1f, 1f);
        titleSpr.EmissiveTint = new Vector3D<float>(0.5f, 0.8f, 1f);
        titleSpr.EmissiveIntensity = 0.5f;

        ref var panelSpr = ref w.Get<Sprite>(gameOverPanel);
        panelSpr.Layer = (int)SpriteLayer.Ui;
        panelSpr.SortKey = 200f;
        panelSpr.AlbedoTextureId = white;
        panelSpr.NormalTextureId = normal;
        panelSpr.ColorMultiply = new Vector4D<float>(0.12f, 0.12f, 0.16f, 1f);
        panelSpr.Alpha = 0.95f;
        panelSpr.Transparent = true;
        panelSpr.EmissiveIntensity = 0f;

        ref var barSpr = ref w.Get<Sprite>(gameOverBar);
        barSpr.Layer = (int)SpriteLayer.Ui;
        barSpr.SortKey = 201f;
        barSpr.AlbedoTextureId = white;
        barSpr.NormalTextureId = normal;
        barSpr.ColorMultiply = new Vector4D<float>(1f, 0.75f, 0.2f, 1f);
        barSpr.EmissiveTint = new Vector3D<float>(1f, 0.8f, 0.2f);
        barSpr.EmissiveIntensity = 0.35f;

        InitLifeSprite(w, life0, white, normal, 0);
        InitLifeSprite(w, life1, white, normal, 1);
        InitLifeSprite(w, life2, white, normal, 2);

        for (var cx = 0; cx < Constants.Cols; cx++)
        for (var cy = 0; cy < Constants.Rows; cy++)
        {
            ref var spr = ref w.Get<Sprite>(cells[cx, cy]);
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

    private static void InitLifeSprite(World w, EntityId id, TextureId white, TextureId normal, int index)
    {
        ref var life = ref w.Get<Sprite>(id);
        life.HalfExtents = new Vector2D<float>(14f, 6f);
        life.Layer = (int)SpriteLayer.Ui;
        life.SortKey = 50f + index;
        life.AlbedoTextureId = white;
        life.NormalTextureId = normal;
        life.ColorMultiply = new Vector4D<float>(0.9f, 0.3f, 0.35f, 1f);
        life.EmissiveTint = new Vector3D<float>(1f, 0.35f, 0.4f);
        life.EmissiveIntensity = 0.2f;
    }

    /// <summary>
    /// Directional facing authored once at cold start; ambient / spot / point colors and cone data are set in the constructors above.
    /// </summary>
    private static void ApplyArenaLightColdDefaults(World w, EntityId directional)
    {
        ref var dirTransform = ref w.Get<Transform>(directional);
        var dirRad = MathF.Atan2(-0.62f, 0.35f);
        dirTransform.LocalRotationRadians = dirRad;
        dirTransform.WorldRotationRadians = dirRad;
    }

    private static void ApplyGlobalPost(World world)
    {
        var e = world.CreateEntity();
        world.GetOrAdd<GlobalPostProcessSource>(e) = new GlobalPostProcessSource
        {
            Active = true,
            Priority = 100,
            Settings = new GlobalPostProcessSettings
            {
                BloomEnabled = true,
                BloomRadius = 1.1f,
                BloomGain = 0.3f,
                BloomExtractThreshold = 0.32f,
                BloomExtractKnee = 0.5f,
                EmissiveToHdrGain = 0.48f,
                EmissiveToBloomGain = 0.45f,
                Exposure = 1f,
                Saturation = 1.05f,
                TonemapEnabled = true,
                ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f),
                ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f),
                ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
            }
        };
    }
}
