using System.Text.Json;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.RuntimeScenes;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

[Collection("EngineDiagnostics")]
public sealed class EngineSceneGlobalPostProcessDeserializerTests
{
    private sealed class RecordingSink : IEngineDiagnosticSink
    {
        public readonly List<(EngineErrorSeverity Severity, string Title, string Message)> Calls = new();
        public void Deliver(EngineErrorSeverity severity, string title, string message) =>
            Calls.Add((severity, title, message));
    }

    [Fact]
    public void ClampLightColor_passes_positive_through()
    {
        var c = new Vector3D<float>(0.5f, 1f, 0.8f);
        var result = EngineSceneComponentDeserializers.ClampLightColor(c, "test");
        Assert.Equal(c, result);
    }

    [Fact]
    public void ClampLightColor_clamps_negative_channels_and_warns()
    {
        var sink = new RecordingSink();
        EngineDiagnostics.SinkOverride = sink;
        try
        {
            var c = new Vector3D<float>(-0.5f, 1f, -0.2f);
            var result = EngineSceneComponentDeserializers.ClampLightColor(c, "test-light");
            Assert.Equal(0f, result.X);
            Assert.Equal(1f, result.Y);
            Assert.Equal(0f, result.Z);
            Assert.True(sink.Calls.Count > 0);
            Assert.Contains("Negative light color", sink.Calls[0].Message);
        }
        finally
        {
            EngineDiagnostics.SinkOverride = null;
        }
    }


    [Fact]
    public void ReadGlobalPostProcessSettings_keeps_shadow_defaults_when_json_omits_shadows()
    {
        using var doc = JsonDocument.Parse("""{"bloomEnabled":false,"bloomGain":2.35}""");
        var settings = EngineSceneComponentDeserializers.ReadGlobalPostProcessSettings(doc.RootElement);
        Assert.False(settings.BloomEnabled);
        Assert.Equal(2.35f, settings.BloomGain);
        Assert.True(settings.Shadows.Enabled);
        Assert.Equal(ShadowSettings.Default.SdfScale, settings.Shadows.SdfScale);
        Assert.Equal(EmissivePromotionSettings.Default.EmissiveLightThreshold, settings.EmissivePromotion.EmissiveLightThreshold);
        Assert.Equal(EmissivePromotionSettings.Default.MaxPromotedLightsPerFrame, settings.EmissivePromotion.MaxPromotedLightsPerFrame);
    }

    [Fact]
    public void ReadShadowSettings_honors_flat_shadowsEnabled_and_nested_object()
    {
        var baseline = ShadowSettings.Default;
        using var flat = JsonDocument.Parse("""{"shadowsEnabled":false}""");
        Assert.False(EngineSceneComponentDeserializers.ReadShadowSettings(flat.RootElement, baseline).Enabled);

        using var nested = JsonDocument.Parse("""{"shadows":{"enabled":false,"sdfScale":0.5,"coneTraceSamples":24,"softShadowK":12.5,"depthBias":1.25,"directionalTraceWorldDist":1500}}""");
        var tuned = EngineSceneComponentDeserializers.ReadShadowSettings(nested.RootElement, baseline);
        Assert.False(tuned.Enabled);
        Assert.Equal(0.5f, tuned.SdfScale);
        Assert.Equal(24, tuned.ConeTraceSamples);
        Assert.Equal(12.5f, tuned.SoftShadowK);
        Assert.Equal(1.25f, tuned.DepthBias);
        Assert.Equal(1500f, tuned.DirectionalTraceWorldDist);
    }

    [Fact]
    public void ReadEmissivePromotionSettings_parses_from_emissivePromotion_section()
    {
        var baseline = EmissivePromotionSettings.Default;
        using var doc = JsonDocument.Parse("""{"emissivePromotion":{"emissiveLightThreshold":2.0,"maxPromotedLightsPerFrame":32,"emissivePromotionRadiusGain":4.0,"emissivePromotionIntensityGain":0.75}}""");
        var tuned = EngineSceneComponentDeserializers.ReadEmissivePromotionSettings(doc.RootElement, baseline);
        Assert.Equal(2.0f, tuned.EmissiveLightThreshold);
        Assert.Equal(32, tuned.MaxPromotedLightsPerFrame);
        Assert.Equal(4.0f, tuned.EmissivePromotionRadiusGain);
        Assert.Equal(0.75f, tuned.EmissivePromotionIntensityGain);
    }

    [Fact]
    public void ReadEmissivePromotionSettings_returns_baseline_when_section_missing()
    {
        var baseline = EmissivePromotionSettings.Default;
        using var doc = JsonDocument.Parse("""{}""");
        var result = EngineSceneComponentDeserializers.ReadEmissivePromotionSettings(doc.RootElement, baseline);
        Assert.Equal(baseline.EmissiveLightThreshold, result.EmissiveLightThreshold);
        Assert.Equal(baseline.MaxPromotedLightsPerFrame, result.MaxPromotedLightsPerFrame);
        Assert.Equal(baseline.EmissivePromotionRadiusGain, result.EmissivePromotionRadiusGain);
        Assert.Equal(baseline.EmissivePromotionIntensityGain, result.EmissivePromotionIntensityGain);
    }

