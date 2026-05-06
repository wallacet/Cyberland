using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Tests;

public sealed class LightSubmissionPolicyTests
{
    [Theory]
    [InlineData(0, 32, 0, 0)]
    [InlineData(-5, 32, 0, 0)]
    [InlineData(12, 32, 12, 0)]
    [InlineData(32, 32, 32, 0)]
    [InlineData(40, 32, 32, 8)]
    public void ClampWithDropCount_returns_expected_values(int submitted, int cap, int expectedKept, int expectedDropped)
    {
        var kept = LightSubmissionPolicy.ClampWithDropCount(submitted, cap, out var dropped);

        Assert.Equal(expectedKept, kept);
        Assert.Equal(expectedDropped, dropped);
    }
}
