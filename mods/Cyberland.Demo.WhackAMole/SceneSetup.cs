using Cyberland.Demo.WhackAMole.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.WhackAMole;

/// <summary>Cold-start entities for the Whack-a-Mole sample scene.</summary>
public static class SceneSetup
{
    public readonly record struct SceneRefs(
        EntityId Background,
        EntityId Target,
        EntityId TargetFillLight,
        EntityId ScoreText,
        EntityId TimerText,
        EntityId OverlayText);

    private const int ViewportWidth = 1280;
    private const int ViewportHeight = 720;
    private static readonly Vector2D<float> TargetHalfExtents = new(32f, 32f);

    public static ValueTask<SceneRefs> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var world = context.World;
        var renderer = context.Host.Renderer;

        var session = world.CreateEntity();
        world.GetOrAdd<WhackAMoleState>(session) = new WhackAMoleState
        {
            Phase = WhackAMolePhase.Ready,
            Score = 0,
            TimeRemainingSeconds = 60f,
            TimerStarted = false
        };

        var camera = world.CreateEntity();
        var cameraTransform = Transform.Identity;
        cameraTransform.LocalPosition = new Vector2D<float>(ViewportWidth * 0.5f, ViewportHeight * 0.5f);
        world.GetOrAdd<Transform>(camera) = cameraTransform;
        ref var camera2D = ref world.GetOrAdd<Camera2D>(camera);
        camera2D = Camera2D.Create(new Vector2D<int>(ViewportWidth, ViewportHeight));
        camera2D.Priority = 250;

        // Dark fullscreen plate behind gameplay so the mole's point light reads as a visible falloff on albedo.
        var background = world.CreateEntity();
        var backgroundTransform = Transform.Identity;
        backgroundTransform.LocalPosition = new Vector2D<float>(ViewportWidth * 0.5f, ViewportHeight * 0.5f);
        world.GetOrAdd<Transform>(background) = backgroundTransform;
        ref var backgroundSprite = ref world.GetOrAdd<Sprite>(background);
        backgroundSprite = Sprite.DefaultWhiteUnlit(
            renderer.WhiteTextureId,
            renderer.DefaultNormalTextureId,
            new Vector2D<float>(ViewportWidth * 0.5f, ViewportHeight * 0.5f));
        backgroundSprite.Space = CoordinateSpace.ViewportSpace;
        backgroundSprite.Layer = (int)SpriteLayer.Background;
        backgroundSprite.SortKey = 0f;
        backgroundSprite.ColorMultiply = new Vector4D<float>(0.07f, 0.075f, 0.1f, 1f);

        // Opaque viewport sprites still go through the deferred G-buffer; baseline lighting keeps albedo visible.
        var ambient = world.CreateEntity();
        world.GetOrAdd<AmbientLightSource>(ambient) = new AmbientLightSource
        {
            Active = true,
            Color = new Vector3D<float>(0.45f, 0.48f, 0.55f),
            Intensity = 0.55f
        };

        var target = world.CreateEntity();
        world.GetOrAdd<WhackAMoleTargetTag>(target);
        var targetTransform = Transform.Identity;
        targetTransform.LocalPosition = new Vector2D<float>(ViewportWidth * 0.5f, ViewportHeight * 0.5f);
        world.GetOrAdd<Transform>(target) = targetTransform;
        ref var targetSprite = ref world.GetOrAdd<Sprite>(target);
        targetSprite = Sprite.DefaultWhiteUnlit(renderer.WhiteTextureId, renderer.DefaultNormalTextureId, TargetHalfExtents);
        targetSprite.Space = CoordinateSpace.ViewportSpace;
        targetSprite.Layer = (int)SpriteLayer.World;
        targetSprite.ColorMultiply = new Vector4D<float>(1f, 0.3f, 0.3f, 1f);
        targetSprite.Visible = true;

        // Point light at local origin under the target — hierarchy supplies world position; game code only updates the parent.
        var targetFill = world.CreateEntity();
        var fillTransform = Transform.Identity;
        fillTransform.Parent = target;
        world.GetOrAdd<Transform>(targetFill) = fillTransform;
        world.GetOrAdd<PointLightSource>(targetFill) = new PointLightSource
        {
            Active = true,
            Radius = 220f,
            Color = new Vector3D<float>(targetSprite.ColorMultiply.X, targetSprite.ColorMultiply.Y, targetSprite.ColorMultiply.Z),
            Intensity = 1.1f,
            FalloffExponent = 2f,
            CastsShadow = false
        };

        var scoreText = CreateTextEntity(world, new TextStyle(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 1f, 1f, 1f)));
        world.GetOrAdd<WhackAMoleScoreTextTag>(scoreText);
        var timerText = CreateTextEntity(world, new TextStyle(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 1f, 1f, 1f)));
        world.GetOrAdd<WhackAMoleTimerTextTag>(timerText);
        var overlayText = CreateTextEntity(world, new TextStyle(BuiltinFonts.UiSans, 24f, new Vector4D<float>(1f, 0.9f, 0.7f, 1f)));
        world.GetOrAdd<WhackAMoleOverlayTextTag>(overlayText);

        return ValueTask.FromResult(new SceneRefs(background, target, targetFill, scoreText, timerText, overlayText));
    }

    private static EntityId CreateTextEntity(World world, TextStyle style)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        ref var text = ref world.GetOrAdd<BitmapText>(entity);
        text.Visible = true;
        text.Content = string.Empty;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
        text.IsLocalizationKey = false;
        text.SortKey = 400f;
        text.Style = style;
        return entity;
    }
}
