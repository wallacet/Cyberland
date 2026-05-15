using Cyberland.Engine.Hosting;

namespace Cyberland.Engine.Tests;

public sealed class StartupSplashPresenterTests
{
    [Fact]
    public void Constructor_uses_default_when_screen_list_empty()
    {
        var p = new StartupSplashPresenter(Array.Empty<StartupSplashScreen>());
        Assert.False(p.IsCompleted);
        Assert.Equal("Loading", p.Current.Label);
    }

    [Fact]
    public void CreateDefault_has_three_cards_and_advances_by_duration()
    {
        var p = StartupSplashPresenter.CreateDefault();
        Assert.Equal("Booting", p.Current.Label);
        p.Advance(10d, canSkip: false, skipRequested: false);
        Assert.False(p.IsCompleted);
        Assert.Equal("Loading Runtime", p.Current.Label);
    }

    [Fact]
    public void Skip_advances_one_screen_when_canSkip()
    {
        var p = StartupSplashPresenter.CreateDefault();
        p.Advance(0d, canSkip: true, skipRequested: true);
        Assert.Equal("Loading Runtime", p.Current.Label);
    }

    [Fact]
    public void Skip_ignored_when_canSkip_false()
    {
        var p = StartupSplashPresenter.CreateDefault();
        p.Advance(0d, canSkip: false, skipRequested: true);
        Assert.Equal("Booting", p.Current.Label);
    }

    [Fact]
    public void Advance_is_noop_when_completed()
    {
        var p = new StartupSplashPresenter([new StartupSplashScreen("One", 0.05f, 0f, 0f, 0f)]);
        p.Advance(1d, canSkip: false, skipRequested: false);
        Assert.True(p.IsCompleted);
        p.Advance(1d, canSkip: true, skipRequested: true);
        Assert.True(p.IsCompleted);
    }

    [Fact]
    public void Current_returns_last_card_when_sequence_finished()
    {
        var p = new StartupSplashPresenter([new StartupSplashScreen("Only", 0.05f, 0.1f, 0.2f, 0.3f)]);
        p.Advance(1d, canSkip: false, skipRequested: false);
        Assert.True(p.IsCompleted);
        Assert.Equal("Only", p.Current.Label);
        Assert.Equal(0.1f, p.Current.R);
    }

    [Fact]
    public void StartupSplashScreen_Default_has_expected_label()
    {
        var d = StartupSplashScreen.Default;
        Assert.Equal("Loading", d.Label);
        Assert.Equal(0f, d.G);
        Assert.Equal(0f, d.B);
    }

    [Fact]
    public void StartupSplashScreen_exposes_rgb_channels()
    {
        var s = new StartupSplashScreen("rgb", 1f, 0.1f, 0.4f, 0.7f);
        Assert.Equal(0.4f, s.G);
        Assert.Equal(0.7f, s.B);
    }
}
