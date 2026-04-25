using Cyberland.Demo.MouseChase.Components;
using Cyberland.Demo.MouseChase.Systems;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo.MouseChase;

// MouseChase is a compact playable tutorial game:
// input -> fixed simulation -> trigger-driven game events -> HUD teaching prompts.
// It intentionally keeps all state in ECS singleton components so systems remain easy to inspect.
public sealed class Mod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        MouseChaseInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("mouse_chase.json");
        var renderer = context.Host.RendererRequired;

        var world = context.World;

        var cameraEntity = world.CreateEntity();
        var cameraTransform = Transform.Identity;
        cameraTransform.WorldPosition = new Vector2D<float>(640f, 360f);
        world.Components<Transform>().GetOrAdd(cameraEntity) = cameraTransform;
        world.Components<Camera2D>().GetOrAdd(cameraEntity) = Camera2D.Create(new Vector2D<int>(1280, 720));

        var stateEntity = world.CreateEntity();
        world.Components<GameState>().GetOrAdd(stateEntity) = new GameState
        {
            Phase = RoundPhase.Tutorial,
            TutorialStep = 0,
            TimerSeconds = 70f,
            Health = 100f,
            Score = 0,
            TargetScore = 140
        };

        var controlEntity = world.CreateEntity();
        world.Components<ControlState>().GetOrAdd(controlEntity);

        var player = CreateSprite(world, renderer.WhiteTextureId, renderer.DefaultNormalTextureId,
            new Vector2D<float>(260f, 360f), new Vector2D<float>(22f, 22f), new Vector4D<float>(0.2f, 0.95f, 1f, 1f));
        world.Components<PlayerTag>().GetOrAdd(player);
        world.Components<Trigger>().GetOrAdd(player) = new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 24f
        };
        world.Components<CameraFollow2D>().GetOrAdd(cameraEntity) = new CameraFollow2D
        {
            Enabled = true,
            Target = player,
            OffsetWorld = new Vector2D<float>(120f, 0f),
            FollowLerp = 0.2f,
            ClampToBounds = true,
            BoundsMinWorld = new Vector2D<float>(130f, 130f),
            BoundsMaxWorld = new Vector2D<float>(1150f, 590f)
        };

        var collectible = CreateSprite(world, renderer.WhiteTextureId, renderer.DefaultNormalTextureId,
            new Vector2D<float>(920f, 300f), new Vector2D<float>(16f, 16f), new Vector4D<float>(1f, 0.7f, 0.2f, 1f));
        world.Components<CollectibleTag>().GetOrAdd(collectible);
        world.Components<SpriteLocalizedAsset>().GetOrAdd(collectible) = new SpriteLocalizedAsset
        {
            CanonicalAlbedoPath = "Textures/Pickups/shard.png",
            ReloadGeneration = 1,
            LoadedGeneration = 0,
            KeepExistingOnMissing = true
        };
        world.Components<Trigger>().GetOrAdd(collectible) = new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Circle,
            Radius = 20f
        };

        var enterZone = CreateZone(world, renderer.WhiteTextureId, renderer.DefaultNormalTextureId,
            new Vector2D<float>(450f, 480f), new Vector2D<float>(120f, 42f), new Vector4D<float>(0.2f, 0.8f, 0.25f, 0.4f));
        world.Components<EnterZoneTag>().GetOrAdd(enterZone);

        var stayZone = CreateZone(world, renderer.WhiteTextureId, renderer.DefaultNormalTextureId,
            new Vector2D<float>(760f, 430f), new Vector2D<float>(100f, 55f), new Vector4D<float>(0.15f, 0.45f, 1f, 0.4f));
        world.Components<StayZoneTag>().GetOrAdd(stayZone);

        var exitZone = CreateZone(world, renderer.WhiteTextureId, renderer.DefaultNormalTextureId,
            new Vector2D<float>(900f, 190f), new Vector2D<float>(100f, 55f), new Vector4D<float>(1f, 0.28f, 0.3f, 0.4f));
        world.Components<ExitZoneTag>().GetOrAdd(exitZone);

        var gateZone = CreateZone(world, renderer.WhiteTextureId, renderer.DefaultNormalTextureId,
            new Vector2D<float>(1120f, 520f), new Vector2D<float>(75f, 85f), new Vector4D<float>(1f, 0.98f, 0.15f, 0.6f));
        world.Components<GateZoneTag>().GetOrAdd(gateZone);

        // Opaque world sprites: deferred_base.frag only adds ambient + directional/spot in the G-buffer pass; with no
        // submissions, albedo*lit stays 0 → black quads. (Semi-transparent tutorial zones use WBOIT and still read tinted.)
        var ambient = world.CreateEntity();
        world.Components<AmbientLightSource>().GetOrAdd(ambient) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.5f, 0.55f, 0.65f),
            Intensity = 0.75f
        };
        // Soft key: widens the lit falloff on the 1280×720 field; pairs with the tiny shard’s normal-mapped g-buffer.
        var key = world.CreateEntity();
        var keyXf = Transform.Identity;
        keyXf.WorldPosition = new Vector2D<float>(480f, 500f);
        world.Components<Transform>().GetOrAdd(key) = keyXf;
        world.Components<PointLightSource>().GetOrAdd(key) = new PointLightSource
        {
            Active = true,
            Radius = 1100f,
            Color = new Vector3D<float>(1f, 0.93f, 0.78f),
            Intensity = 0.55f,
            FalloffExponent = 2.2f,
            CastsShadow = false
        };

        var titleText = CreateHudText(world, 800f);
        var detailText = CreateHudText(world, 801f);
        var statusText = CreateHudText(world, 802f);

        var host = context.Host;
        context.RegisterSequential("cyberland.demo.mousechase/input", new InputSystem(host, controlEntity, stateEntity));
        context.RegisterSequential("cyberland.demo.mousechase/sim",
            new SimulationSystem(stateEntity, controlEntity, cameraEntity, player, collectible, enterZone, stayZone, exitZone, gateZone));
        context.RegisterSequential("cyberland.demo.mousechase/tutorial-hud",
            new TutorialHudSystem(context.LocalizedContent.Strings, stateEntity, titleText, detailText, statusText));

        ApplyGlobalPost(world);
    }

    public void OnUnload()
    {
    }

    private static EntityId CreateSprite(World world, TextureId whiteTextureId, TextureId defaultNormalTextureId,
        Vector2D<float> worldPos, Vector2D<float> halfExtents, Vector4D<float> tint)
    {
        var entity = world.CreateEntity();
        // Root transforms must start from Transform.Identity: a default matrix has zero scale, and interleaved
        // Local/World PRS writes from that state can leave world scale at 0 → invisible sprites in SpriteRenderSystem.
        var xf = Transform.Identity;
        xf.WorldPosition = worldPos;
        world.Components<Transform>().GetOrAdd(entity) = xf;
        var renderer = Sprite.DefaultWhiteUnlit(whiteTextureId, defaultNormalTextureId, halfExtents);
        renderer.Visible = true;
        renderer.Transparent = tint.W < 1f;
        renderer.ColorMultiply = tint;
        renderer.Layer = (int)SpriteLayer.World;
        world.Components<Sprite>().GetOrAdd(entity) = renderer;
        return entity;
    }

    private static EntityId CreateZone(World world, TextureId whiteTextureId, TextureId defaultNormalTextureId,
        Vector2D<float> worldPos, Vector2D<float> halfExtents, Vector4D<float> tint)
    {
        var entity = CreateSprite(world, whiteTextureId, defaultNormalTextureId, worldPos, halfExtents, tint);
        world.Components<Trigger>().GetOrAdd(entity) = new Trigger
        {
            Enabled = true,
            Shape = TriggerShapeKind.Rectangle,
            HalfExtents = halfExtents
        };
        return entity;
    }

    private static EntityId CreateHudText(World world, float sortKey)
    {
        var e = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(e) = Transform.Identity;
        ref var bt = ref world.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = " ";
        bt.SortKey = sortKey;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 1f, 1f, 1f));
        bt.IsLocalizationKey = false;
        return e;
    }

    private static void ApplyGlobalPost(World world)
    {
        var e = world.CreateEntity();
        world.Components<GlobalPostProcessSource>().GetOrAdd(e) = new GlobalPostProcessSource
        {
            Active = true,
            Priority = 100,
            Settings = new GlobalPostProcessSettings
            {
                BloomEnabled = true,
                BloomRadius = 1f,
                BloomGain = 0.18f,
                BloomExtractThreshold = 0.35f,
                BloomExtractKnee = 0.5f,
                EmissiveToHdrGain = 0.42f,
                EmissiveToBloomGain = 0.38f,
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
