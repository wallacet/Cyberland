namespace Cyberland.Engine.Core.Ecs;

internal static class SignatureHelpers
{
    public static int BinarySearchUint(ReadOnlySpan<uint> span, uint value)
    {
        var lo = 0;
        var hi = span.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            var m = span[mid];
            if (m == value)
                return mid;
            if (m < value)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return ~lo;
    }

    public static uint[] InsertSorted(ReadOnlySpan<uint> sorted, uint value)
    {
        var idx = BinarySearchUint(sorted, value);
        if (idx >= 0)
        {
            var dup = new uint[sorted.Length];
            sorted.CopyTo(dup);
            return dup;
        }

        var insert = ~idx;
        var next = new uint[sorted.Length + 1];
        sorted[..insert].CopyTo(next);
        next[insert] = value;
        sorted[insert..].CopyTo(next.AsSpan(insert + 1));
        return next;
    }

    public static uint[] RemoveSorted(ReadOnlySpan<uint> sorted, uint value)
    {
        var idx = BinarySearchUint(sorted, value);
        if (idx < 0)
        {
            var dup = new uint[sorted.Length];
            sorted.CopyTo(dup);
            return dup;
        }

        if (sorted.Length == 1)
            return Array.Empty<uint>();

        var next = new uint[sorted.Length - 1];
        sorted[..idx].CopyTo(next);
        sorted[(idx + 1)..].CopyTo(next.AsSpan(idx));
        return next;
    }
}
