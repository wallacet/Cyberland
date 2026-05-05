using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>
/// One-off scene authoring for Snake: camera, session/control markers, tilemap + visuals rows, lights, and global post.
/// </summary>
/// <remarks>
/// Await from <see cref="Mod.OnLoadAsync"/> before any <c>Register*</c> so singleton/runtime systems resolve entities via <see cref="SystemQuerySpec"/>.
/// </remarks>
public static class SceneSetup
{
    /// <summary>
    /// Spawns the Snake playfield and lighting. Kept <c>async</c> so future disk-backed tables or locale merges can await I/O without reshaping load code.
    /// </summary>
    public static async ValueTask SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var w = context.World;

        // Camera anchors Snake's gameplay to a fixed 1280x720 canvas; non-matching window sizes letterbox
        // instead of exposing more tiles. The camera sits at the canvas center so world-space sprites keep
        // the legacy "fb.Y - offset = near top" convention from before cameras were introduced.
        const int CanvasWidth = 1280;
        const int CanvasHeight = 720;
        var camera = w.CreateEntity();
        var camTransform = Transform.Identity;
        camTransform.WorldPosition = new Vector2D<float>(CanvasWidth * 0.5f, CanvasHeight * 0.5f);
        w.GetOrAdd<Transform>(camera) = camTransform;
        w.GetOrAdd<Camera2D>(camera) = Camera2D.Create(new Vector2D<int>(CanvasWidth, CanvasHeight));

        var controlEntity = w.CreateEntity();
        w.GetOrAdd<Control>(controlEntity);
        var sessionEntity = w.CreateEntity();
        w.GetOrAdd<Session>(sessionEntity);
        var arena = w.CreateEntity();
        w.GetOrAdd<Transform>(arena) = Transform.Identity;
        w.GetOrAdd<Tilemap>(arena);
        var visualsEntity = w.CreateEntity();
        w.GetOrAdd<VisualBundle>(visualsEntity);

        var amb = w.CreateEntity();
        w.GetOrAdd<AmbientLightSource>(amb) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.22f, 0.26f, 0.32f),
            Intensity = 0.13f
        };
        var dir = w.CreateEntity();
        w.GetOrAdd<Transform>(dir) = Transform.Identity;
        w.GetOrAdd<DirectionalLightSource>(dir) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.52f, 0.5f, 0.46f),
            Intensity = 0.2f,
            CastsShadow = false
        };
        var spot = w.CreateEntity();
        w.GetOrAdd<Transform>(spot) = Transform.Identity;
        w.GetOrAdd<SpotLightSource>(spot) = new SpotLightSource
        {
            Active = true,
            Radius = 500f,
            InnerConeRadians = MathF.PI / 4f,
            OuterConeRadians = MathF.PI / 2.15f,
            Color = new Vector3D<float>(0.35f, 0.72f, 1f),
            Intensity = 0.36f,
            CastsShadow = false
        };
        var headPt = w.CreateEntity();
        w.GetOrAdd<Transform>(headPt) = Transform.Identity;
        w.GetOrAdd<HeadFollowPointLightTag>(headPt);
        w.GetOrAdd<PointLightSource>(headPt) = new PointLightSource
        {
            Active = true,
            Radius = 260f,
            Color = new Vector3D<float>(0.35f, 1f, 0.55f),
            Intensity = 0.52f,
            FalloffExponent = 2f,
            CastsShadow = false
        };
        var foodPt = w.CreateEntity();
        w.GetOrAdd<Transform>(foodPt) = Transform.Identity;
        w.GetOrAdd<FoodFollowPointLightTag>(foodPt);
        w.GetOrAdd<PointLightSource>(foodPt) = new PointLightSource
        {
            Active = true,
            Radius = 220f,
            Color = new Vector3D<float>(1f, 0.45f, 0.28f),
            Intensity = 0.44f,
            FalloffExponent = 2.2f,
            CastsShadow = false
        };

        ApplyGlobalPost(w);

        await Task.CompletedTask.ConfigureAwait(false);
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
                BloomEnabled = true, BloomRadius = 1.1f, BloomGain = 0.26f, BloomExtractThreshold = 0.32f, BloomExtractKnee = 0.5f,
                EmissiveToHdrGain = 0.48f, EmissiveToBloomGain = 0.45f, Exposure = 1f, Saturation = 1.08f, TonemapEnabled = true,
                ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f), ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f), ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
            }
        };
    }
}
