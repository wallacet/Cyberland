using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

// BrickBreaker tutorial: session entities for GameState + Control; bricks are Cell-tagged entities laid out by ArenaLayoutSystem.
// Pipeline: input (early, seq) -> arena layout (early, parallel brick chunks) -> lifecycle + winlose (fixed, parallel brick chunks) ->
// paddle / launch / ball / triggers (fixed, seq, singleton ball) -> lights + visual sync (late, seq).
//
// MergeStringTableAsync blocks synchronously during mod load so locale keys exist before OnStart.
public sealed class Mod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        context.LocalizedContent.MergeStringTableAsync("brick.json").GetAwaiter().GetResult();

        var w = context.World;

        // Fixed virtual canvas (see Constants.CanvasWidth/Height) so arena layout matches the camera viewport
        // regardless of physical window size; camera sits at the canvas center.
        var camera = w.CreateEntity();
        var camTransform = Transform.Identity;
        camTransform.WorldPosition = new Vector2D<float>(Constants.CanvasWidth * 0.5f, Constants.CanvasHeight * 0.5f);
        w.Components<Transform>().GetOrAdd(camera) = camTransform;
        w.Components<Camera2D>().GetOrAdd(camera) = Camera2D.Create(new Vector2D<int>(Constants.CanvasWidth, Constants.CanvasHeight));

        var stateEntity = w.CreateEntity();
        w.Components<GameState>().GetOrAdd(stateEntity) = new GameState
        {
            Phase = Phase.Title,
            Lives = Constants.StartingLives,
            BallDocked = true
        };
        var controlEntity = w.CreateEntity();
        w.Components<Control>().GetOrAdd(controlEntity);

        static EntityId Sprite(World world)
        {
            var e = world.CreateEntity();
            world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
            world.Components<Sprite>().GetOrAdd(e);
            return e;
        }

        var background = Sprite(w);
        var paddle = Sprite(w);
        w.Components<Paddle>().GetOrAdd(paddle);
        w.Components<PaddleBody>().GetOrAdd(paddle) = new PaddleBody { HalfWidth = 72f, HalfHeight = 10f };
        w.Components<Trigger>().GetOrAdd(paddle) = new Trigger
        {
            Enabled = false,
            Shape = TriggerShapeKind.Rectangle,
            HalfExtents = new Vector2D<float>(72f, 10f)
        };
        var ball = Sprite(w);
        w.Components<Velocity>().GetOrAdd(ball);
        w.Components<Trigger>().GetOrAdd(ball) = new Trigger
        {
            Enabled = false,
            Shape = TriggerShapeKind.Circle,
            Radius = Constants.BallR
        };
        var titleUi = Sprite(w);
        var gameOverPanel = Sprite(w);
        var gameOverBar = Sprite(w);
        var lives = new EntityId[Constants.StartingLives];
        for (var i = 0; i < lives.Length; i++)
            lives[i] = Sprite(w);

        var cells = new EntityId[Constants.Cols, Constants.Rows];
        for (var cx = 0; cx < Constants.Cols; cx++)
        for (var cy = 0; cy < Constants.Rows; cy++)
        {
            cells[cx, cy] = Sprite(w);
            w.Components<Cell>().GetOrAdd(cells[cx, cy]) = new Cell { X = cx, Y = cy };
            w.Components<BrickState>().GetOrAdd(cells[cx, cy]) = new BrickState { Active = false };
            w.Components<Trigger>().GetOrAdd(cells[cx, cy]) = new Trigger
            {
                Enabled = false,
                Shape = TriggerShapeKind.Rectangle,
                HalfExtents = new Vector2D<float>(1f, 1f)
            };
        }

        static EntityId HudText(World world, float sortKey)
        {
            var e = world.CreateEntity();
            world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
            ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
            bt.Visible = false;
            bt.Content = " ";
            bt.SortKey = sortKey;
            bt.CoordinateSpace = CoordinateSpace.ScreenSpace;
            bt.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
            bt.IsLocalizationKey = false;
            return e;
        }

        var texts = new HudTextIds(
            HudText(w, 450f),
            HudText(w, 451f),
            HudText(w, 452f),
            HudText(w, 453f),
            HudText(w, 454f),
            HudText(w, 455f),
            HudText(w, 456f));

        var amb = w.CreateEntity();
        w.Components<AmbientLightSource>().GetOrAdd(amb) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.22f, 0.24f, 0.32f),
            Intensity = 0.14f
        };
        var dirL = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(dirL) = Transform.Identity;
        w.Components<DirectionalLightSource>().GetOrAdd(dirL) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.55f, 0.52f, 0.48f),
            Intensity = 0.22f,
            CastsShadow = false
        };
        var spotE = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(spotE) = Transform.Identity;
        w.Components<SpotLightSource>().GetOrAdd(spotE) = new SpotLightSource
        {
            Active = true,
            Radius = 560f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.2f,
            Color = new Vector3D<float>(0.35f, 0.55f, 0.95f),
            Intensity = 0.38f,
            CastsShadow = false
        };
        var paddlePt = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(paddlePt) = Transform.Identity;
        w.Components<PointLightSource>().GetOrAdd(paddlePt) = new PointLightSource
        {
            Active = true,
            Radius = 280f,
            Color = new Vector3D<float>(1f, 0.55f, 0.28f),
            Intensity = 0.32f,
            FalloffExponent = 2.2f,
            CastsShadow = false
        };
        var ballPt = w.CreateEntity();
        w.Components<Transform>().GetOrAdd(ballPt) = Transform.Identity;
        w.Components<PointLightSource>().GetOrAdd(ballPt) = new PointLightSource
        {
            Active = true,
            Radius = 140f,
            Color = new Vector3D<float>(0.85f, 0.95f, 1f),
            Intensity = 0.55f,
            FalloffExponent = 2f,
            CastsShadow = false
        };

        var host = context.Host;
        context.RegisterSequential("cyberland.demo.brick/input", new InputSystem(host, stateEntity, controlEntity));
        context.RegisterParallel("cyberland.demo.brick/layout", new ArenaLayoutSystem(host, stateEntity));
        context.RegisterParallel("cyberland.demo.brick/lifecycle",
            new RoundLifecycleSystem(stateEntity, controlEntity, paddle, ball));
        context.RegisterSequential("cyberland.demo.brick/paddle-move",
            new PaddleMoveSystem(stateEntity, controlEntity, paddle));
        context.RegisterSequential("cyberland.demo.brick/ball-launch",
            new BallLaunchSystem(stateEntity, controlEntity, paddle, ball));
        context.RegisterSequential("cyberland.demo.brick/ball-integrate",
            new BallIntegrateSystem(stateEntity, paddle, ball));
        context.RegisterSequential("cyberland.demo.brick/trigger-resolve",
            new TriggerResolveSystem(stateEntity, paddle, ball));
        context.RegisterParallel("cyberland.demo.brick/winlose", new WinLoseSystem(stateEntity));
        context.RegisterSequential("cyberland.demo.brick/lights",
            new BrickBreakerLightsFillSystem(host, stateEntity, paddle, ball, amb, dirL, spotE, paddlePt, ballPt));
        context.RegisterSequential("cyberland.demo.brick/visual-sync",
            new VisualSyncSystem(host, stateEntity, background, paddle, ball, titleUi, gameOverPanel, gameOverBar, lives,
                cells, texts));

        ApplyBrickGlobalPost(host);
    }

    public void OnUnload()
    {
    }

    // Replaces engine baseline HDR for this mod; last writer among mods wins for global post settings.
    private static void ApplyBrickGlobalPost(GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.BrickBreaker — Post-process unavailable",
                "Host.Renderer was null; global HDR/bloom settings for the demo were not applied.");
            return;
        }

        r.SetGlobalPostProcess(new GlobalPostProcessSettings
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
        });
    }
}
