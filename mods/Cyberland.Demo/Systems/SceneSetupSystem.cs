using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// One-shot bootstrap: creates entities, applies global HDR. Registered as <strong>sequential</strong> because there is no
/// parallel work—only <see cref="ISystem.OnStart"/> runs (no early/fixed/late callbacks).
/// </summary>
public sealed class SceneSetupSystem : ISystem
{
    private readonly GameHostServices _host;

    public SceneSetupSystem(GameHostServices host) => _host = host;

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = archetype;
        var renderer = _host.Renderer
                       ?? throw new InvalidOperationException("cyberland.demo/scene-setup requires Host.Renderer during OnStart.");

        var white = renderer.WhiteTextureId;
        var defaultNormal = renderer.DefaultNormalTextureId;

        var player = world.CreateEntity();
        world.Components<PlayerTag>().GetOrAdd(player);
        world.Components<Transform>().GetOrAdd(player) = Transform.Identity;
        world.Components<Velocity>().GetOrAdd(player);
        ref var playerSprite = ref world.Components<Sprite>().GetOrAdd(player);
        playerSprite = Sprite.DefaultWhiteUnlit(white, defaultNormal, new Vector2D<float>(Constants.SpriteHalfExtent, Constants.SpriteHalfExtent));
        playerSprite.Layer = (int)SpriteLayer.World;
        playerSprite.SortKey = 2f;
        playerSprite.ColorMultiply = new Vector4D<float>(0f, 0.9f, 0f, 1f);

