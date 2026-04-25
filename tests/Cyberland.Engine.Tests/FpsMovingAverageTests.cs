using Cyberland.Engine;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class FpsMovingAverageTests
{
    [Fact]
    public void TryGet_is_false_until_first_sample()
    {
        var m = new FpsMovingAverage(0.5f);
        Assert.False(m.TryGetAverageFps(out _));
    }

    [Fact]
    public void Zero_or_negative_delta_is_ignored()
    {
        var m = new FpsMovingAverage(0.5f);
        m.AddFrameDeltaSeconds(0f);
        m.AddFrameDeltaSeconds(-0.1f);
        Assert.False(m.TryGetAverageFps(out _));
    }

    [Fact]
    public void Single_frame_reports_inverse_delta_as_fps()
    {
        var m = new FpsMovingAverage(0.5f);
        m.AddFrameDeltaSeconds(0.1f);
        Assert.True(m.TryGetAverageFps(out var fps));
        Assert.Equal(10f, fps, 4);
    }

    [Fact]
    public void Multiple_equal_deltas_average_to_same_rate()
    {
        var m = new FpsMovingAverage(0.5f);
        for (var i = 0; i < 10; i++)
            m.AddFrameDeltaSeconds(0.01f);
        Assert.True(m.TryGetAverageFps(out var fps));
        Assert.Equal(100f, fps, 1);
    }

    [Fact]
    public void Window_drops_oldest_frames_when_sum_exceeds_window()
    {
        var m = new FpsMovingAverage(0.1f);
        for (var i = 0; i < 20; i++)
            m.AddFrameDeltaSeconds(0.01f);
        Assert.True(m.TryGetAverageFps(out var fps));
        // ~0.1s of 0.01s frames => ~10 samples => ~100 fps
        Assert.InRange(fps, 80f, 120f);
    }

    [Fact]
    public void One_long_frame_keeps_one_sample_when_larger_than_window()
    {
        var m = new FpsMovingAverage(0.5f);
        m.AddFrameDeltaSeconds(2f);
        Assert.True(m.TryGetAverageFps(out var fps));
        Assert.Equal(0.5f, fps, 4);
    }

    [Fact]
    public void WindowSeconds_can_be_changed()
    {
        var m = new FpsMovingAverage(0.1f) { WindowSeconds = 1f };
        m.AddFrameDeltaSeconds(0.1f);
        m.AddFrameDeltaSeconds(0.1f);
        Assert.True(m.TryGetAverageFps(out var fps));
        Assert.Equal(10f, fps, 2);
    }
}
