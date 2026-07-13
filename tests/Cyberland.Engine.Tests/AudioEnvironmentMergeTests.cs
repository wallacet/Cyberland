using Cyberland.Engine.Audio;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class AudioEnvironmentMergeTests
{
    [Fact]
    public void Resolve_returns_global_when_outside_volumes()
    {
        var global = AudioEnvironmentSettings.Default;
        global.LowPassHz = 18000f;
        var volumes = new[]
        {
            new AudioEnvironmentVolumeSubmission(
                new AudioEnvironmentVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(10f, 10f),
                    Priority = 1,
                    Overrides = new AudioEnvironmentOverrides { HasLowPassHz = true, LowPassHz = 500f },
                },
                new Vector2D<float>(100f, 100f),
                0f,
                new Vector2D<float>(1f, 1f)),
        };
        var r = AudioEnvironmentMerge.ResolveAtPoint(global, volumes, new Vector2D<float>(0f, 0f));
        Assert.Equal(18000f, r.LowPassHz, 1);
    }

    [Fact]
    public void Resolve_applies_highest_priority_volume()
    {
        var global = AudioEnvironmentSettings.Default;
        var volumes = new[]
        {
            new AudioEnvironmentVolumeSubmission(
                new AudioEnvironmentVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(50f, 50f),
                    Priority = 1,
                    Overrides = new AudioEnvironmentOverrides
                    {
                        HasLowPassHz = true,
                        LowPassHz = 2000f,
                        BusGains = new[] { new AudioBusGainEntry { BusId = "music", Gain = 1.2f } },
                    },
                },
                new Vector2D<float>(0f, 0f),
                0f,
                new Vector2D<float>(1f, 1f)),
            new AudioEnvironmentVolumeSubmission(
                new AudioEnvironmentVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(50f, 50f),
                    Priority = 5,
                    Overrides = new AudioEnvironmentOverrides
                    {
                        HasLowPassHz = true,
                        LowPassHz = 800f,
                        BusGains = new[] { new AudioBusGainEntry { BusId = "music", Gain = 0.5f } },
                    },
                },
                new Vector2D<float>(0f, 0f),
                0f,
                new Vector2D<float>(1f, 1f)),
        };
        var r = AudioEnvironmentMerge.ResolveAtPoint(global, volumes, new Vector2D<float>(0f, 0f));
        Assert.Equal(800f, r.LowPassHz, 1);
        Assert.Equal(0.5f, AudioEnvironmentMerge.GetBusMultiplier(r, "music"), 3);
    }

    [Fact]
    public void Blend_lerps_low_pass()
    {
        var a = AudioEnvironmentSettings.Default;
        a.LowPassHz = 1000f;
        var b = AudioEnvironmentSettings.Default;
        b.LowPassHz = 3000f;
        var m = AudioEnvironmentMerge.Blend(a, b, 0.5f);
        Assert.Equal(2000f, m.LowPassHz, 1);
    }

    [Fact]
    public void GetBusMultiplier_defaults_to_one()
    {
        Assert.Equal(1f, AudioEnvironmentMerge.GetBusMultiplier(AudioEnvironmentSettings.Default, "sfx"));
    }
}
