using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Promotes bright emissive sprites into synthetic <see cref="PointLight"/> entries each frame, so that any sprite with
/// <see cref="SpriteDrawRequest.EmissiveIntensity"/> above a threshold automatically illuminates and casts soft shadows
/// without an explicit <see cref="Scene.PointLightSource"/> in the scene.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate space.</b> Inputs are <see cref="SpriteDrawRequest"/> in <b>world</b> (+Y up). Output is
/// <see cref="PointLight"/> with <b>world</b> position and <b>world</b> radius. No conversion to swapchain happens here —
/// that occurs in the renderer's per-frame light SSBO upload, the single canonical conversion site.
/// </para>
/// <para>
/// <b>Performance.</b> One serial pass over the sprite array, O(N). Allocation-free: writes into a caller-provided span
/// and returns the count produced. Promotion is intentionally serial because the output is bounded by
/// <see cref="DeferredRenderingConstants.MaxPromotedLightsCap"/> and input counts are typically hundreds to low thousands;
/// the overhead of chunked parallelism and per-chunk heap allocations outweighs any threading gain at these sizes.
/// </para>
/// <para>
/// Does not promote sprites with <see cref="Scene.CoordinateSpace.ViewportSpace"/> (HUD/overlay) — those should not
/// illuminate gameplay.
/// </para>
/// <para>Pure function; safe to invoke from <see cref="Core.Ecs.IParallelSystem"/> workers.</para>
/// </remarks>
internal static class EmissiveLightPromotionCpu
{
    /// <summary>
    /// Scans <paramref name="sprites"/> and writes promoted <see cref="PointLight"/> entries into <paramref name="dst"/>.
    /// Returns the number of lights produced (≤ <paramref name="dst"/>.Length and ≤ <paramref name="maxPromoted"/>).
    /// </summary>
    /// <param name="sprites">Sprite submission scratch.</param>
    /// <param name="spriteCount">Valid sprite count (<paramref name="sprites"/> may be longer).</param>
    /// <param name="emissiveLightThreshold">Sprites with <c>EmissiveIntensity</c> ≥ this become lights.</param>
    /// <param name="promotionRadiusGain">Multiplier on the sprite's world diagonal to derive light radius.</param>
    /// <param name="promotionIntensityGain">Multiplier on <c>EmissiveIntensity</c> to derive light intensity.</param>
    /// <param name="maxPromoted">Hard cap (overflow truncated deterministically; sprites scanned in submit order).</param>
    /// <param name="dst">Output span; promoted lights default to <c>CastsShadow = true</c>. The caller
    /// (<c>FramePlanBuilder</c>) clears this flag when global shadows are disabled so promoted lights still
    /// illuminate but do not trace the SDF.</param>
    public static int Promote(
        SpriteDrawRequest[] sprites,
        int spriteCount,
        float emissiveLightThreshold,
        float promotionRadiusGain,
        float promotionIntensityGain,
        int maxPromoted,
        Span<PointLight> dst)
        => Promote(sprites, spriteCount, ReadOnlySpan<int>.Empty, emissiveLightThreshold,
            promotionRadiusGain, promotionIntensityGain, maxPromoted, dst);

    /// <summary>
    /// Overload that iterates sprites via <paramref name="sortIndices"/> so truncation order matches layer/sort
    /// priority. When <paramref name="sortIndices"/> is empty, sprites are scanned sequentially.
    /// </summary>
    public static int Promote(
        SpriteDrawRequest[] sprites,
        int spriteCount,
        ReadOnlySpan<int> sortIndices,
        float emissiveLightThreshold,
        float promotionRadiusGain,
        float promotionIntensityGain,
        int maxPromoted,
        Span<PointLight> dst)
    {
        if (sprites is null || spriteCount <= 0 || dst.Length == 0 || maxPromoted <= 0)
            return 0;
        if (emissiveLightThreshold <= 0f)
            return 0;

        var useSortIndices = sortIndices.Length >= spriteCount;
        var cap = System.Math.Min(maxPromoted, dst.Length);
        var written = 0;
        for (var i = 0; i < spriteCount && written < cap; i++)
        {
            var idx = useSortIndices ? sortIndices[i] : i;
            ref readonly var sprite = ref sprites[idx];
            if (sprite.Space != Scene.CoordinateSpace.WorldSpace)
                continue;
            if (sprite.EmissiveIntensity < emissiveLightThreshold)
                continue;

            var tintR = sprite.EmissiveTint.X;
            var tintG = sprite.EmissiveTint.Y;
            var tintB = sprite.EmissiveTint.Z;
            if (tintR <= 0f && tintG <= 0f && tintB <= 0f)
                continue;

            var diagWorld = MathF.Sqrt(
                sprite.HalfExtentsWorld.X * sprite.HalfExtentsWorld.X +
                sprite.HalfExtentsWorld.Y * sprite.HalfExtentsWorld.Y);
            var radiusWorld = MathF.Max(diagWorld * promotionRadiusGain, DeferredRenderingConstants.MinPromotedLightRadiusWorld);

            dst[written++] = new PointLight
            {
                PositionWorld = sprite.CenterWorld,
                Radius = radiusWorld,
                Color = new Vector3D<float>(tintR, tintG, tintB),
                Intensity = sprite.EmissiveIntensity * promotionIntensityGain,
                FalloffExponent = 2f,
                CastsShadow = true,
            };
        }

        return written;
    }
}
