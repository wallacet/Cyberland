using Cyberland.Engine.Audio;

namespace Cyberland.Engine.Tests;

public sealed class NullAudioServiceTests
{
    [Fact]
    public void Registers_and_enumerates_buses_without_playback()
    {
        IAudioService a = new NullAudioService();
        Assert.False(a.IsReady);
        a.RegisterBus("dialogue", 0.9f);
        a.SetBusVolume("dialogue", 0.5f);
        Assert.Equal(0.5f, a.GetBusVolume("dialogue"), 3);
        a.SetBusMuted("dialogue", true);
        Assert.True(a.IsBusMuted("dialogue"));
        Assert.Contains("master", a.EnumerateBuses());
        Assert.Contains("dialogue", a.EnumerateBuses());

        a.PlayOneShot(OneShotRequest.DefaultUi);
        Assert.Equal(VoiceId.None, a.PlayLoop(default));
        a.RegisterCue("x", new AudioCueDesc { ClipPaths = new[] { "Sounds/a.wav" } });
        a.PlayCue("x", default);
        a.GetStats(out var stats);
        Assert.False(stats.IsReady);
        Assert.True(stats.RegisteredBusCount >= 6);
    }

    [Fact]
    public async Task LoadClip_returns_invalid()
    {
        var a = new NullAudioService();
        var id = await a.LoadClipAsync("Sounds/missing.wav");
        Assert.False(id.IsValid);
        Assert.False(a.IsClipLoaded("Sounds/missing.wav"));
    }
}
