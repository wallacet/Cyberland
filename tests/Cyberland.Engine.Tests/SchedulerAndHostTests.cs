using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class SchedulerAndHostTests
{
    [Fact]
    public void ParallelismSettings_zero_uses_processor_count()
    {
        var p = new ParallelismSettings { MaxConcurrency = 0 };
        var o = p.CreateParallelOptions();
        Assert.Equal(Environment.ProcessorCount, o.MaxDegreeOfParallelism);
    }

    [Fact]
    public void ParallelismSettings_positive_caps_parallelism()
    {
        var p = new ParallelismSettings { MaxConcurrency = 2 };
        Assert.Equal(2, p.CreateParallelOptions().MaxDegreeOfParallelism);
    }

    private sealed class TrackSeq : ISystem
    {
        public int Step;
        public void OnUpdate(World world, float deltaSeconds) => Step = 1;
    }

    private sealed class TrackPar : IParallelSystem
    {
        public int Step;
        public void OnParallelUpdate(World world, ParallelOptions parallelOptions) => Step = 2;
    }

    [Fact]
    public void SystemScheduler_runs_sequential_before_parallel()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var s = new TrackSeq();
        var p = new TrackPar();
        sched.Register(s);
        sched.Register(p);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.Step);
        Assert.Equal(2, p.Step);
    }

    [Fact]
    public void GameHostServices_exposes_assignable_renderer_and_input()
    {
        var keys = new KeyBindingStore();
        var host = new GameHostServices(keys);
        Assert.Same(keys, host.KeyBindings);
        Assert.Null(host.Renderer);
        Assert.Null(host.Input);
        host.Renderer = null;
        host.Input = null;
    }
}
