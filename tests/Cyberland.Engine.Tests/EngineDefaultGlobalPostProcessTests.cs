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
        Assert.Equal(1.04f, s.Saturation);
    }

    [Fact]
    public void Apply_sets_renderer_global()
    {
        var r = new RecordingRenderer();
        EngineDefaultGlobalPostProcess.Apply(r);
        Assert.NotNull(r.LastGlobal);
        Assert.True(r.LastGlobal!.Value.BloomEnabled);
    }

    [Fact]
    public void Apply_null_is_noop()
    {
        EngineDefaultGlobalPostProcess.Apply(null);
    }
}
