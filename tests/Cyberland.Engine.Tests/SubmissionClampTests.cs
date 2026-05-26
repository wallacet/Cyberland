using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Tests;

public sealed class SubmissionClampTests
{
    [Theory]
    [InlineData(0, 32, 0, 0)]
    [InlineData(-5, 32, 0, 0)]
    [InlineData(12, 32, 12, 0)]
    [InlineData(32, 32, 32, 0)]
    [InlineData(40, 32, 32, 8)]
    [InlineData(10, 0, 0, 10)]
    [InlineData(5, -5, 0, 5)]
    [InlineData(1, int.MinValue, 0, 1)]
    public void ClampWithDropCount_returns_expected_values(int submitted, int cap, int expectedKept, int expectedDropped)
    {
        var kept = SubmissionClamp.ClampWithDropCount(submitted, cap, out var dropped);

        Assert.Equal(expectedKept, kept);
        Assert.Equal(expectedDropped, dropped);
    }

    [Fact]
    public void ClampWithDropCount_MaxZero_ReturnsZero()
    {
        var kept = SubmissionClamp.ClampWithDropCount(5, 0, out var dropped);
        Assert.Equal(0, kept);
        Assert.Equal(5, dropped);
    }

    [Fact]
    public void ClampWithDropCount_MaxNegative_ReturnsZero()
    {
        var kept = SubmissionClamp.ClampWithDropCount(5, -5, out var dropped);
        Assert.Equal(0, kept);
        Assert.Equal(5, dropped);
    }
}
