using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo;

/// <summary>
/// One-shot scene bootstrap: camera, gameplay sprites, HUD, stationary lights, follow lights, bloom volume, and global post.
/// </summary>
/// <remarks>
/// Registered as a sequential <see cref="ISystem"/> with no phase hooks so only <see cref="OnStart"/> runs—everything here is
/// cold-start authoring. Helpers below mirror “spawn sections” you might split across files in a larger mod.
/// <see cref="DesignCanvas"/> keeps radii/positions consistent with the virtual 1280×720 simulation space used elsewhere.
/// </remarks>
public sealed class SceneSetupSystem : ISystem
{
    private readonly GameHostServices _host;

    public SceneSetupSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = archetype;
        var renderer = _host.Renderer
            ?? throw new InvalidOperationException("cyberland.demo/scene-setup requires Host.Renderer during OnStart.");

        var white = renderer.WhiteTextureId;
        var defaultNormal = renderer.DefaultNormalTextureId;
        var canvas = DesignCanvas.Demo1280x720;

        // Spawn order is readability-only; ECS does not infer dependencies from creation order. Tags identify singletons for queries.
        SpawnCamera(world, canvas);
        var player = SpawnPlayer(world, white, defaultNormal);
        SpawnBackground(world, white, defaultNormal);
        SpawnNeonStrip(world, white, defaultNormal);
        SpawnHudTitle(world);
        SpawnHudHint(world);
        SpawnHudFps(world);
        SpawnStationaryHdrLightRig(world, canvas);
        SpawnPlayerFollowPointLight(world, player);
        SpawnFullscreenBloomVolume(world, canvas);
        SpawnGlobalPostProcessTuning(world);
    }

    /// <summary>
    /// Virtual resolution shared by camera, lights, and bloom volume authoring (matches <see cref="ModLayoutViewport"/> defaults for this demo).
    /// </summary>
    private readonly struct DesignCanvas
    {
        public int WidthPx { get; }
        public int HeightPx { get; }
        public float W { get; }
        public float H { get; }
        public float HalfW { get; }
        public float HalfH { get; }

        public static DesignCanvas Demo1280x720 => new(1280, 720);

        private DesignCanvas(int widthPx, int heightPx)
        {
            WidthPx = widthPx;
            HeightPx = heightPx;
            W = widthPx;
            H = heightPx;
            HalfW = W * 0.5f;
            HalfH = H * 0.5f;
        }
    }

    private static void SpawnCamera(World world, in DesignCanvas canvas)
    {
        // Camera transform sits at canvas center in world units; Camera2D pixel size matches DesignCanvas for 1:1 simulation framing.
        var entity = world.CreateEntity();
        var camTransform = Transform.Identity;
        camTransform.WorldPosition = new Vector2D<float>(canvas.HalfW, canvas.HalfH);
        world.GetOrAdd<Transform>(entity) = camTransform;
        world.GetOrAdd<Camera2D>(entity) = Camera2D.Create(new Vector2D<int>(canvas.WidthPx, canvas.HeightPx));
    }

    private static EntityId SpawnPlayer(World world, TextureId white, TextureId defaultNormal)
    {
        var player = world.CreateEntity();
        world.GetOrAdd<PlayerTag>(player);
        world.GetOrAdd<Transform>(player) = Transform.Identity;
        world.GetOrAdd<Velocity>(player);
        ref var playerSprite = ref world.GetOrAdd<Sprite>(player);
        playerSprite = Sprite.DefaultWhiteUnlit(
            white,
            defaultNormal,
            new Vector2D<float>(Constants.SpriteHalfExtent, Constants.SpriteHalfExtent));
        playerSprite.Layer = (int)SpriteLayer.World;
        playerSprite.SortKey = 2f;
        playerSprite.ColorMultiply = new Vector4D<float>(0f, 0.9f, 0f, 1f);
        return player;
    }

    private static void SpawnBackground(World world, TextureId white, TextureId defaultNormal)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<BackgroundTag>(entity);
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var sprite = ref world.GetOrAdd<Sprite>(entity);
        sprite.Layer = (int)SpriteLayer.Background;
        sprite.AlbedoTextureId = white;
        sprite.NormalTextureId = defaultNormal;
        sprite.ColorMultiply = new Vector4D<float>(0.07f, 0.06f, 0.12f, 1f);
        sprite.SortKey = 0f;
        sprite.Visible = true;
        world.GetOrAdd<ViewportAnchor2D>(entity) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.WorldSpace,
            Anchor = ViewportAnchorPreset.Center,
            OffsetX = 0f,
            OffsetY = 0f,
            SyncSpriteHalfExtentsToViewport = true
        };
    }

    private static void SpawnNeonStrip(World world, TextureId white, TextureId defaultNormal)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<NeonStripTag>(entity);
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var sprite = ref world.GetOrAdd<Sprite>(entity);
        sprite.HalfExtents = new Vector2D<float>(36f, 140f);
        sprite.Layer = (int)SpriteLayer.World;
        sprite.AlbedoTextureId = white;
        sprite.NormalTextureId = defaultNormal;
        sprite.ColorMultiply = new Vector4D<float>(0.15f, 0.95f, 0.85f, 1f);
        sprite.EmissiveTint = new Vector3D<float>(0.35f, 1f, 0.9f);
        sprite.EmissiveIntensity = 2.4f;
        sprite.SortKey = 1f;
        sprite.Visible = true;
        world.GetOrAdd<ViewportAnchor2D>(entity) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.WorldSpace,
            Anchor = ViewportAnchorPreset.LeftCenter,
            OffsetX = 110f,
            OffsetY = 0f,
            SyncSpriteHalfExtentsToViewport = false
        };
    }

    private static void SpawnHudTitle(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<HudTitleTag>(entity);
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var text = ref world.GetOrAdd<BitmapText>(entity);
        text.Visible = true;
        text.IsLocalizationKey = true;
        text.Content = "demo.hdr.title";
        text.Style = new TextStyle(BuiltinFonts.UiSans, 22f, new Vector4D<float>(0.85f, 0.95f, 1f, 1f), Bold: true);
        text.SortKey = 450f;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
        world.GetOrAdd<ViewportAnchor2D>(entity) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ViewportSpace,
            Anchor = ViewportAnchorPreset.BottomLeft,
            OffsetX = 24f,
            OffsetY = 36f,
            SyncSpriteHalfExtentsToViewport = false
        };
    }

    private static void SpawnHudHint(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<HudHintTag>(entity);
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var text = ref world.GetOrAdd<BitmapText>(entity);
        text.Visible = true;
        text.IsLocalizationKey = true;
        text.Content = "demo.hdr.hint";
        text.Style = new TextStyle(BuiltinFonts.UiSans, 15f, new Vector4D<float>(0.55f, 0.65f, 0.75f, 0.9f), Italic: true);
        text.SortKey = 451f;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
        world.GetOrAdd<ViewportAnchor2D>(entity) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ViewportSpace,
            Anchor = ViewportAnchorPreset.TopLeft,
            OffsetX = 24f,
            OffsetY = 48f,
            SyncSpriteHalfExtentsToViewport = false
        };
    }

    private static void SpawnHudFps(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<HudFpsTag>(entity);
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var text = ref world.GetOrAdd<BitmapText>(entity);
        text.Visible = true;
        text.IsLocalizationKey = false;
        text.Content = "FPS —";
        text.Style = new TextStyle(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f));
        text.SortKey = 452f;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
    }

    /// <summary>Non-moving lights; radii/positions scale with the design canvas (same virtual size as the camera).</summary>
    private static void SpawnStationaryHdrLightRig(World world, in DesignCanvas canvas)
    {
        SpawnHdrAmbient(world);
        SpawnHdrDirectional(world);
        SpawnHdrSpot(world, canvas);
        SpawnHdrWarmPoint(world, canvas);
    }

    private static void SpawnHdrAmbient(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<AmbientLightSource>(entity) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.38f, 0.4f, 0.48f),
            Intensity = 0.11f
        };
    }

    private static void SpawnHdrDirectional(World world)
    {
        var entity = world.CreateEntity();
        var transform = Transform.Identity;
        // Root entity: local rotation is world rotation until a parent exists.
        transform.LocalRotationRadians = MathF.Atan2(-0.55f, 0.4f);
        world.GetOrAdd<Transform>(entity) = transform;
        world.GetOrAdd<DirectionalLightSource>(entity) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.5f, 0.48f, 0.44f),
            Intensity = 0.14f,
            CastsShadow = false
        };
    }

    private static void SpawnHdrSpot(World world, in DesignCanvas canvas)
    {
        var spotPos = new Vector2D<float>(canvas.W * 0.2f, canvas.H * 0.58f);
        // Cone aims toward canvas center so the hotspot reads as “into the scene” from the upper-left arc.
        var dx = canvas.HalfW - spotPos.X;
        var dy = canvas.HalfH - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dirVec = dLen > 1e-4f ? new Vector2D<float>(dx / dLen, dy / dLen) : new Vector2D<float>(1f, 0f);

        var entity = world.CreateEntity();
        var transform = Transform.Identity;
        transform.LocalPosition = spotPos;
        transform.LocalRotationRadians = MathF.Atan2(dirVec.Y, dirVec.X);
        world.GetOrAdd<Transform>(entity) = transform;
        world.GetOrAdd<SpotLightSource>(entity) = new SpotLightSource
        {
            Active = true,
            Radius = canvas.W * 0.46f,
            InnerConeRadians = MathF.PI / 3.5f,
            OuterConeRadians = MathF.PI / 2.15f,
            Color = new Vector3D<float>(0.42f, 0.62f, 1f),
            Intensity = 0.42f,
            CastsShadow = false
        };
    }

    private static void SpawnHdrWarmPoint(World world, in DesignCanvas canvas)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<HdrWarmPointTag>(entity);
        var transform = Transform.Identity;
        transform.LocalPosition = new Vector2D<float>(canvas.W * 0.76f, canvas.H * 0.3f);
        world.GetOrAdd<Transform>(entity) = transform;
        world.GetOrAdd<PointLightSource>(entity) = new PointLightSource
        {
            Active = true,
            Radius = canvas.W * 0.4f,
            Color = new Vector3D<float>(1f, 0.65f, 0.38f),
            Intensity = 0.48f,
            FalloffExponent = 2.1f,
            CastsShadow = false
        };
    }

    private static void SpawnPlayerFollowPointLight(World world, EntityId player)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<HdrPlayerPointTag>(entity);
        var transform = Transform.Identity;
        // Follow the player via hierarchy; TransformHierarchySystem + PointLightSystem use world matrix each frame.
        transform.Parent = player;
        world.GetOrAdd<Transform>(entity) = transform;
        world.GetOrAdd<PointLightSource>(entity) = new PointLightSource
        {
            Active = true,
            Radius = 180f,
            Color = new Vector3D<float>(0.35f, 0.95f, 0.55f),
            Intensity = 1.2f,
            FalloffExponent = 2.25f,
            CastsShadow = false
        };
    }

    private static void SpawnFullscreenBloomVolume(World world, in DesignCanvas canvas)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<HdrBloomVolumeTag>(entity);
        // Seed from Identity so scale is (1,1) rather than a default zero matrix.
        var transform = Transform.Identity;
        transform.LocalPosition = new Vector2D<float>(canvas.HalfW, canvas.HalfH);
        world.GetOrAdd<Transform>(entity) = transform;
        // Priority/overrides match runtime tuning in HdrPostVolumeFillSystem; bloom gain is driven per frame from player X.
        world.GetOrAdd<PostProcessVolumeSource>(entity) = new PostProcessVolumeSource
        {
            Active = true,
            Volume = new PostProcessVolume
            {
                HalfExtentsLocal = new Vector2D<float>(canvas.HalfW, canvas.HalfH),
                Priority = 1,
                Overrides = new PostProcessOverrides
                {
                    HasBloomGain = true,
                    BloomGain = HdrDemoBloom.GainAtPlayerLeft,
                    HasExposure = false,
                    HasSaturation = false
                }
            }
        };
    }

    /// <summary>Stacks with <c>EngineDefaultGlobalPostProcess.Apply</c> from <c>GameApplication</c>.</summary>
    private static void SpawnGlobalPostProcessTuning(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<GlobalPostProcessSource>(entity) = new GlobalPostProcessSource
        {
            Active = true,
            Priority = 100,
            Settings = new GlobalPostProcessSettings
            {
                BloomEnabled = true,
                BloomRadius = 1.5f,
                BloomGain = 1.1f,
                BloomExtractThreshold = 0.32f,
                BloomExtractKnee = 0.5f,
                EmissiveToHdrGain = 0.45f,
                EmissiveToBloomGain = 0.6f,
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
