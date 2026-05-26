using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Merges global post settings with the submitted post volume whose oriented box contains the active camera's
/// world position. Higher <see cref="PostProcessVolume.Priority"/> wins on overlap; ties keep the first submitted
/// volume (stable first-wins).
/// </summary>
/// <remarks>
/// Volumes only apply when the camera's world position is inside them, per design goal: "post volumes only apply
/// to cameras when the camera's transform position is inside the volume." Global post settings always apply;
/// an overlapping volume multiplies its override fields onto the global result.
/// </remarks>
public static class PostProcessVolumeMerge
{
    /// <summary>
    /// Picks the highest-priority <see cref="PostProcessVolume"/> that contains <paramref name="cameraPositionWorld"/>
    /// and merges its <see cref="PostProcessVolume.Overrides"/> into <paramref name="global"/>.
    /// </summary>
    /// <param name="global">Baseline settings from <see cref="IRenderer.SetGlobalPostProcess"/>.</param>
    /// <param name="volumes">Volumes submitted this frame.</param>
    /// <param name="cameraPositionWorld">Active camera world position (+Y up).</param>
    internal static GlobalPostProcessSettings ResolveAtPoint(
        in GlobalPostProcessSettings global,
        ReadOnlySpan<PostProcessVolumeSubmission> volumes,
        Vector2D<float> cameraPositionWorld)
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
            if (!ContainsPoint(
                    submitted.WorldPosition,
                    halfExtentsWorld,
                    submitted.WorldRotationRadians,
                    cameraPositionWorld))
                continue;

            if (bestIdx >= 0 && v.Priority <= bestPri)
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
    /// Whether an oriented rectangle (center, half extents, CCW rotation) contains <paramref name="pointWorld"/>
    /// in world space. Negative or zero half extents are treated as empty boxes (never contain).
    /// </summary>
    public static bool ContainsPoint(
        Vector2D<float> centerWorld,
        Vector2D<float> halfExtentsWorld,
        float rotationRadians,
        Vector2D<float> pointWorld)
    {
        if (halfExtentsWorld.X <= 0f || halfExtentsWorld.Y <= 0f)
            return false;

        // Express the point in the box's local frame: translate by -center, rotate by -rotation.
        var dx = pointWorld.X - centerWorld.X;
        var dy = pointWorld.Y - centerWorld.Y;
        var c = MathF.Cos(-rotationRadians);
        var s = MathF.Sin(-rotationRadians);
        var lx = dx * c - dy * s;
        var ly = dx * s + dy * c;
        return MathF.Abs(lx) <= halfExtentsWorld.X && MathF.Abs(ly) <= halfExtentsWorld.Y;
    }

    private static void ApplyOverrides(ref GlobalPostProcessSettings g, PostProcessOverrides o)
    {
        if (o.HasBloomRadius)
            g.BloomRadius *= o.BloomRadius;
        if (o.HasBloomGain)
            g.BloomGain *= o.BloomGain;
        if (o.HasEmissiveToHdrGain)
            g.EmissiveToHdrGain *= o.EmissiveToHdrGain;
        if (o.HasBloomSourceGain)
            g.BloomSourceGain *= o.BloomSourceGain;
        if (o.HasExposure)
            g.Exposure *= o.Exposure;
        if (o.HasSaturation)
            g.Saturation *= o.Saturation;
        if (o.HasShadows)
            g.Shadows = o.Shadows;
        if (o.HasTonemapEnabled)
            g.TonemapEnabled = o.TonemapEnabled;
        if (o.HasColorGradingShadows)
        {
            g.ColorGradingShadows = new Vector3D<float>(
                g.ColorGradingShadows.X * o.ColorGradingShadows.X,
                g.ColorGradingShadows.Y * o.ColorGradingShadows.Y,
                g.ColorGradingShadows.Z * o.ColorGradingShadows.Z);
        }
        if (o.HasColorGradingMidtones)
        {
            g.ColorGradingMidtones = new Vector3D<float>(
                g.ColorGradingMidtones.X * o.ColorGradingMidtones.X,
                g.ColorGradingMidtones.Y * o.ColorGradingMidtones.Y,
                g.ColorGradingMidtones.Z * o.ColorGradingMidtones.Z);
        }
        if (o.HasColorGradingHighlights)
        {
            g.ColorGradingHighlights = new Vector3D<float>(
                g.ColorGradingHighlights.X * o.ColorGradingHighlights.X,
                g.ColorGradingHighlights.Y * o.ColorGradingHighlights.Y,
                g.ColorGradingHighlights.Z * o.ColorGradingHighlights.Z);
        }
        if (o.HasBloomEnabled)
            g.BloomEnabled = o.BloomEnabled;
        if (o.HasBloomExtractThreshold)
            g.BloomExtractThreshold *= o.BloomExtractThreshold;
        if (o.HasBloomExtractKnee)
            g.BloomExtractKnee *= o.BloomExtractKnee;
    }
}
