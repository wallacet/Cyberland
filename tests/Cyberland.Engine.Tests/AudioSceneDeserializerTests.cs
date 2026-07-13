using System.Text.Json;
using Cyberland.Engine.Audio;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Engine.Tests;

public sealed class AudioSceneDeserializerTests
{
    [Fact]
    public void ReadAudioEnvironmentSettings_parses_bus_gains()
    {
        using var doc = JsonDocument.Parse("""
            {
              "blendSeconds": 0.5,
              "lowPassHz": 1200,
              "masterScale": 0.9,
              "busGains": [
                { "bus": "music", "gain": 1.2 },
                { "bus": "dialogue", "gain": 0.4 }
              ]
            }
            """);
        var s = EngineSceneComponentDeserializers.ReadAudioEnvironmentSettings(doc.RootElement);
        Assert.Equal(0.5f, s.BlendSeconds, 3);
        Assert.Equal(1200f, s.LowPassHz, 1);
        Assert.Equal(0.9f, s.MasterScale, 3);
        Assert.NotNull(s.BusGains);
        Assert.Equal(1.2f, AudioEnvironmentMerge.GetBusMultiplier(s, "music"), 3);
        Assert.Equal(0.4f, AudioEnvironmentMerge.GetBusMultiplier(s, "dialogue"), 3);
    }

    [Fact]
    public void ReadAudioEnvironmentOverrides_sets_has_flags()
    {
        using var doc = JsonDocument.Parse("""
            { "lowPassHz": 800, "busGains": [ { "bus": "ambient", "gain": 0.7 } ] }
            """);
        var o = EngineSceneComponentDeserializers.ReadAudioEnvironmentOverrides(doc.RootElement);
        Assert.True(o.HasLowPassHz);
        Assert.Equal(800f, o.LowPassHz, 1);
        Assert.False(o.HasBlendSeconds);
        Assert.NotNull(o.BusGains);
    }
}
