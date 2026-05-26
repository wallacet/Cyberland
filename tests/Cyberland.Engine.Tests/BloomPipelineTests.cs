using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Tests;

public sealed class BloomPipelineTests
{
    [Fact]
    public void BloomGainZero_ReturnsEarly()
    {
        Assert.False(VulkanRenderer.IsBloomEffective(bloomEnabled: true, bloomGain: 0f, bloomRadius: 1f));
    }

    [Fact]
    public void BloomRadiusZero_ProducesNoBloom()
    {
        Assert.False(VulkanRenderer.IsBloomEffective(bloomEnabled: true, bloomGain: 1f, bloomRadius: 0f));
        Assert.False(VulkanRenderer.IsBloomEffective(bloomEnabled: false, bloomGain: 1f, bloomRadius: 1f));
        Assert.True(VulkanRenderer.IsBloomEffective(bloomEnabled: true, bloomGain: 0.5f, bloomRadius: 0.5f));
    }

    [Fact]
    public void GetBloomGaussianRadiusScale_Monotonic()
    {
        var r05 = VulkanRenderer.GetBloomGaussianRadiusScale(0.5f);
        var r10 = VulkanRenderer.GetBloomGaussianRadiusScale(1.0f);
        var r20 = VulkanRenderer.GetBloomGaussianRadiusScale(2.0f);

        Assert.True(r05 < r10, $"0.5 → {r05} should be less than 1.0 → {r10}");
        Assert.True(r10 < r20, $"1.0 → {r10} should be less than 2.0 → {r20}");
        Assert.True(r05 > 0f, "result should be positive");
    }

    [Fact]
    public void IsBloomEffective_all_disabled_combinations_return_false()
    {
        Assert.False(VulkanRenderer.IsBloomEffective(false, 0f, 0f));
        Assert.False(VulkanRenderer.IsBloomEffective(false, 1f, 0f));
        Assert.False(VulkanRenderer.IsBloomEffective(false, 0f, 1f));
        Assert.False(VulkanRenderer.IsBloomEffective(true, -1f, 1f));
        Assert.False(VulkanRenderer.IsBloomEffective(true, 1f, -1f));
    }

    [Fact]
    public void GetBloomGaussianRadiusScale_clamps_tiny_and_large_inputs()
    {
        var rTiny = VulkanRenderer.GetBloomGaussianRadiusScale(0.001f);
        var rHuge = VulkanRenderer.GetBloomGaussianRadiusScale(100f);

        // Both should produce finite, positive values thanks to internal clamping.
        Assert.True(rTiny > 0f);
        Assert.True(rHuge > 0f);
        Assert.True(rTiny <= rHuge);
    }
}
