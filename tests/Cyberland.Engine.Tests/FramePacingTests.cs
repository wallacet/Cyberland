using Cyberland.Engine.Rendering;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class FramePacingTests
{
    [Fact]
    public void Limited_factory_accepts_bounds()
    {
        Assert.Equal(1, FramePacing.Limited(1).TargetFps);
        Assert.Equal(1000, FramePacing.Limited(1000).TargetFps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Limited_factory_rejects_out_of_range(int fps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FramePacing.Limited(fps));
    }

    [Fact]
    public void Constructor_validates_Limited_target()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FramePacing(FramePacingMode.Limited, 0));
    }

    [Fact]
    public void Equality_matches_mode_and_target()
    {
        var a = FramePacing.Limited(60);
        var b = FramePacing.Limited(60);
        var c = FramePacing.Limited(120);
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a.Equals(c));
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(FramePacing.VSync.Equals(FramePacing.Limited(60)));
    }

    [Fact]
    public void Equals_object_accepts_boxed_FramePacing_and_rejects_other_types()
    {
        var a = FramePacing.Limited(60);
        object boxed = FramePacing.Limited(60);
        Assert.True(a.Equals(boxed));
        Assert.False(a.Equals("not-a-frame-pacing"));
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void VSync_and_Unlimited_ignore_TargetFps_field()
    {
        Assert.Equal(0, FramePacing.VSync.TargetFps);
        Assert.Equal(FramePacingMode.VSync, new FramePacing(FramePacingMode.VSync, 999).Mode);
    }
}
