using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Shared helpers for authored 2D light rigs (demo and gameplay systems).
/// Keeps common direction/aim math consistent across mods.
/// </summary>
public static class LightRigMath
{
    /// <summary>
    /// Returns a normalized vector from <paramref name="from"/> toward <paramref name="to"/>.
    /// Uses <paramref name="fallbackDirection"/> if the points are nearly identical.
    /// </summary>
    public static Vector2D<float> DirectionToOrFallback(
        in Vector2D<float> from,
        in Vector2D<float> to,
        in Vector2D<float> fallbackDirection)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq <= 1e-8f)
            return fallbackDirection;

        var invLen = 1f / MathF.Sqrt(lenSq);
        return new Vector2D<float>(dx * invLen, dy * invLen);
    }
}
