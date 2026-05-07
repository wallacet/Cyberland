#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Pure aggregation for frame profiler samples (unit-tested; no GPU or threading). Compiled only in Debug builds.
/// </summary>
internal sealed class FrameProfilerScopeStats
{
    public long SumTicks;
    public int Count;
    public long MinTicks = long.MaxValue;
    public long MaxTicks;
    public long SumAllocBytes;
    public int AllocSampleCount;
    public readonly long[] RingTicks = new long[64];
    public int RingCount;

    public void AddTicks(long ticks)
    {
        SumTicks += ticks;
        Count++;
        if (ticks < MinTicks)
            MinTicks = ticks;
        if (ticks > MaxTicks)
            MaxTicks = ticks;
        RingTicks[RingCount % RingTicks.Length] = ticks;
        RingCount++;
    }

    public void AddAllocDelta(long deltaBytes)
    {
        SumAllocBytes += deltaBytes;
        AllocSampleCount++;
    }

    /// <summary>Approximate p99 from the last up to 64 samples (sorted copy).</summary>
    public long GetApproxP99Ticks()
    {
        var n = Math.Min(RingCount, RingTicks.Length);
        if (n <= 0)
            return 0;
        Span<long> copy = stackalloc long[n];
        for (var i = 0; i < n; i++)
            copy[i] = RingTicks[i];
        copy.Sort();
        var idx = (int)Math.Clamp(Math.Ceiling(0.99 * n) - 1, 0, n - 1);
        return copy[idx];
    }
}

/// <summary>Thread-safe accumulation keyed by scope name.</summary>
internal static class FrameProfilerStats
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, FrameProfilerScopeStats> Buckets = new(StringComparer.Ordinal);

    internal static void Clear()
    {
        lock (Gate)
            Buckets.Clear();
    }

    /// <summary>Test-only: inserts a named bucket with zero samples so dump/top-scope paths can skip it.</summary>
    internal static void RegisterEmptyScopeForTests(string name)
    {
        lock (Gate)
            Buckets[name] = new FrameProfilerScopeStats();
    }

    /// <summary>Accumulates one hierarchical profiler sample for <paramref name="name"/>.</summary>
    /// <param name="name">Scope label (e.g. system or subsystem name).</param>
    /// <param name="ticks">Elapsed time for this scope in <see cref="Stopwatch"/> ticks.</param>
    /// <param name="allocDeltaBytes">Byte delta from <see cref="GC.GetAllocatedBytesForCurrentThread"/> when allocation tracking is on.</param>
    /// <param name="includeAllocSample">When false, skips per-scope allocation accounting (keeps hot paths cheap).</param>
    internal static void Record(string name, long ticks, long allocDeltaBytes, bool includeAllocSample = true)
    {
        lock (Gate)
        {
            if (!Buckets.TryGetValue(name, out var s))
            {
                s = new FrameProfilerScopeStats();
                Buckets[name] = s;
            }

            s.AddTicks(ticks);
            if (includeAllocSample)
                s.AddAllocDelta(allocDeltaBytes);
        }
    }

    internal static int BucketCount
    {
        get
        {
            lock (Gate)
                return Buckets.Count;
        }
    }

    internal static bool TryGetStat(string name, out FrameProfilerScopeStats stats)
    {
        lock (Gate)
            return Buckets.TryGetValue(name, out stats!);
    }

    internal static List<string> GetScopeNamesSnapshot()
    {
        lock (Gate)
            return new List<string>(Buckets.Keys);
    }

    /// <summary>Deterministic dump for <see cref="FrameProfiler.WriteDump"/>.</summary>
    internal static void AppendDump(
        StringBuilder sb,
        int framesRecorded,
        double wallSeconds,
        long warmupTicks,
        long gen0,
        long gen1,
        long gen2)
    {
        sb.AppendLine($"frames={framesRecorded} wallSeconds={wallSeconds.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"warmupTicks={warmupTicks} GC_gen012={gen0},{gen1},{gen2}");
        sb.AppendLine("scope\tcount\tavgMs\tminMs\tmaxMs\tp99Ms\tavgAllocB");

        List<(string Name, FrameProfilerScopeStats S)> rows;
        lock (Gate)
        {
            rows = new List<(string, FrameProfilerScopeStats)>(Buckets.Count);
            foreach (var kv in Buckets)
                rows.Add((kv.Key, kv.Value));
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        var freq = Stopwatch.Frequency;
        foreach (var (name, st) in rows)
        {
            if (st.Count <= 0)
                continue;
            var avgMs = (st.SumTicks / (double)st.Count) * 1000.0 / freq;
            var minMs = st.MinTicks == long.MaxValue ? 0 : st.MinTicks * 1000.0 / freq;
            var maxMs = st.MaxTicks * 1000.0 / freq;
            var p99Ms = st.GetApproxP99Ticks() * 1000.0 / freq;
            var avgAlloc = st.AllocSampleCount > 0 ? st.SumAllocBytes / (double)st.AllocSampleCount : 0;
            sb.Append(name);
            sb.Append('\t');
            sb.Append(st.Count.ToString(CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.Append(avgMs.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.Append(minMs.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.Append(maxMs.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.Append(p99Ms.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.AppendLine(avgAlloc.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }
}
#endif
