using System;
using System.Collections.Generic;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Deterministic ordering helper applied before viewport culling and clamping overflowing light queues.
/// Four sort entry-points cover every light type:
/// <see cref="SortPointLights"/>, <see cref="SortSpotLights"/>, <see cref="SortDirectionalLights"/>,
/// and <see cref="SortAmbientLights"/>.
/// Point and spot lights sort by value-order: shadow-casting first, then descending visual weight
/// (Intensity × Radius²), then position-based tie-breaking (<c>PositionWorld.X</c>, then <c>PositionWorld.Y</c>),
/// then ascending <c>SubmissionIndex</c>. The positional tie-breaker ensures determinism independent of
/// thread-interleaving order in the concurrent drain. When overflow occurs the brightest, largest
/// shadow-casting lights survive and the dimmest get dropped.
/// Ambient lights sort by descending intensity, then color, then index.
/// Directional lights retain their original field-by-field sort (direction, intensity, color, shadow, index).
/// </summary>
/// <remarks>
/// <para>
/// Each Sort* method is called every frame for any list with ≥ 2 entries (guarded at the call site in
/// <c>FramePlanBuilder.Build</c>). This is intentional: parallel ECS systems drain into
/// <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> whose interleaving order is nondeterministic
/// across frames. Sorting guarantees (1) deterministic overflow truncation when the cap is hit, and (2) stable
/// SSBO upload order even below the cap, preventing per-frame light-index flicker that would cause visible
/// shadow/attenuation discontinuities.
/// The cost is negligible — Array.Sort on a small, already-sorted-ish array is near-linear.
/// </para>
/// <para>
/// <b>SubmissionIndex nondeterminism.</b> <c>SubmissionIndex</c> is stamped post-drain by
/// <c>StampSubmissionIndices</c> in <c>FramePlanBuilder.Build</c>, so it reflects nondeterministic
/// worker-thread interleaving in the concurrent queue. The position-based tie-breakers for point and
/// spot lights make the final order deterministic regardless of index assignment. Ambient lights have
/// no position, so two ambients with identical intensity and color may swap positions across frames —
/// in practice this is invisible because their contribution is summed.
/// </para>
/// </remarks>
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

    public static void SortAmbientLights(AmbientLight[] lights, int count)
    {
        if (count <= 1)
            return;
        Array.Sort(lights, 0, count, AmbientLightComparer.Instance);
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
            var c = y.CastsShadow.CompareTo(x.CastsShadow);
            if (c != 0)
                return c;
            c = (y.Intensity * y.Radius * y.Radius).CompareTo(x.Intensity * x.Radius * x.Radius);
            if (c != 0)
                return c;
            c = CompareVec2(x.PositionWorld, y.PositionWorld);
            if (c != 0)
                return c;
            return x.SubmissionIndex.CompareTo(y.SubmissionIndex);
        }
    }

    private sealed class SpotLightComparer : IComparer<SpotLight>
    {
        public static readonly SpotLightComparer Instance = new();

        public int Compare(SpotLight x, SpotLight y)
        {
            var c = y.CastsShadow.CompareTo(x.CastsShadow);
            if (c != 0)
                return c;
            c = (y.Intensity * y.Radius * y.Radius).CompareTo(x.Intensity * x.Radius * x.Radius);
            if (c != 0)
                return c;
            c = CompareVec2(x.PositionWorld, y.PositionWorld);
            if (c != 0)
                return c;
            return x.SubmissionIndex.CompareTo(y.SubmissionIndex);
        }
    }

    private sealed class DirectionalLightComparer : IComparer<DirectionalLight>
    {
        public static readonly DirectionalLightComparer Instance = new();

        public int Compare(DirectionalLight x, DirectionalLight y)
        {
            var c = y.CastsShadow.CompareTo(x.CastsShadow);
            if (c != 0)
                return c;
            c = y.Intensity.CompareTo(x.Intensity);
            if (c != 0)
                return c;
            c = CompareVec2(x.DirectionWorld, y.DirectionWorld);
            if (c != 0)
                return c;
            c = CompareVec3(x.Color, y.Color);
            if (c != 0)
                return c;
            return x.SubmissionIndex.CompareTo(y.SubmissionIndex);
        }
    }

    private sealed class AmbientLightComparer : IComparer<AmbientLight>
    {
        public static readonly AmbientLightComparer Instance = new();

        public int Compare(AmbientLight x, AmbientLight y)
        {
            // Descending intensity: brighter ambients survive overflow clamping.
            var c = y.Intensity.CompareTo(x.Intensity);
            if (c != 0)
                return c;
            c = CompareVec3(x.Color, y.Color);
            if (c != 0)
                return c;
            return x.SubmissionIndex.CompareTo(y.SubmissionIndex);
        }
    }
}
