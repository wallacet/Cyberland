using Cyberland.Engine.Audio;

namespace Cyberland.Engine.Tests;

public sealed class AudioDuckMixerTests
{
    [Fact]
    public void Duck_attacks_toward_gain_when_trigger_active()
    {
        var m = new AudioDuckMixer();
        m.Register(new AudioDuckRule("dialogue", "music", 0.35f, attackSeconds: 0.1f, releaseSeconds: 0.4f));
        m.Tick(0.1f, _ => true);
        Assert.True(m.GetMultiplier("music") < 0.99f);
        for (var i = 0; i < 20; i++)
            m.Tick(0.1f, _ => true);
        Assert.Equal(0.35f, m.GetMultiplier("music"), 2);
    }

    [Fact]
    public void Duck_releases_when_idle()
    {
        var m = new AudioDuckMixer();
        m.Register(new AudioDuckRule("dialogue", "music", 0.35f, 0.01f, 0.1f));
        for (var i = 0; i < 10; i++)
            m.Tick(0.05f, _ => true);
        for (var i = 0; i < 30; i++)
            m.Tick(0.05f, _ => false);
        Assert.Equal(1f, m.GetMultiplier("music"), 2);
    }

    [Fact]
    public void Clear_resets()
    {
        var m = new AudioDuckMixer();
        m.Register(new AudioDuckRule("a", "b", 0.5f, 0.01f, 0.01f));
        m.Tick(1f, _ => true);
        m.Clear();
        Assert.Equal(1f, m.GetMultiplier("b"));
    }
}
