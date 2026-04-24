using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Merges global post settings with overlapping volumes (higher <see cref="PostProcessVolume.Priority"/> wins per field).
/// </summary>
public static class PostProcessVolumeMerge
{
    /// <summary>
    /// Picks the highest-priority <see cref="PostProcessVolume"/> overlapping the view rectangle and merges its <see cref="PostProcessVolume.Overrides"/> into <paramref name="global"/>.
    /// </summary>
    /// <param name="global">Baseline settings from <see cref="IRenderer.SetGlobalPostProcess"/>.</param>
    /// <param name="volumes">Volumes submitted this frame.</param>
    /// <param name="viewMinWorld">Axis-aligned min corner of the evaluated region in world space (+Y up).</param>
    /// <param name="viewMaxWorld">Axis-aligned max corner.</param>
    internal static GlobalPostProcessSettings Resolve(
        in GlobalPostProcessSettings global,
        ReadOnlySpan<PostProcessVolumeSubmission> volumes,
        Vector2D<float> viewMinWorld,
        Vector2D<float> viewMaxWorld)
    {
        var bestPri = int.MinValue;
        var bestIdx = -1;

        for (var i = 0; i < volumes.Length; i++)
        {
            ref readonly var submitted = ref volumes[i];
            ref readonly var v = ref submitted.Volume;
            var halfExtentsWorld = new Vector2D<float>(
                v.HalfExtentsLocal.X * submitted.WorldScale.X,
                v.HalfExtentsLocal.Y * submitted.WorldScale.Y);
            if (!OrientedRectOverlapsAxisAlignedRect(
                    submitted.WorldPosition,
                    halfExtentsWorld,
                    submitted.WorldRotationRadians,
                    viewMinWorld,
                    viewMaxWorld))
                continue;

            if (v.Priority < bestPri)
                continue;

            bestPri = v.Priority;
            bestIdx = i;
        }

        if (bestIdx < 0)
            return global;

        var result = global;
        ApplyOverrides(ref result, volumes[bestIdx].Volume.Overrides);
        return result;
    }

    /// <summary>Axis-aligned box overlap test in world space (+Y up).</summary>
    public static bool Overlaps(
        Vector2D<float> aMin,
        Vector2D<float> aMax,
        Vector2D<float> bMin,
        Vector2D<float> bMax) =>
        aMin.X < bMax.X && aMax.X > bMin.X && aMin.Y < bMax.Y && aMax.Y > bMin.Y;

    /// <summary>
    /// Whether an oriented rectangle (center, half extents, CCW rotation) intersects an axis-aligned rectangle in world space.
    /// </summary>
    public static bool OrientedRectOverlapsAxisAlignedRect(
        Vector2D<float> centerWorld,
        Vector2D<float> halfExtentsWorld,
        float rotationRadians,
        Vector2D<float> rectMinWorld,
        Vector2D<float> rectMaxWorld)
    {
        if (halfExtentsWorld.X <= 0f || halfExtentsWorld.Y <= 0f)
            return false;

        var c = MathF.Cos(rotationRadians);
        var s = MathF.Sin(rotationRadians);
        var ux = new Vector2D<float>(c, s);
        var uy = new Vector2D<float>(-s, c);

        Span<Vector2D<float>> axes = stackalloc Vector2D<float>[4];
        axes[0] = new Vector2D<float>(1f, 0f);
        axes[1] = new Vector2D<float>(0f, 1f);
        axes[2] = ux;
        axes[3] = uy;

        for (var ai = 0; ai < 4; ai++)
        {
            var axis = axes[ai];
            var ax = axis.X;
            var ay = axis.Y;
            var lenSq = ax * ax + ay * ay;
            var invLen = 1f / MathF.Sqrt(lenSq);
            var nx = ax * invLen;
            var ny = ay * invLen;

            var obbCenterProj = centerWorld.X * nx + centerWorld.Y * ny;
            var obbHalf =
                halfExtentsWorld.X * MathF.Abs(ux.X * nx + ux.Y * ny) +
                halfExtentsWorld.Y * MathF.Abs(uy.X * nx + uy.Y * ny);

            ProjectAxisAlignedRectOntoAxis(rectMinWorld, rectMaxWorld, nx, ny, out var rectMinProj, out var rectMaxProj);

            if (obbCenterProj + obbHalf <= rectMinProj || obbCenterProj - obbHalf >= rectMaxProj)
                return false;
        }

        return true;
    }

    private static void ProjectAxisAlignedRectOntoAxis(
        Vector2D<float> rectMin,
        Vector2D<float> rectMax,
        float nx,
        float ny,
        out float minProj,
        out float maxProj)
    {
        var p0 = rectMin.X * nx + rectMin.Y * ny;
        var p1 = rectMax.X * nx + rectMin.Y * ny;
        var p2 = rectMax.X * nx + rectMax.Y * ny;
        var p3 = rectMin.X * nx + rectMax.Y * ny;
        minProj = MathF.Min(MathF.Min(p0, p1), MathF.Min(p2, p3));
        maxProj = MathF.Max(MathF.Max(p0, p1), MathF.Max(p2, p3));
    }

    private static void ApplyOverrides(ref GlobalPostProcessSettings g, PostProcessOverrides o)
    {
        if (o.HasBloomRadius)
            g.BloomRadius *= o.BloomRadius;
        if (o.HasBloomGain)
            g.BloomGain *= o.BloomGain;
        if (o.HasEmissiveToHdrGain)
            g.EmissiveToHdrGain *= o.EmissiveToHdrGain;
        if (o.HasEmissiveToBloomGain)
            g.EmissiveToBloomGain *= o.EmissiveToBloomGain;
        if (o.HasExposure)
            g.Exposure *= o.Exposure;
        if (o.HasSaturation)
            g.Saturation *= o.Saturation;
    }
}
