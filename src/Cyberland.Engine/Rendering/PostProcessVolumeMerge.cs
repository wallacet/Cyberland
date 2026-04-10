using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Merges global post settings with overlapping volumes (higher <see cref="PostProcessVolume.Priority"/> wins per field).
/// </summary>
public static class PostProcessVolumeMerge
{
    public static GlobalPostProcessSettings Resolve(
        in GlobalPostProcessSettings global,
        ReadOnlySpan<PostProcessVolume> volumes,
        Vector2D<float> viewMinWorld,
        Vector2D<float> viewMaxWorld)
    {
        var bestPri = int.MinValue;
        var bestIdx = -1;

        for (var i = 0; i < volumes.Length; i++)
        {
            ref readonly var v = ref volumes[i];
            if (!Overlaps(v.MinWorld, v.MaxWorld, viewMinWorld, viewMaxWorld))
                continue;

            if (v.Priority < bestPri)
                continue;

            bestPri = v.Priority;
            bestIdx = i;
        }

        if (bestIdx < 0)
            return global;

        var result = global;
        ApplyOverrides(ref result, volumes[bestIdx].Overrides);
        return result;
    }

    public static bool Overlaps(
        Vector2D<float> aMin,
        Vector2D<float> aMax,
        Vector2D<float> bMin,
        Vector2D<float> bMax) =>
        aMin.X < bMax.X && aMax.X > bMin.X && aMin.Y < bMax.Y && aMax.Y > bMin.Y;

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
