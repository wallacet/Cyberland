using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;

namespace Cyberland.Engine.Tests;

public sealed class EngineDefaultSchedulerSystemsTests
{
    [Fact]
    public void RegisterBeforeGameplayMods_registers_expected_logical_ids()
    {
        var (_, scheduler) = CreateContext();

        EngineDefaultSchedulerSystems.RegisterBeforeGameplayMods(CreateContext(scheduler));

        Assert.True(scheduler.SetEnabled("cyberland.engine/transform2d", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/sprite-animation", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/particle-sim", true));
    }

    [Fact]
    public void RegisterAfterGameplayMods_registers_expected_logical_ids()
    {
        var (_, scheduler) = CreateContext();

        EngineDefaultSchedulerSystems.RegisterAfterGameplayMods(CreateContext(scheduler));

        Assert.True(scheduler.SetEnabled("cyberland.engine/camera-follow", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/camera-submit", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/audio-listener", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/audio-session", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/global-audio-environment", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/audio-environment-volumes", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/audio-emitters", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/music", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/sprite-render", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/text-render", true));
        Assert.True(scheduler.SetEnabled("cyberland.engine/ui-command-drain", true));
    }

    [Fact]
    public void RegisterBeforeGameplayMods_throws_when_context_null() =>
        Assert.Throws<ArgumentNullException>(() => EngineDefaultSchedulerSystems.RegisterBeforeGameplayMods(null!));

    [Fact]
    public void RegisterAfterGameplayMods_throws_when_context_null() =>
        Assert.Throws<ArgumentNullException>(() => EngineDefaultSchedulerSystems.RegisterAfterGameplayMods(null!));

    [Fact]
    public void RegisterBeforeGameplayMods_with_progress_phase_reports_monotonic_load_progress()
    {
        var (_, scheduler) = CreateContext();
        var ctx = CreateContext(scheduler);
        EngineDefaultSchedulerSystems.RegisterBeforeGameplayMods(ctx, "engine-early-register");
        var snap = ctx.Host.StartupProgress.Snapshot();
        Assert.Equal("test.mod", snap.ActiveOwner);
        Assert.Equal(1f, snap.ReportedProgress01, 5);
    }

    [Fact]
    public void RegisterAfterGameplayMods_with_progress_phase_reports_monotonic_load_progress()
    {
        var (_, scheduler) = CreateContext();
        var ctx = CreateContext(scheduler);
        EngineDefaultSchedulerSystems.RegisterAfterGameplayMods(ctx, "engine-late-register");
        var snap = ctx.Host.StartupProgress.Snapshot();
        Assert.Equal("test.mod", snap.ActiveOwner);
        Assert.Equal(1f, snap.ReportedProgress01, 5);
    }

    private static (ModLoadContext Context, SystemScheduler Scheduler) CreateContext()
    {
        var scheduler = new SystemScheduler(new ParallelismSettings());
        return (CreateContext(scheduler), scheduler);
    }

    private static ModLoadContext CreateContext(SystemScheduler scheduler)
    {
        var vfs = new VirtualFileSystem();
        var localized = new LocalizedContent(new LocalizationManager(), vfs, "en");
        return new ModLoadContext(
            new ModManifest { Id = "test.mod", ContentRoot = "Content" },
            Path.GetTempPath(),
            vfs,
            localized,
            new World(),
            scheduler,
            new GameHostServices());
    }
}
