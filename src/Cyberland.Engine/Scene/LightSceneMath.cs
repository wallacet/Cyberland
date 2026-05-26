using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>Shared 2D lighting direction and scale math for <see cref="Systems"/> light submitters.</summary>
internal static class LightSceneMath
{
    public static Vector2D<float> DirectionFromWorldRotation(float radians)
    {
        if (!float.IsFinite(radians))
            return new Vector2D<float>(1f, 0f);
        return new(MathF.Cos(radians), MathF.Sin(radians));
    }

    /// <summary>
    /// Returns <c>max(|scaleX|, |scaleY|)</c> — the radius scale factor for point and spot lights.
    /// Uniform scale on light entities is recommended; with non-uniform scale the radius uses the larger axis.
    /// </summary>
    public static float MaxAbsScale(in Vector2D<float> scale)
    {
        var ax = MathF.Abs(scale.X);
        var ay = MathF.Abs(scale.Y);
        var m = MathF.Max(ax, ay);
        return float.IsFinite(m) ? m : 1f;
    }

    /// <summary>
    /// True when <paramref name="entity"/>'s transform root owns a <see cref="Sprite"/> in viewport-locked space
    /// (<see cref="CoordinateSpace.ViewportSpace"/> or <see cref="CoordinateSpace.PresentationViewportSpace"/>): composed
    /// translations are virtual canvas pixels (+Y down), not world units (+Y up).
    /// </summary>
    public static bool RootTransformUsesViewportSpriteSpace(World world, EntityId entity) =>
        TryGetViewportLockedRootSpriteSpace(world, entity, out _);

    private static bool TryGetViewportLockedRootSpriteSpace(World world, EntityId entity, out CoordinateSpace rootSpriteSpace)
    {
        var id = entity;
        while (true)
        {
            ref readonly var tf = ref world.Get<Transform>(id);
            var p = tf.Parent;
            if (p.Raw == 0 || !world.IsAlive(p))
                break;
            id = p;
        }

        if (!world.Has<Sprite>(id))
        {
            rootSpriteSpace = default;
            return false;
        }

        ref readonly var spr = ref world.Get<Sprite>(id);
        if (spr.Space is CoordinateSpace.ViewportSpace or CoordinateSpace.PresentationViewportSpace)
        {
            rootSpriteSpace = spr.Space;
            return true;
        }

        rootSpriteSpace = default;
        return false;
    }

    /// <summary>
    /// Maps <paramref name="transformTranslation"/> from <see cref="Transform.WorldMatrix"/> into the world-space
    /// position expected by <see cref="IRenderer.SubmitPointLight"/> / <see cref="IRenderer.SubmitSpotLight"/> when the
    /// light lives under a viewport-authored sprite subtree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>camera.Valid</c> is <c>false</c> (early startup frames or post-world-swap before camera state updates),
    /// viewport-pixel translations pass through as-is. Viewport-locked lights may appear at wrong positions for up to
    /// one frame. This is a known race; callers accept the transient artifact rather than deferring light submission.
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> reads only — component stores and parent transforms are accessed read-only. Safe to call
    /// from <see cref="Core.Ecs.IParallelSystem"/> workers during the late phase, which forbids structural mutation.
    /// Do not call from phases that add/remove entities or components.
    /// </para>
    /// </remarks>
    public static Vector2D<float> ResolveLightPositionWorldForSubmit(
        World world,
        EntityId lightEntity,
        Vector2D<float> transformTranslation,
        in CameraRuntimeState camera)
    {
        if (!TryGetViewportLockedRootSpriteSpace(world, lightEntity, out var rootSpace))
            return transformTranslation;
        if (!camera.Valid)
            return transformTranslation;
        var vpDims = rootSpace == CoordinateSpace.PresentationViewportSpace
            ? CameraPresentationLayout.ResolvePresentationViewportSize(camera)
            : camera.ViewportSizeWorld;
        var viewportSize = new Vector2D<float>(vpDims.X, vpDims.Y);
        return CameraProjection.ViewportPixelToWorld(
            transformTranslation,
            camera.PositionWorld,
            camera.RotationRadians,
            viewportSize);
    }
}
