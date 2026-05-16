using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo.Rts;

/// <summary>Cold start: playfield, camera, lights, unit + child point light, selection frame sprites, session row, FPS HUD.</summary>
public static class SceneSetup
{
    public const int ViewportWidth = 1280;
    public const int ViewportHeight = 720;

    public static async ValueTask SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var renderer = context.Host.Renderer ?? throw new InvalidOperationException("Renderer required for RTS demo.");
        var world = context.World;

        var bgTex = BuildCheckerboardTexture();
        var bgTextureId = renderer.RegisterTextureRgba(bgTex, 64, 64);
        if (bgTextureId == TextureId.MaxValue)
            throw new InvalidOperationException("RTS demo failed to register background texture.");

        var cameraEntity = world.CreateEntity();
        world.GetOrAdd<RtsCameraTag>(cameraEntity);
        var camXf = Transform.Identity;
        camXf.WorldPosition = new Vector2D<float>(RtsConstants.PlaySize * 0.5f, RtsConstants.PlaySize * 0.5f);
        world.GetOrAdd<Transform>(cameraEntity) = camXf;
        var cam = Camera2D.Create(new Vector2D<int>(ViewportWidth, ViewportHeight));
        cam.PresentationViewportSizeWorld = new Vector2D<int>(ViewportWidth, ViewportHeight);
        world.GetOrAdd<Camera2D>(cameraEntity) = cam;
        world.GetOrAdd<RtsCameraZoomState>(cameraEntity) = new RtsCameraZoomState
        {
            TargetViewportWidth = ViewportWidth,
            TargetViewportHeight = ViewportHeight
        };

        var backgroundEntity = world.CreateEntity();
        var bgXf = Transform.Identity;
        bgXf.WorldPosition = new Vector2D<float>(RtsConstants.PlaySize * 0.5f, RtsConstants.PlaySize * 0.5f);
        world.GetOrAdd<Transform>(backgroundEntity) = bgXf;

        var ambientEntity = world.CreateEntity();
        world.GetOrAdd<AmbientLightSource>(ambientEntity) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.45f, 0.5f, 0.58f),
            Intensity = 0.55f
        };

        var unitEntity = world.CreateEntity();
        world.GetOrAdd<RtsUnitTag>(unitEntity);
        var unitXf = Transform.Identity;
        unitXf.WorldPosition = new Vector2D<float>(800f, 900f);
        world.GetOrAdd<Transform>(unitEntity) = unitXf;

        ConfigurePlayfieldSprites(world, renderer, backgroundEntity, bgTextureId, unitEntity);

        var lightEntity = world.CreateEntity();
        var lightXf = Transform.Identity;
        lightXf.Parent = unitEntity;
        lightXf.LocalPosition = new Vector2D<float>(6f, 14f);
        world.GetOrAdd<Transform>(lightEntity) = lightXf;
        world.GetOrAdd<PointLightSource>(lightEntity) = new PointLightSource
        {
            Active = true,
            Radius = 420f,
            Color = new Vector3D<float>(1f, 0.92f, 0.72f),
            Intensity = 0.48f,
            FalloffExponent = 2.1f,
            CastsShadow = false
        };

        var bar0 = CreateSelectionBar(world, renderer);
        var bar1 = CreateSelectionBar(world, renderer);
        var bar2 = CreateSelectionBar(world, renderer);
        var bar3 = CreateSelectionBar(world, renderer);

        var sessionEntity = world.CreateEntity();
        world.GetOrAdd<RtsSessionState>(sessionEntity) = new RtsSessionState
        {
            CameraEntity = cameraEntity,
            UnitEntity = unitEntity,
            SelectionBar0 = bar0,
            SelectionBar1 = bar1,
            SelectionBar2 = bar2,
            SelectionBar3 = bar3,
            UnitSelected = false,
            HasMoveTarget = false,
            MoveTargetWorld = default
        };

        SpawnHudFps(world);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static void SpawnHudFps(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<RtsHudFpsTag>(entity);
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var text = ref world.GetOrAdd<BitmapText>(entity);
        text.Visible = true;
        text.IsLocalizationKey = false;
        text.Content = "FPS —";
        text.Style = new TextStyle(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f));
        text.SortKey = 452f;
        text.CoordinateSpace = BitmapText.HudDefaultCoordinateSpace;
    }

    /// <summary>
    /// Sprite fields must be written through <c>ref</c> into the ECS store. This lives in a non-async helper because
    /// C# disallows <c>ref</c> locals inside <c>async</c> methods (the RTS setup method stays <c>async</c> for future I/O).
    /// </summary>
    private static void ConfigurePlayfieldSprites(
        World world,
        IRenderer renderer,
        EntityId backgroundEntity,
        TextureId bgTextureId,
        EntityId unitEntity)
    {
        ref var bgSpr = ref world.GetOrAdd<Sprite>(backgroundEntity);
        bgSpr.Visible = true;
        bgSpr.Transparent = false;
        bgSpr.AlbedoTextureId = bgTextureId;
        bgSpr.NormalTextureId = renderer.DefaultNormalTextureId;
        bgSpr.HalfExtents = new Vector2D<float>(RtsConstants.PlaySize * 0.5f, RtsConstants.PlaySize * 0.5f);
        bgSpr.ColorMultiply = new Vector4D<float>(0.35f, 0.38f, 0.48f, 1f);
        bgSpr.Layer = (int)SpriteLayer.World;
        bgSpr.SortKey = -500f;

        ref var unitSpr = ref world.GetOrAdd<Sprite>(unitEntity);
        unitSpr.Visible = true;
        unitSpr.Transparent = false;
        unitSpr.AlbedoTextureId = renderer.WhiteTextureId;
        unitSpr.NormalTextureId = renderer.DefaultNormalTextureId;
        unitSpr.HalfExtents = RtsConstants.UnitHalfExtents;
        unitSpr.ColorMultiply = new Vector4D<float>(0.85f, 0.82f, 0.35f, 1f);
        unitSpr.Layer = (int)SpriteLayer.World;
        unitSpr.SortKey = 10f;
    }

    private static EntityId CreateSelectionBar(World world, IRenderer renderer)
    {
        var e = world.CreateEntity();
        var xf = Transform.Identity;
        xf.WorldPosition = default;
        world.GetOrAdd<Transform>(e) = xf;
        ref var spr = ref world.GetOrAdd<Sprite>(e);
        spr.Visible = false;
        spr.Transparent = true;
        spr.AlbedoTextureId = renderer.WhiteTextureId;
        spr.NormalTextureId = renderer.DefaultNormalTextureId;
        spr.HalfExtents = new Vector2D<float>(2f, 2f);
        spr.ColorMultiply = new Vector4D<float>(0.2f, 1f, 0.35f, 0.95f);
        spr.Layer = (int)SpriteLayer.World;
        spr.SortKey = 200f;
        return e;
    }

    private static byte[] BuildCheckerboardTexture()
    {
        const int w = 64;
        const int h = 64;
        var rgba = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var cell = ((x >> 3) + (y >> 3)) & 1;
                var i = (y * w + x) * 4;
                var v = cell == 0 ? (byte)56 : (byte)88;
                rgba[i] = v;
                rgba[i + 1] = (byte)(v + 6);
                rgba[i + 2] = (byte)(v + 12);
                rgba[i + 3] = 255;
            }
        }

        return rgba;
    }
}
