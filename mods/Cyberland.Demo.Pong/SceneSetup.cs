using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Cold-start authoring for Pong: camera, session row (<see cref="State"/> + <see cref="Control"/>), sprites, HUD, lights, global post.
/// </summary>
/// <remarks>
/// <para><b>Virtual canvas:</b> 1280×720 camera centered on the arena so paddle/ball math stays in world units aligned with that rectangle.</para>
/// <para><b>Session row:</b> one entity carries <see cref="State"/> + <see cref="Control"/>; <see cref="SimulationSystem"/>, <see cref="InputSystem"/>, <see cref="PongLightsFillSystem"/> use <see cref="ISingletonSystem"/> queries against that archetype.</para>
/// <para><b>Visual bundle:</b> <see cref="ColdStart"/> returns explicit <see cref="VisualIds"/> / <see cref="HudTextIds"/> for sprites and HUD <see cref="BitmapText"/> rows that <see cref="VisualSyncSystem"/> mutates each frame (handles not stored on the session row).</para>
/// </remarks>
public static class SceneSetup
{
    /// <summary>Sprite and HUD entity bundles created here — passed into simulation/visual systems that drive those rows.</summary>
    public readonly record struct ColdStart(VisualIds Visuals, HudTextIds Texts);

    /// <summary>Spawns the Pong scene. Async shell matches HDR/BrickBreaker so future disk-backed layout can await I/O.</summary>
    public static async ValueTask<ColdStart> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var world = context.World;

        var session = world.CreateEntity();
        world.GetOrAdd<State>(session);
        world.GetOrAdd<Control>(session);

        // Fixed 1280×720 virtual canvas: camera centered so paddle/ball math stays in world units aligned with that rectangle.
        const int CanvasWidth = 1280;
        const int CanvasHeight = 720;
        var camera = world.CreateEntity();
        var camTransform = Transform.Identity;
        camTransform.WorldPosition = new Vector2D<float>(CanvasWidth * 0.5f, CanvasHeight * 0.5f);
        world.GetOrAdd<Transform>(camera) = camTransform;
        world.GetOrAdd<Camera2D>(camera) = Camera2D.Create(new Vector2D<int>(CanvasWidth, CanvasHeight));

        var visuals = new VisualIds(
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world),
            CreateSpriteEntity(world));

        var texts = new HudTextIds(
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world),
            CreateHudTextEntity(world));

        var amb = world.CreateEntity();
        world.GetOrAdd<AmbientLightSource>(amb) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.2f, 0.23f, 0.3f),
            Intensity = 0.12f
        };
        var dir = world.CreateEntity();
        world.GetOrAdd<Transform>(dir) = Transform.Identity;
        world.GetOrAdd<DirectionalLightSource>(dir) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.52f, 0.5f, 0.46f),
            Intensity = 0.19f,
            CastsShadow = false
        };
        var spot = world.CreateEntity();
        world.GetOrAdd<Transform>(spot) = Transform.Identity;
        world.GetOrAdd<SpotLightSource>(spot) = new SpotLightSource
        {
            Active = true,
            Radius = 460f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.2f,
            Color = new Vector3D<float>(0.38f, 0.58f, 1f),
            Intensity = 0.35f,
            CastsShadow = false
        };
        var ballPt = world.CreateEntity();
        world.GetOrAdd<Transform>(ballPt) = Transform.Identity;
        world.GetOrAdd<BallAccentPointLightTag>(ballPt);
        world.GetOrAdd<PointLightSource>(ballPt) = new PointLightSource
        {
            Active = true,
            Radius = 280f,
            Color = new Vector3D<float>(0.9f, 0.95f, 1f),
            Intensity = 0.52f,
            FalloffExponent = 2f,
            CastsShadow = false
        };
        var leftPt = world.CreateEntity();
        world.GetOrAdd<Transform>(leftPt) = Transform.Identity;
        world.GetOrAdd<LeftPaddleAccentPointLightTag>(leftPt);
        world.GetOrAdd<PointLightSource>(leftPt) = new PointLightSource
        {
            Active = true,
            Radius = 320f,
            Color = new Vector3D<float>(0.25f, 0.75f, 1f),
            Intensity = 0.34f,
            FalloffExponent = 2.1f,
            CastsShadow = false
        };

        ApplyGlobalPost(world);

        await Task.CompletedTask.ConfigureAwait(false);

        return new ColdStart(visuals, texts);
    }

    private static EntityId CreateSpriteEntity(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        world.GetOrAdd<Sprite>(entity);
        return entity;
    }

    private static EntityId CreateHudTextEntity(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var text = ref world.GetOrAdd<BitmapText>(entity);
        text.Visible = false;
        text.Content = " ";
        text.SortKey = 450f;
        text.CoordinateSpace = BitmapText.HudDefaultCoordinateSpace;
        text.Style = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(1f, 1f, 1f, 1f));
        text.IsLocalizationKey = false;
        return entity;
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
            BloomEnabled = true, BloomRadius = 1.1f, BloomGain = 0.3f, BloomExtractThreshold = 0.32f, BloomExtractKnee = 0.5f,
            EmissiveToHdrGain = 0.48f, EmissiveToBloomGain = 0.45f, Exposure = 1f, Saturation = 1.05f, TonemapEnabled = true,
            ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f), ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f), ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
            }
        };
    }
}
