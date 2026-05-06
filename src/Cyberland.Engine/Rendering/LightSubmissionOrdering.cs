using System;
using System.Collections.Generic;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Deterministic ordering helper applied before clamping overflowing light queues.
/// </summary>
internal static class LightSubmissionOrdering
{
    public static void SortPointLights(PointLight[] lights, int count)
    {
        if (count <= 1)
            return;
        Array.Sort(lights, 0, count, PointLightComparer.Instance);
    }

    public static void SortSpotLights(SpotLight[] lights, int count)
    {
        if (count <= 1)
            return;
        Array.Sort(lights, 0, count, SpotLightComparer.Instance);
    }

    public static void SortDirectionalLights(DirectionalLight[] lights, int count)
    {
        if (count <= 1)
            return;
        Array.Sort(lights, 0, count, DirectionalLightComparer.Instance);
    }

    private static int CompareVec2(in Vector2D<float> a, in Vector2D<float> b)
    {
        var c = a.X.CompareTo(b.X);
        if (c != 0)
            return c;
        return a.Y.CompareTo(b.Y);
    }

    private static int CompareVec3(in Vector3D<float> a, in Vector3D<float> b)
    {
        var c = a.X.CompareTo(b.X);
        if (c != 0)
            return c;
        c = a.Y.CompareTo(b.Y);
        if (c != 0)
            return c;
        return a.Z.CompareTo(b.Z);
    }

    private sealed class PointLightComparer : IComparer<PointLight>
    {
        public static readonly PointLightComparer Instance = new();

        public int Compare(PointLight x, PointLight y)
        {
            var c = CompareVec2(x.PositionWorld, y.PositionWorld);
            if (c != 0)
                return c;
            c = x.Radius.CompareTo(y.Radius);
            if (c != 0)
                return c;
            c = x.Intensity.CompareTo(y.Intensity);
            if (c != 0)
                return c;
            c = x.FalloffExponent.CompareTo(y.FalloffExponent);
            if (c != 0)
                return c;
            c = CompareVec3(x.Color, y.Color);
            if (c != 0)
                return c;
            return x.CastsShadow.CompareTo(y.CastsShadow);
        }
    }

    private sealed class SpotLightComparer : IComparer<SpotLight>
    {
        public static readonly SpotLightComparer Instance = new();

        public int Compare(SpotLight x, SpotLight y)
        {
            var c = CompareVec2(x.PositionWorld, y.PositionWorld);
            if (c != 0)
                return c;
            c = CompareVec2(x.DirectionWorld, y.DirectionWorld);
            if (c != 0)
                return c;
            c = x.Radius.CompareTo(y.Radius);
            if (c != 0)
                return c;
            c = x.InnerConeRadians.CompareTo(y.InnerConeRadians);
            if (c != 0)
                return c;
            c = x.OuterConeRadians.CompareTo(y.OuterConeRadians);
            if (c != 0)
                return c;
            c = x.Intensity.CompareTo(y.Intensity);
            if (c != 0)
                return c;
            c = CompareVec3(x.Color, y.Color);
            if (c != 0)
                return c;
            return x.CastsShadow.CompareTo(y.CastsShadow);
        }
    }

    private sealed class DirectionalLightComparer : IComparer<DirectionalLight>
    {
        public static readonly DirectionalLightComparer Instance = new();

        public int Compare(DirectionalLight x, DirectionalLight y)
        {
            var c = CompareVec2(x.DirectionWorld, y.DirectionWorld);
            if (c != 0)
                return c;
            c = x.Intensity.CompareTo(y.Intensity);
            if (c != 0)
                return c;
            c = CompareVec3(x.Color, y.Color);
            if (c != 0)
                return c;
            return x.CastsShadow.CompareTo(y.CastsShadow);
        }
    }
}
