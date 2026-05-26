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
        if (!float.IsFinite(lenSq) || lenSq <= 1e-8f)
        {
            // Defensively normalize the fallback to avoid non-unit results from caller-supplied vectors.
            var fbLenSq = fallbackDirection.X * fallbackDirection.X + fallbackDirection.Y * fallbackDirection.Y;
            if (!float.IsFinite(fbLenSq) || fbLenSq <= 1e-8f)
                return new Vector2D<float>(0f, -1f);
            var fbInvLen = 1f / MathF.Sqrt(fbLenSq);
            return new Vector2D<float>(fallbackDirection.X * fbInvLen, fallbackDirection.Y * fbInvLen);
        }

        var invLen = 1f / MathF.Sqrt(lenSq);
        return new Vector2D<float>(dx * invLen, dy * invLen);
    }
}
