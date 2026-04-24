using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>Shared 2D lighting direction and scale math for <see cref="Systems"/> light submitters.</summary>
internal static class LightSceneMath
{
    public static Vector2D<float> DirectionFromWorldRotation(float radians) =>
        new(MathF.Cos(radians), MathF.Sin(radians));

    public static float MaxAbsScale(in Vector2D<float> scale) =>
        MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y));
}
