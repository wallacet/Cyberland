using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;

namespace Cyberland.Engine.Tests;

public sealed class StartupProgressTrackerTests
{
    [Fact]
    public void Weighted_progress_aggregates_and_is_monotonic_per_phase()
    {
        var tracker = new StartupProgressTracker();
        using var a = tracker.BeginPhase("a", 1f, "A");
        using var b = tracker.BeginPhase("b", 3f, "B");

        tracker.ReportPhaseProgress("a", 1f);
        tracker.ReportPhaseProgress("b", 0.50f);
        tracker.ReportPhaseProgress("b", 0.10f); // ignored (non-monotonic)

        var snapshot = tracker.Snapshot();
        Assert.Equal(0.625f, snapshot.ReportedProgress01, 3);
        Assert.Equal(2, snapshot.PhaseCount);
    }

    [Fact]
    public void Display_progress_catches_up_without_overshooting_reported()
    {
        var tracker = new StartupProgressTracker();
        using var phase = tracker.BeginPhase("phase", 1f);
        tracker.ReportPhaseProgress("phase", 0.4f);

        tracker.AdvanceDisplay(deltaSeconds: 1f, maxCatchupPerSecond: 0.2f);
        var one = tracker.Snapshot();
        Assert.Equal(0.2f, one.DisplayProgress01, 3);

        tracker.AdvanceDisplay(deltaSeconds: 10f, maxCatchupPerSecond: 0.2f);
        var two = tracker.Snapshot();
        Assert.Equal(0.4f, two.DisplayProgress01, 3);
    }

    [Fact]
    public void ModLoadContext_progress_reports_scoped_mod_owner()
    {
        var vfs = new VirtualFileSystem();
        var host = new GameHostServices();
        var localized = new LocalizedContent(new LocalizationManager(), vfs, "en");
        var context = new ModLoadContext(
            new ModManifest { Id = "demo.mod", ContentRoot = "Content" },
            Path.GetTempPath(),
            vfs,
            localized,
            new World(),
            new SystemScheduler(new ParallelismSettings()),
            host);

        using (context.BeginLoadPhase("bootstrap", 1f, "Bootstrap"))
        {
            context.ReportLoadProgress("bootstrap", 0.6f, "Bootstrap");
            var mid = host.StartupProgress.Snapshot();
            Assert.Equal("demo.mod", mid.ActiveOwner);
            Assert.Equal("Bootstrap", mid.ActiveLabel);
            Assert.True(mid.ReportedProgress01 >= 0.6f);
        }

        var done = host.StartupProgress.Snapshot();
        Assert.Equal(1f, done.ReportedProgress01, 3);
    }

    [Fact]
    public void Reset_clears_phases_and_display()
    {
        var tracker = new StartupProgressTracker();
        using (tracker.BeginPhase("a", 1f))
            tracker.ReportPhaseProgress("a", 0.5f);
        tracker.AdvanceDisplay(1f, 1f);
        tracker.Reset();
        var snap = tracker.Snapshot();
        Assert.Equal(0, snap.PhaseCount);
        Assert.Equal(0f, snap.ReportedProgress01);
        Assert.Equal(0f, snap.DisplayProgress01);
    }

    [Fact]
    public void BeginPhase_clamps_non_positive_weight()
    {
        var tracker = new StartupProgressTracker();
        using (tracker.BeginPhase("w", -5f))
        {
            tracker.ReportPhaseProgress("w", 1f);
        }

        var snap = tracker.Snapshot();
        Assert.Equal(1f, snap.ReportedProgress01, 5);
    }

    [Fact]
    public void ReportPhaseProgress_creates_implicit_weighted_phase_when_missing()
    {
        var tracker = new StartupProgressTracker();
        tracker.ReportPhaseProgress("orphan", 0.25f, "Orphan", "host");
        var snap = tracker.Snapshot();
        Assert.Equal(0.25f, snap.ReportedProgress01, 5);
        Assert.Equal("host", snap.ActiveOwner);
    }

    [Fact]
    public void BeginPhase_reuse_updates_label_and_owner_when_provided()
    {
        var tracker = new StartupProgressTracker();
        using (tracker.BeginPhase("k", 1f, "L1", "O1"))
        {
        }

        using (tracker.BeginPhase("k", 1f, "L2", "O2"))
        {
            var mid = tracker.Snapshot();
            Assert.Equal("L2", mid.ActiveLabel);
            Assert.Equal("O2", mid.ActiveOwner);
        }
    }

    [Fact]
    public void AdvanceDisplay_clamps_negative_delta_and_non_positive_catchup()
    {
        var tracker = new StartupProgressTracker();
        tracker.ReportPhaseProgress("p", 1f);
        tracker.AdvanceDisplay(-1f, maxCatchupPerSecond: 0f);
        var snap = tracker.Snapshot();
        Assert.Equal(1f, snap.ReportedProgress01, 5);
        Assert.True(snap.DisplayProgress01 >= 0f);
    }

    [Fact]
    public void MarkComplete_pins_all_phases_and_display()
    {
        var tracker = new StartupProgressTracker();
        using (tracker.BeginPhase("a", 2f))
            tracker.ReportPhaseProgress("a", 0.1f);
        tracker.MarkComplete();
        var snap = tracker.Snapshot();
        Assert.Equal(1f, snap.ReportedProgress01, 5);
        Assert.Equal(1f, snap.DisplayProgress01, 5);
    }

    [Fact]
    public void Phase_scope_dispose_is_idempotent()
    {
        var tracker = new StartupProgressTracker();
        var scope = tracker.BeginPhase("x", 1f);
        scope.Dispose();
        scope.Dispose();
        var snap = tracker.Snapshot();
        Assert.Equal(1f, snap.ReportedProgress01, 5);
    }

    [Fact]
    public void AdvanceDisplay_with_no_phases_is_safe()
    {
        var tracker = new StartupProgressTracker();
        tracker.AdvanceDisplay(0.016f, 0.5f);
        var snap = tracker.Snapshot();
        Assert.Equal(0f, snap.ReportedProgress01);
        Assert.Equal(0f, snap.DisplayProgress01);
    }
}
