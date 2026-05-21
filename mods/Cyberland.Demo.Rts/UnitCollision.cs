using Silk.NET.Maths;

namespace Cyberland.Demo.Rts;

/// <summary>Lightweight circle separation so units do not overlap (demo-only, no engine physics).</summary>
public static class UnitCollision
{
    /// <summary>
    /// Pushes overlapping circle centers apart symmetrically for <paramref name="iterations"/> passes.
    /// Positions are updated in place; caller clamps to playfield afterward.
    /// </summary>
    public static void ResolveSeparation(Span<Vector2D<float>> positions, float radius, int iterations)
    {
        if (positions.Length < 2 || iterations <= 0 || radius <= 0f)
            return;

        var minDist = radius * 2f;
        var minDistSq = minDist * minDist;

        for (var iter = 0; iter < iterations; iter++)
        {
            for (var i = 0; i < positions.Length; i++)
            {
                for (var j = i + 1; j < positions.Length; j++)
                {
                    ref var a = ref positions[i];
                    ref var b = ref positions[j];
                    var dx = b.X - a.X;
                    var dy = b.Y - a.Y;
                    var distSq = dx * dx + dy * dy;
                    if (distSq >= minDistSq)
                        continue;

                    if (distSq < 1e-8f)
                    {
                        // Coincident centers: nudge along a stable axis.
                        dx = 1f;
                        dy = 0f;
                        distSq = 1f;
                    }

                    var dist = MathF.Sqrt(distSq);
                    var overlap = (minDist - dist) * 0.5f;
                    var nx = dx / dist;
                    var ny = dy / dist;
                    a = new Vector2D<float>(a.X - nx * overlap, a.Y - ny * overlap);
                    b = new Vector2D<float>(b.X + nx * overlap, b.Y + ny * overlap);
                }
            }
        }
    }
}
