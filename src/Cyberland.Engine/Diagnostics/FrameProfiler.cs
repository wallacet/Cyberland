using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Debug-only hierarchical CPU profiler for the render thread. Release builds compile scopes to no-ops.
/// </summary>
public static class FrameProfiler
{
#if DEBUG
    private static readonly object StackGate = new();
    private static readonly Stack<(string Name, long StartTicks, long StartBytes)> Stack = new();

    private static readonly Stopwatch Wall = Stopwatch.StartNew();
    private static long _warmupEndTicks;
    private static int _framesAfterWarmup;
    private static long _gen0AtStart;
    private static long _gen1AtStart;
    private static long _gen2AtStart;

    /// <summary>Wall time after ctor when samples are recorded (default 1 s).</summary>
    public static void ConfigureWarmup(TimeSpan wallWarmup) =>
        _warmupEndTicks = Wall.ElapsedTicks + wallWarmup.Ticks;

    /// <summary>Reset session counters (call when starting a profile run).</summary>
    public static void ResetSession()
    {
        lock (StackGate)
            Stack.Clear();
        FrameProfilerStats.Clear();
        _framesAfterWarmup = 0;
        _warmupEndTicks = Wall.ElapsedTicks + Stopwatch.Frequency;
        _gen0AtStart = GC.CollectionCount(0);
        _gen1AtStart = GC.CollectionCount(1);
        _gen2AtStart = GC.CollectionCount(2);
    }

    internal static void Push(string name)
    {
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        lock (StackGate)
            Stack.Push((name, Stopwatch.GetTimestamp(), bytes));
    }

    internal static void Pop()
    {
        long endTicks = Stopwatch.GetTimestamp();
        var endBytes = GC.GetAllocatedBytesForCurrentThread();
        string name;
        long startTicks;
        long startBytes;
        lock (StackGate)
        {
            if (Stack.Count == 0)
                return;
            (name, startTicks, startBytes) = Stack.Pop();
        }

        var dt = endTicks - startTicks;
        var dBytes = endBytes - startBytes;
        if (Wall.ElapsedTicks >= _warmupEndTicks)
        {
            FrameProfilerStats.Record(name, dt, dBytes);
            if (Stack.Count == 0)
                _framesAfterWarmup++;
        }
    }

    /// <summary>Write aggregated stats to a UTF-8 text file (creates parent directories).</summary>
    public static void WriteDump(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var wallSec = Wall.Elapsed.TotalSeconds;
        var sb = new StringBuilder(4096);
        FrameProfilerStats.AppendDump(
            sb,
            _framesAfterWarmup,
            wallSec,
            _warmupEndTicks,
            GC.CollectionCount(0) - _gen0AtStart,
            GC.CollectionCount(1) - _gen1AtStart,
            GC.CollectionCount(2) - _gen2AtStart);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>Top scopes by average ms this session (after warmup), for HUD overlay.</summary>
    public static void AppendTopScopes(StringBuilder sb, int maxLines)
    {
        List<(string Name, double AvgMs)> rows = new();
        foreach (var name in FrameProfilerStats.GetScopeNamesSnapshot())
        {
            if (!FrameProfilerStats.TryGetStat(name, out var st) || st.Count <= 0)
                continue;
            var avgMs = (st.SumTicks / (double)st.Count) * 1000.0 / Stopwatch.Frequency;
            rows.Add((name, avgMs));
        }

        rows.Sort((a, b) => b.AvgMs.CompareTo(a.AvgMs));
        for (var i = 0; i < rows.Count && i < maxLines; i++)
            sb.AppendLine($"{rows[i].AvgMs:0.0}ms {rows[i].Name}");
    }
#else
    /// <summary>Release no-op; profiling is compiled out.</summary>
    public static void ConfigureWarmup(TimeSpan wallWarmup) => _ = wallWarmup;

    /// <summary>Release no-op; profiling is compiled out.</summary>
    public static void ResetSession() { }

    /// <summary>Release no-op; profiling is compiled out.</summary>
    public static void WriteDump(string path) => _ = path;

    /// <summary>Release no-op; profiling is compiled out.</summary>
    public static void AppendTopScopes(StringBuilder sb, int maxLines)
    {
        _ = sb;
        _ = maxLines;
    }
#endif

#if DEBUG
    internal static void PushInternal(string name) => Push(name);

    internal static void PopInternal() => Pop();
#endif
}

#if DEBUG
/// <summary>RAII scope for <see cref="FrameProfiler"/> (debug only).</summary>
public ref struct FrameProfilerScope
{
    private readonly bool _active;

    internal FrameProfilerScope(bool active) => _active = active;

    /// <summary>Begins a named scope; pair with <see cref="Dispose"/>.</summary>
    public static FrameProfilerScope Enter(string name)
    {
        FrameProfiler.PushInternal(name);
        return new FrameProfilerScope(true);
    }

    /// <summary>Ends the scope started by <see cref="Enter"/>.</summary>
    public readonly void Dispose()
    {
        if (_active)
            FrameProfiler.PopInternal();
    }
}
#else
/// <summary>Release no-op scope.</summary>
[ExcludeFromCodeCoverage(Justification = "Empty dispose; profiling stripped in Release.")]
public ref struct FrameProfilerScope
{
    /// <summary>Release no-op; does not record.</summary>
    public static FrameProfilerScope Enter(string name)
    {
        _ = name;
        return default;
    }

    /// <summary>Release no-op.</summary>
    public readonly void Dispose() { }
}
#endif
