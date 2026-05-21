using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts;

/// <summary>Formation slot assignment for move orders (centered grid around the click anchor).</summary>
public static class RtsFormation
{
    /// <summary>
    /// Writes each unit's <see cref="RtsUnitState.MoveTargetWorld"/> to a stable row-major grid slot centered on
    /// <paramref name="anchor"/>.
    /// </summary>
    public static void AssignGridTargets(Vector2D<float> anchor, ReadOnlySpan<EntityId> units, World world)
    {
        if (units.Length == 0)
            return;

        var sorted = new EntityId[units.Length];
        units.CopyTo(sorted);
        Array.Sort(sorted, static (a, b) => a.Raw.CompareTo(b.Raw));

        var count = sorted.Length;
        var cols = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(count)));
        var rows = (count + cols - 1) / cols;
        var spacing = RtsConstants.FormationSpacing;
        var gridW = (cols - 1) * spacing;
        var gridH = (rows - 1) * spacing;
        var originX = anchor.X - gridW * 0.5f;
        var originY = anchor.Y - gridH * 0.5f;

        for (var i = 0; i < count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var target = new Vector2D<float>(originX + col * spacing, originY + row * spacing);
            ref var state = ref world.Get<RtsUnitState>(sorted[i]);
            state.MoveTargetWorld = ClampToPlayfield(target);
        }
    }

    private static Vector2D<float> ClampToPlayfield(Vector2D<float> p)
    {
        var s = RtsConstants.PlaySize;
        return new Vector2D<float>(Math.Clamp(p.X, 0f, s), Math.Clamp(p.Y, 0f, s));
    }
}
