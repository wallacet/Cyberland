using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Debug-only hierarchical CPU profiler for the render thread. Release builds compile scopes to no-ops.
/// </summary>
public static class FrameProfiler
{
#if DEBUG
    private static volatile bool _enabled = true;
    [ThreadStatic]
    private static Stack<(string Name, long StartTicks, long StartBytes)>? _threadScopeStack;

    private static readonly Stopwatch Wall = Stopwatch.StartNew();
    private static long _warmupEndTicks;
    private static int _framesAfterWarmup;
    private static long _gen0AtStart;
    private static long _gen1AtStart;
    private static long _gen2AtStart;

    /// <summary>Whether debug profiling scopes record samples.</summary>
    public static bool IsEnabled => _enabled;

    /// <summary>Turns debug profiling on/off at runtime.</summary>
    public static void SetEnabled(bool enabled) => _enabled = enabled;

    /// <summary>
    /// Reads <c>CYBERLAND_ENABLE_FRAME_PROFILER</c> once: <c>0/false</c> disables, <c>1/true</c> enables.
    /// Empty/unset keeps the current setting.
    /// </summary>
    public static void ApplyEnvironmentDefaults()
    {
        var v = Environment.GetEnvironmentVariable("CYBERLAND_ENABLE_FRAME_PROFILER");
        if (string.IsNullOrWhiteSpace(v))
            return;
        if (v.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            v.Equals("false", StringComparison.OrdinalIgnoreCase))
            _enabled = false;
        else if (v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                 v.Equals("true", StringComparison.OrdinalIgnoreCase))
            _enabled = true;
    }

    /// <summary>Wall time after ctor when samples are recorded (default 1 s).</summary>
    public static void ConfigureWarmup(TimeSpan wallWarmup) =>
        _warmupEndTicks = Wall.ElapsedTicks + wallWarmup.Ticks;

    /// <summary>Reset session counters (call when starting a profile run).</summary>
    public static void ResetSession()
    {
        FrameProfilerStats.Clear();
        _framesAfterWarmup = 0;
        _warmupEndTicks = Wall.ElapsedTicks + Stopwatch.Frequency;
        _gen0AtStart = GC.CollectionCount(0);
        _gen1AtStart = GC.CollectionCount(1);
        _gen2AtStart = GC.CollectionCount(2);
    }

    /// <summary>Marks one presented frame after warmup; called from the main render loop.</summary>
    public static void MarkFrame()
    {
        if (!_enabled || Wall.ElapsedTicks < _warmupEndTicks)
            return;
        Interlocked.Increment(ref _framesAfterWarmup);
    }

    internal static void Push(string name)
    {
        if (!_enabled)
            return;
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var stack = _threadScopeStack ??= new Stack<(string Name, long StartTicks, long StartBytes)>(64);
        stack.Push((name, Stopwatch.GetTimestamp(), bytes));
    }

    internal static void Pop()
    {
        if (!_enabled)
            return;
        long endTicks = Stopwatch.GetTimestamp();
        var endBytes = GC.GetAllocatedBytesForCurrentThread();
        var stack = _threadScopeStack;
        if (stack is null || stack.Count == 0)
            return;
        var (name, startTicks, startBytes) = stack.Pop();

        var dt = endTicks - startTicks;
        var dBytes = endBytes - startBytes;
        if (Wall.ElapsedTicks >= _warmupEndTicks)
            FrameProfilerStats.Record(name, dt, dBytes);
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
    /// <summary>Release constant false; profiling is compiled out.</summary>
    public static bool IsEnabled => false;
    /// <summary>Release no-op; profiling is compiled out.</summary>
    public static void SetEnabled(bool enabled) => _ = enabled;
    /// <summary>Release no-op; profiling is compiled out.</summary>
    public static void ApplyEnvironmentDefaults() { }
    /// <summary>Release no-op; profiling is compiled out.</summary>
    public static void MarkFrame() { }

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
        if (!FrameProfiler.IsEnabled)
            return default;
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
