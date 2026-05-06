#if DEBUG
using System.Text;
using Cyberland.Engine.Diagnostics;
using Xunit;

namespace Cyberland.Engine.Tests;

/// <summary>Covers debug-only <see cref="FrameProfiler"/> stack paths (stripped in Release).</summary>
[Collection("FrameProfiler")]
public sealed class FrameProfilerDebugTests
{
    [Fact]
    public void FrameProfiler_nested_scopes_record_stats()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        using (FrameProfilerScope.Enter("outer"))
        {
            using (FrameProfilerScope.Enter("inner")) { }
        }

        Assert.True(FrameProfilerStats.TryGetStat("outer", out var o) && o.Count >= 1);
        Assert.True(FrameProfilerStats.TryGetStat("inner", out var i) && i.Count >= 1);
        Assert.True(FrameProfilerStats.BucketCount >= 2);
    }

    [Fact]
    public void FrameProfiler_WriteDump_writes_scopes()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        using (FrameProfilerScope.Enter("dump.me")) { }

        var path = Path.Combine(Path.GetTempPath(), "cybprof" + Guid.NewGuid() + ".txt");
        try
        {
            FrameProfiler.WriteDump(path);
            var text = File.ReadAllText(path);
            Assert.Contains("dump.me", text, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // best-effort cleanup on locked temp paths
            }
        }
    }

    [Fact]
    public void FrameProfiler_WriteDump_whitespace_path_is_ignored()
    {
        FrameProfiler.WriteDump("   ");
    }

    [Fact]
    public void FrameProfiler_scope_double_dispose_extra_pop_is_safe()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        var scope = FrameProfilerScope.Enter("once");
        scope.Dispose();
        scope.Dispose();
    }

    [Fact]
    public void FrameProfiler_AppendTopScopes_formats_lines()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        using (FrameProfilerScope.Enter("top.scope")) { }

        var sb = new StringBuilder();
        FrameProfiler.AppendTopScopes(sb, 8);
        Assert.Contains("top.scope", sb.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FrameProfiler_AppendTopScopes_skips_zero_count_buckets()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        FrameProfilerStats.RegisterEmptyScopeForTests("ghost");
        using (FrameProfilerScope.Enter("real.scope")) { }

        var sb = new StringBuilder();
        FrameProfiler.AppendTopScopes(sb, 8);
        Assert.DoesNotContain("ghost", sb.ToString(), StringComparison.Ordinal);
        Assert.Contains("real.scope", sb.ToString(), StringComparison.Ordinal);
    }
}
#endif
