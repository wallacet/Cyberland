#if !DEBUG
using System.Text;
using Cyberland.Engine.Diagnostics;
using Xunit;

namespace Cyberland.Engine.Tests;

/// <summary>Validates hierarchical profiler types compile to inert stubs in Release.</summary>
public sealed class FrameProfilerReleaseTests
{
    [Fact]
    public void Frame_profiler_overlay_append_hud_does_not_throw()
    {
        var sb = new StringBuilder();
        FrameProfilerOverlay.AppendHud(sb, maxLines: 4);
        _ = sb.ToString();
    }

    [Fact]
    public void FrameProfiler_AppendTopScopes_does_not_append_when_stripped()
    {
        var sb = new StringBuilder();
        FrameProfiler.AppendTopScopes(sb, 4);
        Assert.Equal(0, sb.Length);
    }

    [Fact]
    public void FrameProfiler_public_api_is_inert_in_Release()
    {
        Assert.False(FrameProfiler.IsEnabled);
        FrameProfiler.SetEnabled(true);
        Assert.False(FrameProfiler.IsEnabled);
        FrameProfiler.ApplyEnvironmentDefaults();
        FrameProfiler.ConfigureWarmup(TimeSpan.FromSeconds(1));
        FrameProfiler.MarkFrame();
        FrameProfiler.ResetSession();
        FrameProfiler.WriteDump("nul");
        var sb = new StringBuilder();
        FrameProfiler.AppendTopScopes(sb, 2);
        Assert.Equal(0, sb.Length);
    }
}
#endif