        var background = world.CreateEntity();
        world.Components<BackgroundTag>().GetOrAdd(background);
        world.Components<Transform>().GetOrAdd(background) = Transform.Identity;
        ref var backgroundSprite = ref world.Components<Sprite>().GetOrAdd(background);
        backgroundSprite.Layer = (int)SpriteLayer.Background;
        backgroundSprite.AlbedoTextureId = white;
        backgroundSprite.NormalTextureId = defaultNormal;
        backgroundSprite.ColorMultiply = new Vector4D<float>(0.07f, 0.06f, 0.12f, 1f);
        backgroundSprite.SortKey = 0f;
        backgroundSprite.Visible = true;
        world.Components<ViewportAnchor2D>().GetOrAdd(background) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.WorldSpace,
            Anchor = ViewportAnchorPreset.Center,
            OffsetX = 0f,
            OffsetY = 0f,
            SyncSpriteHalfExtentsToViewport = true
        };

        var neonStrip = world.CreateEntity();
        world.Components<NeonStripTag>().GetOrAdd(neonStrip);
        world.Components<Transform>().GetOrAdd(neonStrip) = Transform.Identity;
        ref var neonSprite = ref world.Components<Sprite>().GetOrAdd(neonStrip);
        neonSprite.HalfExtents = new Vector2D<float>(36f, 140f);
        neonSprite.Layer = (int)SpriteLayer.World;
        neonSprite.AlbedoTextureId = white;
        neonSprite.NormalTextureId = defaultNormal;
        neonSprite.ColorMultiply = new Vector4D<float>(0.15f, 0.95f, 0.85f, 1f);
        neonSprite.EmissiveTint = new Vector3D<float>(0.35f, 1f, 0.9f);
        neonSprite.EmissiveIntensity = 2.4f;
        neonSprite.SortKey = 1f;
        neonSprite.Visible = true;
        world.Components<ViewportAnchor2D>().GetOrAdd(neonStrip) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.WorldSpace,
            Anchor = ViewportAnchorPreset.LeftCenter,
            OffsetX = 110f,
            OffsetY = 0f,
            SyncSpriteHalfExtentsToViewport = false
        };

        var hudTitle = world.CreateEntity();
        world.Components<HudTitleTag>().GetOrAdd(hudTitle);
        world.Components<Transform>().GetOrAdd(hudTitle) = Transform.Identity;
        ref var titleText = ref world.Components<BitmapText>().GetOrAdd(hudTitle);
        titleText.Visible = true;
        titleText.IsLocalizationKey = true;
        titleText.Content = "demo.hdr.title";
        titleText.Style = new TextStyle(BuiltinFonts.UiSans, 22f, new Vector4D<float>(0.85f, 0.95f, 1f, 1f), Bold: true);
        titleText.SortKey = 450f;
        titleText.CoordinateSpace = CoordinateSpace.ScreenSpace;
        world.Components<ViewportAnchor2D>().GetOrAdd(hudTitle) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ScreenSpace,
            Anchor = ViewportAnchorPreset.BottomLeft,
            OffsetX = 24f,
            OffsetY = 36f,
            SyncSpriteHalfExtentsToViewport = false
        };

        var hudHint = world.CreateEntity();
        world.Components<HudHintTag>().GetOrAdd(hudHint);
        world.Components<Transform>().GetOrAdd(hudHint) = Transform.Identity;
        ref var hintText = ref world.Components<BitmapText>().GetOrAdd(hudHint);
        hintText.Visible = true;
        hintText.IsLocalizationKey = true;
        hintText.Content = "demo.hdr.hint";
        hintText.Style = new TextStyle(BuiltinFonts.UiSans, 15f, new Vector4D<float>(0.55f, 0.65f, 0.75f, 0.9f), Italic: true);
        hintText.SortKey = 451f;
        hintText.CoordinateSpace = CoordinateSpace.ScreenSpace;
        world.Components<ViewportAnchor2D>().GetOrAdd(hudHint) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ScreenSpace,
            Anchor = ViewportAnchorPreset.TopLeft,
            OffsetX = 24f,
            OffsetY = 48f,
            SyncSpriteHalfExtentsToViewport = false
        };

        var eAmb = world.CreateEntity();
        world.Components<AmbientLightSource>().GetOrAdd(eAmb) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.2f, 0.23f, 0.32f),
            Intensity = 0.14f
        };

        var eDir = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(eDir) = Transform.Identity;
        world.Components<DirectionalLightSource>().GetOrAdd(eDir) = new DirectionalLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.6f, 0.58f, 0.55f),
            Intensity = 0.2f,
            CastsShadow = false
        };

        var eSpot = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(eSpot) = Transform.Identity;
        world.Components<SpotLightSource>().GetOrAdd(eSpot) = new SpotLightSource
        {
            Active = true,
            Radius = 540f,
            InnerConeRadians = MathF.PI / 5f,
            OuterConeRadians = MathF.PI / 2.3f,
            Color = new Vector3D<float>(0.35f, 0.58f, 1f),
            Intensity = 0.4f,
            CastsShadow = false
        };

        var eWarm = world.CreateEntity();
        world.Components<HdrWarmPointTag>().GetOrAdd(eWarm);
        world.Components<Transform>().GetOrAdd(eWarm) = Transform.Identity;
        world.Components<PointLightSource>().GetOrAdd(eWarm) = new PointLightSource
        {
            Active = true,
            Radius = 420f,
            Color = new Vector3D<float>(0.95f, 0.52f, 0.25f),
            Intensity = 0.28f,
            FalloffExponent = 2.2f,
            CastsShadow = false
        };

        var ePlayerPt = world.CreateEntity();
        world.Components<HdrPlayerPointTag>().GetOrAdd(ePlayerPt);
        world.Components<Transform>().GetOrAdd(ePlayerPt) = Transform.Identity;
        world.Components<PointLightSource>().GetOrAdd(ePlayerPt) = new PointLightSource
        {
            Active = true,
            Radius = 180f,
            Color = new Vector3D<float>(0.35f, 0.95f, 0.55f),
            Intensity = 1.2f,
            FalloffExponent = 2.25f,
            CastsShadow = false
        };

        var eBloom = world.CreateEntity();
        world.Components<HdrBloomVolumeTag>().GetOrAdd(eBloom);
        var fb = renderer.SwapchainPixelSize;
        var hx = fb.X * 0.5f;
        var hy = fb.Y * 0.5f;
        // Transform's property setters rebuild the backing matrix from cached PRS; seed from Identity so the initial
        // scale is (1,1) rather than the zero-matrix default.
        var bloomTransform = Transform.Identity;
        bloomTransform.LocalPosition = new Vector2D<float>(hx, hy);
        bloomTransform.WorldPosition = new Vector2D<float>(hx, hy);
        world.Components<Transform>().GetOrAdd(eBloom) = bloomTransform;
        world.Components<PostProcessVolumeSource>().GetOrAdd(eBloom) = new PostProcessVolumeSource
        {
            Active = true,
            Volume = new PostProcessVolume
            {
                HalfExtentsLocal = new Vector2D<float>(hx, hy),
                Priority = 0,
                Overrides = default
            }
        };

        renderer.SetGlobalPostProcess(new GlobalPostProcessSettings
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
        });
    }
}
