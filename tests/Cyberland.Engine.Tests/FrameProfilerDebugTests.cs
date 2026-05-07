#if DEBUG
using System.Text;
using System.Threading.Tasks;
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

    [Fact]
    public void FrameProfiler_disable_short_circuits_scope_recording()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        FrameProfiler.SetEnabled(false);
        try
        {
            using (FrameProfilerScope.Enter("disabled.scope")) { }
            Assert.False(FrameProfilerStats.TryGetStat("disabled.scope", out _));
        }
        finally
        {
            FrameProfiler.SetEnabled(true);
        }
    }

    [Fact]
    public void FrameProfiler_mark_frame_writes_positive_frame_count()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        FrameProfiler.MarkFrame();
        FrameProfiler.MarkFrame();

        var path = Path.Combine(Path.GetTempPath(), "cybprof-frames" + Guid.NewGuid() + ".txt");
        try
        {
            FrameProfiler.WriteDump(path);
            var text = File.ReadAllText(path);
            Assert.Contains("frames=2", text, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void FrameProfiler_parallel_scopes_record_without_stack_corruption()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        Parallel.For(0, 64, _ =>
        {
            using (FrameProfilerScope.Enter("parallel.outer"))
            {
                using (FrameProfilerScope.Enter("parallel.inner")) { }
            }
        });

        Assert.True(FrameProfilerStats.TryGetStat("parallel.outer", out var outer) && outer.Count >= 64);
        Assert.True(FrameProfilerStats.TryGetStat("parallel.inner", out var inner) && inner.Count >= 64);
    }

    private const string FrameProfilerEnvVar = "CYBERLAND_ENABLE_FRAME_PROFILER";

    [Fact]
    public void FrameProfiler_ApplyEnvironmentDefaults_noop_for_empty_or_whitespace_env()
    {
        var prev = Environment.GetEnvironmentVariable(FrameProfilerEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(FrameProfilerEnvVar, null);
            FrameProfiler.SetEnabled(true);
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.True(FrameProfiler.IsEnabled);

            Environment.SetEnvironmentVariable(FrameProfilerEnvVar, "  ");
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.True(FrameProfiler.IsEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FrameProfilerEnvVar, prev);
            FrameProfiler.SetEnabled(true);
            FrameProfiler.TrackSessionAllocations = false;
        }
    }

    [Fact]
    public void FrameProfiler_ApplyEnvironmentDefaults_toggles_from_env_strings()
    {
        var prev = Environment.GetEnvironmentVariable(FrameProfilerEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(FrameProfilerEnvVar, "0");
            FrameProfiler.SetEnabled(true);
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.False(FrameProfiler.IsEnabled);

            Environment.SetEnvironmentVariable(FrameProfilerEnvVar, "FALSE");
            FrameProfiler.SetEnabled(true);
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.False(FrameProfiler.IsEnabled);

            Environment.SetEnvironmentVariable(FrameProfilerEnvVar, "1");
            FrameProfiler.SetEnabled(false);
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.True(FrameProfiler.IsEnabled);

            Environment.SetEnvironmentVariable(FrameProfilerEnvVar, "TRUE");
            FrameProfiler.SetEnabled(false);
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.True(FrameProfiler.IsEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FrameProfilerEnvVar, prev);
            FrameProfiler.SetEnabled(true);
            FrameProfiler.TrackSessionAllocations = false;
        }
    }

    private const string FrameProfilerTrackAllocEnvVar = "CYBERLAND_FRAME_PROFILER_TRACK_ALLOC";

    [Fact]
    public void FrameProfiler_ApplyEnvironmentDefaults_track_alloc_from_env()
    {
        var prevAlloc = Environment.GetEnvironmentVariable(FrameProfilerTrackAllocEnvVar);
        try
        {
            FrameProfiler.TrackSessionAllocations = false;

            Environment.SetEnvironmentVariable(FrameProfilerTrackAllocEnvVar, "1");
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.True(FrameProfiler.TrackSessionAllocations);

            FrameProfiler.TrackSessionAllocations = false;
            Environment.SetEnvironmentVariable(FrameProfilerTrackAllocEnvVar, "TRUE");
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.True(FrameProfiler.TrackSessionAllocations);

            FrameProfiler.TrackSessionAllocations = true;
            Environment.SetEnvironmentVariable(FrameProfilerTrackAllocEnvVar, "  ");
            FrameProfiler.ApplyEnvironmentDefaults();
            Assert.True(FrameProfiler.TrackSessionAllocations);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FrameProfilerTrackAllocEnvVar, prevAlloc);
            FrameProfiler.TrackSessionAllocations = false;
        }
    }

    [Fact]
    public void FrameProfiler_MarkFrame_before_warmup_end_is_ignored()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.SetEnabled(true);
        FrameProfiler.MarkFrame();

        var path = Path.Combine(Path.GetTempPath(), "cybprof-warmup" + Guid.NewGuid() + ".txt");
        try
        {
            FrameProfiler.WriteDump(path);
            var text = File.ReadAllText(path);
            Assert.Contains("frames=0", text, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }

            FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        }
    }

    [Fact]
    public void FrameProfiler_PushInternal_and_PopInternal_short_circuit_when_disabled()
    {
        FrameProfiler.ResetSession();
        FrameProfiler.ConfigureWarmup(TimeSpan.Zero);
        FrameProfiler.SetEnabled(false);
        try
        {
            FrameProfiler.PushInternal("noop.when.disabled");
            FrameProfiler.PopInternal();
            Assert.False(FrameProfilerStats.TryGetStat("noop.when.disabled", out _));
        }
        finally
        {
            FrameProfiler.SetEnabled(true);
        }
    }
}
#endif
