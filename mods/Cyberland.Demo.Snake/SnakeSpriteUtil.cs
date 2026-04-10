using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

internal static class SnakeSpriteUtil
{
    public static SpriteDrawRequest Q(
        int white,
        int n,
        Vector2D<float> c,
        Vector2D<float> half,
        int layer,
        float sort,
        Vector4D<float> color,
        float alpha,
        float em,
        Vector3D<float> et = default,
        bool transparent = false) =>
        new()
        {
            CenterWorld = c,
            HalfExtentsWorld = half,
            RotationRadians = 0f,
            Layer = layer,
            SortKey = sort,
            AlbedoTextureId = white,
            NormalTextureId = n,
            EmissiveTextureId = -1,
            ColorMultiply = color,
            Alpha = alpha,
            EmissiveTint = et,
            EmissiveIntensity = em,
            DepthHint = sort,
            Transparent = transparent
        };
}
