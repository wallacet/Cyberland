using Silk.NET.Maths;

namespace Cyberland.Demo.IdleGold;

/// <summary>Tints for equipment strip icons (debug-friendly palette).</summary>
public static class TierVisual
{
    public static Vector4D<float> Stripe(int tierIndex)
    {
        tierIndex = Math.Clamp(tierIndex, 0, GameBalance.TierCount - 1);
        ReadOnlySpan<Vector4D<float>> palette =
        [
            new(0.55f, 0.38f, 0.22f, 1f),
            new(0.55f, 0.55f, 0.58f, 1f),
            new(0.78f, 0.48f, 0.28f, 1f),
            new(0.72f, 0.52f, 0.32f, 1f),
            new(0.45f, 0.48f, 0.52f, 1f),
            new(0.62f, 0.65f, 0.7f, 1f),
            new(0.82f, 0.84f, 0.9f, 1f),
            new(0.55f, 0.72f, 0.95f, 1f)
        ];
        return palette[tierIndex];
    }
}
