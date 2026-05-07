#if DEBUG
using System.Text;
using Cyberland.Engine.Diagnostics;
using Xunit;

namespace Cyberland.Engine.Tests;

[Collection("FrameProfiler")]
public sealed class FrameProfilerStatsTests
{
    [Fact]
    public void FrameProfilerScopeStats_GetApproxP99_ticks_empty_ring_returns_zero()
    {
        var s = new FrameProfilerScopeStats();
        Assert.Equal(0, s.GetApproxP99Ticks());
    }

    [Fact]
    public void Record_then_append_dump_includes_sorted_scope_rows()
    {
        FrameProfilerStats.Clear();
        FrameProfilerStats.Record("b.scope", 100, 10);
        FrameProfilerStats.Record("a.scope", 200, 20);
        FrameProfilerStats.Record("a.scope", 300, 0);
        Assert.True(FrameProfilerStats.BucketCount >= 2);

        var sb = new StringBuilder();
        FrameProfilerStats.AppendDump(sb, framesRecorded: 2, wallSeconds: 1.5, warmupTicks: 99, gen0: 1, gen1: 0, gen2: 0);
        var text = sb.ToString();
        Assert.Contains("frames=2", text);
        Assert.Contains("wallSeconds=1.5", text);
        Assert.Contains("warmupTicks=99", text);
        Assert.Contains("GC_gen012=1,0,0", text);
        var aIdx = text.IndexOf("a.scope", StringComparison.Ordinal);
        var bIdx = text.IndexOf("b.scope", StringComparison.Ordinal);
        Assert.True(aIdx >= 0 && bIdx >= 0);
        Assert.True(aIdx < bIdx, "dump should sort scopes lexicographically");
        Assert.Contains("a.scope\t2\t", text);
    }

    [Fact]
    public void GetApproxP99_ticks_handles_single_sample_and_sorted_ring()
    {
        var s = new FrameProfilerScopeStats();
        s.AddTicks(10);
        Assert.Equal(10, s.GetApproxP99Ticks());

        FrameProfilerStats.Clear();
        for (var i = 0; i < 80; i++)
            FrameProfilerStats.Record("flat", 100 + i, 0);

        Assert.True(FrameProfilerStats.TryGetStat("flat", out var st));
        Assert.True(st.Count >= 64);
        var p99 = st.GetApproxP99Ticks();
        Assert.InRange(p99, 100, 200);
    }

    [Fact]
    public void Frame_profiler_overlay_append_hud_does_not_throw()
    {
        var sb = new StringBuilder();
        FrameProfilerOverlay.AppendHud(sb, maxLines: 4);
        _ = sb.ToString();
    }

    [Fact]
    public void FrameProfiler_AppendTopScopes_empty_buckets_is_safe()
    {
        FrameProfilerStats.Clear();
        var sb = new StringBuilder();
        FrameProfiler.AppendTopScopes(sb, 4);
        Assert.Equal(0, sb.Length);
    }

    [Fact]
    public void AppendDump_skips_zero_sample_buckets()
    {
        FrameProfilerStats.Clear();
        FrameProfilerStats.RegisterEmptyScopeForTests("empty.bucket");
        var sb = new StringBuilder();
        FrameProfilerStats.AppendDump(sb, 0, 1.0, 0, 0, 0, 0);
        Assert.DoesNotContain("empty.bucket\t", sb.ToString(), StringComparison.Ordinal);
    }
}
#endif
