using Cyberland.Engine.Audio;

namespace Cyberland.Engine.Tests;

public sealed class AudioBusRegistryTests
{
    [Fact]
    public void Stock_buses_registered()
    {
        var r = new AudioBusRegistry();
        r.RegisterStockBuses();
        Assert.True(r.Contains(AudioBusIds.Master));
        Assert.True(r.Contains(AudioBusIds.Ui));
        Assert.Equal(6, r.Count);
    }

    [Fact]
    public void Register_set_get_mute()
    {
        var r = new AudioBusRegistry();
        r.Register("dialogue", new BusRegistration { DefaultGain = 0.8f });
        Assert.Equal(0.8f, r.GetVolume("dialogue"), 3);
        r.SetVolume("dialogue", 0.5f);
        Assert.Equal(0.5f, r.GetVolume("dialogue"), 3);
        r.SetMuted("dialogue", true);
        Assert.True(r.IsMuted("dialogue"));
        Assert.Equal(0f, r.GetEffectiveBusGain("dialogue"));
        r.SetMuted("dialogue", false);
        Assert.Equal(0.5f, r.GetEffectiveBusGain("dialogue"), 3);
    }

    [Fact]
    public void Unknown_bus_gain_is_one()
    {
        var r = new AudioBusRegistry();
        Assert.Equal(1f, r.GetVolume("nope"));
        Assert.Equal(1f, r.GetEffectiveBusGain("nope"));
        Assert.False(r.IsMuted("nope"));
    }

    [Fact]
    public void CopyBusIds_lists_registered()
    {
        var r = new AudioBusRegistry();
        r.Register("a", BusRegistration.Default);
        r.Register("b", BusRegistration.Default);
        var buf = new string[8];
        var n = r.CopyBusIds(buf);
        Assert.Equal(2, n);
        Assert.Contains("a", buf.Take(n));
        Assert.Contains("b", buf.Take(n));
    }

    [Fact]
    public void Polyphony_settings_round_trip()
    {
        var r = new AudioBusRegistry();
        r.Register("weapons", new BusRegistration
        {
            DefaultGain = 1f,
            MaxVoices = 4,
            StealMode = VoiceStealMode.Fail,
        });
        Assert.True(r.TryGetPolyphony("weapons", out var max, out var mode));
        Assert.Equal(4, max);
        Assert.Equal(VoiceStealMode.Fail, mode);
    }
}
