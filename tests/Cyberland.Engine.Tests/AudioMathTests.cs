using Cyberland.Engine.Audio;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class AudioMathTests
{
    [Fact]
    public void DistanceGain_at_or_below_ref_is_one()
    {
        Assert.Equal(1f, AudioAttenuation.DistanceGain(0f, 64f, 480f, 1f));
        Assert.Equal(1f, AudioAttenuation.DistanceGain(64f, 64f, 480f, 1f));
    }

    [Fact]
    public void DistanceGain_at_or_beyond_max_is_zero()
    {
        Assert.Equal(0f, AudioAttenuation.DistanceGain(480f, 64f, 480f, 1f));
        Assert.Equal(0f, AudioAttenuation.DistanceGain(999f, 64f, 480f, 1f));
    }

    [Fact]
    public void DistanceGain_midpoint_is_half_with_linear_rolloff()
    {
        var mid = 64f + (480f - 64f) * 0.5f;
        Assert.Equal(0.5f, AudioAttenuation.DistanceGain(mid, 64f, 480f, 1f), 3);
    }

    [Fact]
    public void StereoPan_right_of_listener_is_positive()
    {
        var pan = AudioAttenuation.StereoPan(
            new Vector2D<float>(100f, 0f),
            new Vector2D<float>(0f, 0f),
            0f,
            100f);
        Assert.Equal(1f, pan, 3);
    }

    [Fact]
    public void Fade_in_and_out_complete()
    {
        var e = AudioFade.AdvanceFadeIn(0f, 1f, 0.5f, out var done);
        Assert.False(done);
        Assert.Equal(0.5f, e, 3);
        e = AudioFade.AdvanceFadeIn(e, 1f, 0.5f, out done);
        Assert.True(done);
        Assert.Equal(1f, e);

        e = AudioFade.AdvanceFadeOut(1f, 1f, 0.5f, out done);
        Assert.False(done);
        e = AudioFade.AdvanceFadeOut(e, 1f, 0.5f, out done);
        Assert.True(done);
        Assert.Equal(0f, e);
    }

    [Fact]
    public void BlendToward_reaches_target()
    {
        Assert.Equal(1f, AudioFade.BlendToward(0f, 1f, 0f, 1f));
        Assert.Equal(0.5f, AudioFade.BlendToward(0f, 1f, 1f, 0.5f), 3);
    }

    [Fact]
    public void GainCompose_multiplies_stages()
    {
        var g = AudioGainMath.Compose(1f, 0.5f, 1f, 0.5f, 1f, 1f, 1f, 1f, 1f);
        Assert.Equal(0.25f, g, 3);
    }

    [Fact]
    public void FocusFactor_mute_and_duck()
    {
        Assert.Equal(1f, AudioGainMath.FocusFactor(AudioFocusPolicy.Ignore, false));
        Assert.Equal(0f, AudioGainMath.FocusFactor(AudioFocusPolicy.MuteMaster, false));
        Assert.Equal(0.2f, AudioGainMath.FocusFactor(AudioFocusPolicy.DuckMaster(0.2f), false), 3);
        Assert.Equal(1f, AudioGainMath.FocusFactor(AudioFocusPolicy.MuteMaster, true));
    }

    [Fact]
    public void VoiceLimit_allows_under_cap()
    {
        var c = new[] { new VoiceLimitLogic.VoiceCandidate(0, 1, 0, "sfx") };
        Assert.Equal(-1, VoiceLimitLogic.ChooseSteal(c, 1, 4, VoiceStealMode.StealLowestPriority, 0));
    }

    [Fact]
    public void VoiceLimit_fail_mode_drops()
    {
        var c = new[]
        {
            new VoiceLimitLogic.VoiceCandidate(0, 1, 0, "sfx"),
            new VoiceLimitLogic.VoiceCandidate(1, 2, 1, "sfx"),
        };
        Assert.Equal(-2, VoiceLimitLogic.ChooseSteal(c, 2, 2, VoiceStealMode.Fail, 99));
    }

    [Fact]
    public void VoiceLimit_steals_lowest_priority()
    {
        var c = new[]
        {
            new VoiceLimitLogic.VoiceCandidate(10, 5, 0, "sfx"),
            new VoiceLimitLogic.VoiceCandidate(11, 1, 1, "sfx"),
        };
        Assert.Equal(11, VoiceLimitLogic.ChooseSteal(c, 2, 2, VoiceStealMode.StealLowestPriority, 3));
    }

    [Fact]
    public void Cooldown_blocks_then_allows()
    {
        Assert.False(VoiceLimitLogic.CooldownAllows(1.0, 0.95, 0.2f));
        Assert.True(VoiceLimitLogic.CooldownAllows(1.2, 0.95, 0.2f));
    }

    [Fact]
    public void CueVariation_round_robin_cycles()
    {
        var cursor = 0;
        Assert.Equal(0, AudioCueVariation.PickIndex(3, AudioCueVariation.PickMode.RoundRobin, ref cursor, Random.Shared));
        Assert.Equal(1, AudioCueVariation.PickIndex(3, AudioCueVariation.PickMode.RoundRobin, ref cursor, Random.Shared));
        Assert.Equal(2, AudioCueVariation.PickIndex(3, AudioCueVariation.PickMode.RoundRobin, ref cursor, Random.Shared));
        Assert.Equal(0, AudioCueVariation.PickIndex(3, AudioCueVariation.PickMode.RoundRobin, ref cursor, Random.Shared));
    }

    [Fact]
    public void CueVariation_sample_range_clamps()
    {
        var v = AudioCueVariation.SampleRange(0.9f, 1.1f, new Random(1));
        Assert.InRange(v, 0.9f, 1.1f);
    }
}
