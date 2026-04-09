using System.Numerics;

namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// SIMD-friendly helpers for contiguous <see cref="float"/> spans (e.g. per-chunk columns).
/// Peels a scalar tail when length is not a multiple of <see cref="Vector{T}.Count"/>.
/// </summary>
public static class SimdFloat
{
    /// <summary>values[i] *= scale</summary>
    public static void MultiplyInPlace(Span<float> values, float scale)
    {
        if (values.IsEmpty)
            return;

        var n = values.Length;
        var vecSize = Vector<float>.Count;
        var i = 0;
        var limit = n - (n % vecSize);
        for (; i < limit; i += vecSize)
        {
            var slice = values.Slice(i, vecSize);
            var v = new Vector<float>(slice) * new Vector<float>(scale);
            v.CopyTo(slice);
        }

        for (; i < n; i++)
            values[i] *= scale;
    }

    /// <summary>dst[i] = a[i] * b[i] (length = min of spans)</summary>
    public static void MultiplyElementWise(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> dst)
    {
        var n = Math.Min(a.Length, b.Length);
        n = Math.Min(n, dst.Length);
        if (n <= 0)
            return;

        var vecSize = Vector<float>.Count;
        var i = 0;
        var limit = n - (n % vecSize);
        for (; i < limit; i += vecSize)
        {
            var sa = a.Slice(i, vecSize);
            var sb = b.Slice(i, vecSize);
            var v = new Vector<float>(sa) * new Vector<float>(sb);
            v.CopyTo(dst.Slice(i, vecSize));
        }

        for (; i < n; i++)
            dst[i] = a[i] * b[i];
    }
}
