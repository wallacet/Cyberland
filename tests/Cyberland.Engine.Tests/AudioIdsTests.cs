using Cyberland.Engine.Audio;

namespace Cyberland.Engine.Tests;

public sealed class AudioIdsTests
{
    [Fact]
    public void Clip_and_voice_ids_equality()
    {
        Assert.False(AudioClipId.Invalid.IsValid);
        Assert.False(VoiceId.None.IsValid);
        var a = new AudioClipId(3);
        var b = new AudioClipId(3);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Contains("3", a.ToString());

        var v = new VoiceId(7);
        Assert.True(v.IsValid);
        Assert.True(v == new VoiceId(7));
        Assert.True(v != VoiceId.None);
    }
}
