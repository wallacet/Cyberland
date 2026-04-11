using Cyberland.Engine.Rendering;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class FramePacingCpuTests
{
    [Theory]
    [InlineData(0.0, 60, 1.0 / 60.0)]
    [InlineData(1.0 / 120.0, 60, 1.0 / 60.0 - 1.0 / 120.0)]
    [InlineData(1.0 / 60.0, 60, 0)]
    [InlineData(0.1, 60, 0)]
    public void GetRemainingDelaySeconds(double elapsed, int fps, double expected)
    {
        var r = FramePacingCpu.GetRemainingDelaySeconds(elapsed, fps);
        Assert.Equal(expected, r, 9);
    }

    [Fact]
    public void GetRemainingDelaySeconds_invalid_fps_returns_zero()
    {
        Assert.Equal(0, FramePacingCpu.GetRemainingDelaySeconds(0, 0));
    }
}