    [Fact]
    public void ReadShadowSettings_throws_on_obsolete_atlas_keys()
    {
        var baseline = ShadowSettings.Default;
        using var doc = JsonDocument.Parse("""{"shadows":{"enabled":true,"atlasSize":2048}}""");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            EngineSceneComponentDeserializers.ReadShadowSettings(doc.RootElement, baseline));
        Assert.Contains("atlasSize", ex.Message, StringComparison.Ordinal);
        Assert.Contains("SDF", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("directionalTraceWorldDist", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadShadowSettings_throws_on_obsolete_filter_radius()
    {
        var baseline = ShadowSettings.Default;
        using var doc = JsonDocument.Parse("""{"shadows":{"filterRadius":0.5}}""");
        Assert.Throws<InvalidOperationException>(() =>
            EngineSceneComponentDeserializers.ReadShadowSettings(doc.RootElement, baseline));
    }

    [Fact]
    public void ReadShadowSettings_throws_on_obsolete_resolution_keys()
    {
        var baseline = ShadowSettings.Default;
        var obsoleteResolutionKeys = new[] { "directionalResolution", "spotResolution", "pointResolution" };
        foreach (var key in obsoleteResolutionKeys)
        {
            var json = "{\"shadows\":{\"" + key + "\":256}}";
            using var doc = JsonDocument.Parse(json);
            Assert.Throws<InvalidOperationException>(() =>
                EngineSceneComponentDeserializers.ReadShadowSettings(doc.RootElement, baseline));
        }
    }

    [Fact]
    public void ReadPostProcessOverrides_parses_all_fields()
    {
        using var doc = JsonDocument.Parse("""
        {
            "hasBloomGain": true,
            "bloomGain": 2.5,
            "hasBloomRadius": true,
            "bloomRadius": 1.5,
            "hasEmissiveToHdrGain": true,
            "emissiveToHdrGain": 0.8,
            "hasBloomSourceGain": true,
            "bloomSourceGain": 1.2,
            "hasExposure": true,
            "exposure": 0.9,
            "hasSaturation": true,
            "saturation": 1.1,
            "hasTonemapEnabled": true,
            "tonemapEnabled": false,
            "colorGradingShadowsOverride": {"x": 0.5, "y": 0.6, "z": 0.7},
            "colorGradingMidtonesOverride": {"x": 1.1, "y": 1.2, "z": 1.3},
            "colorGradingHighlightsOverride": {"x": 0.9, "y": 0.8, "z": 0.7},
            "hasBloomEnabled": true,
            "bloomEnabled": false,
            "hasBloomExtractThreshold": true,
            "bloomExtractThreshold": 0.6,
            "hasBloomExtractKnee": true,
            "bloomExtractKnee": 0.4,
            "hasShadows": true,
            "shadows": {"enabled": false, "sdfScale": 0.25}
        }
        """);

        var o = EngineSceneComponentDeserializers.ReadPostProcessOverrides(doc.RootElement);
        Assert.True(o.HasBloomGain);
        Assert.Equal(2.5f, o.BloomGain);
        Assert.True(o.HasBloomRadius);
        Assert.Equal(1.5f, o.BloomRadius);
        Assert.True(o.HasEmissiveToHdrGain);
        Assert.Equal(0.8f, o.EmissiveToHdrGain);
        Assert.True(o.HasBloomSourceGain);
        Assert.Equal(1.2f, o.BloomSourceGain);
        Assert.True(o.HasExposure);
        Assert.Equal(0.9f, o.Exposure);
        Assert.True(o.HasSaturation);
        Assert.Equal(1.1f, o.Saturation);
        Assert.True(o.HasTonemapEnabled);
        Assert.False(o.TonemapEnabled);
        Assert.True(o.HasColorGradingShadows);
        Assert.Equal(0.5f, o.ColorGradingShadows.X);
        Assert.True(o.HasColorGradingMidtones);
        Assert.Equal(1.2f, o.ColorGradingMidtones.Y);
        Assert.True(o.HasColorGradingHighlights);
        Assert.Equal(0.7f, o.ColorGradingHighlights.Z);
        Assert.True(o.HasBloomEnabled);
        Assert.False(o.BloomEnabled);
        Assert.True(o.HasBloomExtractThreshold);
        Assert.Equal(0.6f, o.BloomExtractThreshold);
        Assert.True(o.HasBloomExtractKnee);
        Assert.Equal(0.4f, o.BloomExtractKnee);
        Assert.True(o.HasShadows);
        Assert.False(o.Shadows.Enabled);
        Assert.Equal(0.25f, o.Shadows.SdfScale);
    }

    [Fact]
    public void ReadPostProcessOverrides_defaults_omitted_fields_to_no_override()
    {
        using var doc = JsonDocument.Parse("{}");
        var o = EngineSceneComponentDeserializers.ReadPostProcessOverrides(doc.RootElement);
        Assert.False(o.HasBloomGain);
        Assert.False(o.HasBloomRadius);
        Assert.False(o.HasEmissiveToHdrGain);
        Assert.False(o.HasBloomSourceGain);
        Assert.False(o.HasExposure);
        Assert.False(o.HasSaturation);
        Assert.False(o.HasShadows);
        Assert.False(o.HasTonemapEnabled);
        Assert.False(o.HasColorGradingShadows);
        Assert.False(o.HasColorGradingMidtones);
        Assert.False(o.HasColorGradingHighlights);
        Assert.False(o.HasBloomEnabled);
        Assert.False(o.HasBloomExtractThreshold);
        Assert.False(o.HasBloomExtractKnee);
    }
}
