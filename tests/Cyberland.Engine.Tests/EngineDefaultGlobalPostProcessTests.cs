using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Tests;

public sealed class EngineDefaultGlobalPostProcessTests
{
    [Fact]
    public void DefaultSettings_matches_engine_baseline()
    {
        var s = EngineDefaultGlobalPostProcess.DefaultSettings;
        Assert.True(s.BloomEnabled);
        Assert.Equal(1.1f, s.BloomRadius);
        Assert.Equal(0.28f, s.BloomGain);
        Assert.Equal(0.32f, s.BloomExtractThreshold);
        Assert.Equal(0.5f, s.BloomExtractKnee);
        Assert.Equal(0.45f, s.EmissiveToHdrGain);
        Assert.Equal(0.45f, s.EmissiveToBloomGain);
        Assert.Equal(1f, s.Exposure);
        Assert.Equal(1.04f, s.Saturation);
        Assert.True(s.TonemapEnabled);
        Assert.Equal(1f, s.ColorGradingShadows.X);
        Assert.Equal(1f, s.ColorGradingMidtones.Y);
        Assert.Equal(1f, s.ColorGradingHighlights.Z);
    }

    [Fact]
    public void Apply_sets_renderer_global()
    {
        var r = new RecordingRenderer();
        EngineDefaultGlobalPostProcess.Apply(r);
        Assert.NotNull(r.LastGlobal);
        var applied = r.LastGlobal!.Value;
        var expected = EngineDefaultGlobalPostProcess.DefaultSettings;
        Assert.Equal(expected.BloomEnabled, applied.BloomEnabled);
        Assert.Equal(expected.BloomRadius, applied.BloomRadius);
        Assert.Equal(expected.BloomGain, applied.BloomGain);
        Assert.Equal(expected.BloomExtractThreshold, applied.BloomExtractThreshold);
        Assert.Equal(expected.BloomExtractKnee, applied.BloomExtractKnee);
        Assert.Equal(expected.EmissiveToHdrGain, applied.EmissiveToHdrGain);
        Assert.Equal(expected.EmissiveToBloomGain, applied.EmissiveToBloomGain);
        Assert.Equal(expected.Exposure, applied.Exposure);
        Assert.Equal(expected.Saturation, applied.Saturation);
        Assert.Equal(expected.TonemapEnabled, applied.TonemapEnabled);
        Assert.Equal(expected.ColorGradingShadows, applied.ColorGradingShadows);
        Assert.Equal(expected.ColorGradingMidtones, applied.ColorGradingMidtones);
        Assert.Equal(expected.ColorGradingHighlights, applied.ColorGradingHighlights);
    }

    [Fact]
    public void Apply_null_is_noop()
    {
        EngineDefaultGlobalPostProcess.Apply(null);
    }
}
