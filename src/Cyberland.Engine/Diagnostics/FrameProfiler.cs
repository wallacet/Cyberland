using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Debug-only hierarchical CPU profiler for the render thread. Release builds expose an inert API (no scopes, no samples).
/// </summary>
public static class FrameProfiler
{
#if DEBUG
    private static volatile bool _enabled = true;
    /// <summary>
    /// When true, <see cref="Push"/>/<see cref="Pop"/> call <see cref="GC.GetAllocatedBytesForCurrentThread"/> twice per scope.
    /// That is useful for dump sessions but costs multiple ms/frame at high FPS; keep off during normal Debug play.
    /// </summary>
    private static volatile bool _trackSessionAllocations;
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
    /// When true (default false), scope enter/exit records per-scope allocation deltas via the GC API (expensive).
    /// </summary>
    public static bool TrackSessionAllocations
    {
        get => _trackSessionAllocations;
        set => _trackSessionAllocations = value;
    }

    /// <summary>
    /// Reads profiler env vars once: <c>CYBERLAND_ENABLE_FRAME_PROFILER</c> toggles scopes;
    /// <c>CYBERLAND_FRAME_PROFILER_TRACK_ALLOC=1</c> enables allocation deltas (expensive).
    /// Empty/unset leaves each flag unchanged. Use <c>--profile-alloc</c> (see <see cref="ProfileCommandLine.TryParseProfileAlloc"/>)
    /// or this env var to enable per-scope byte deltas — keep off for timing-only profile dumps.
    /// </summary>
    public static void ApplyEnvironmentDefaults()
    {
        var v = Environment.GetEnvironmentVariable("CYBERLAND_ENABLE_FRAME_PROFILER");
        if (!string.IsNullOrWhiteSpace(v))
        {
            if (v.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("false", StringComparison.OrdinalIgnoreCase))
                _enabled = false;
            else if (v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                     v.Equals("true", StringComparison.OrdinalIgnoreCase))
                _enabled = true;
        }

        var a = Environment.GetEnvironmentVariable("CYBERLAND_FRAME_PROFILER_TRACK_ALLOC");
        if (!string.IsNullOrWhiteSpace(a) &&
            (a.Equals("1", StringComparison.OrdinalIgnoreCase) || a.Equals("true", StringComparison.OrdinalIgnoreCase)))
            _trackSessionAllocations = true;
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
        var bytes = _trackSessionAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0L;
        var stack = _threadScopeStack ??= new Stack<(string Name, long StartTicks, long StartBytes)>(64);
        stack.Push((name, Stopwatch.GetTimestamp(), bytes));
    }

    internal static void Pop()
    {
        if (!_enabled)
            return;
        long endTicks = Stopwatch.GetTimestamp();
        var endBytes = _trackSessionAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0L;
        var stack = _threadScopeStack;
        if (stack is null || stack.Count == 0)
            return;
        var (name, startTicks, startBytes) = stack.Pop();

        var dt = endTicks - startTicks;
        var dBytes = endBytes - startBytes;
        if (Wall.ElapsedTicks >= _warmupEndTicks)
            FrameProfilerStats.Record(name, dt, dBytes, _trackSessionAllocations);
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
#endif
