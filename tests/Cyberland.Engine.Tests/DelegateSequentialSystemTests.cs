using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Engine.Tests;

public sealed class DelegateSequentialSystemTests
{
    [Fact]
    public void OnLateUpdate_invokes_delegate()
    {
        var w = new World();
        var n = 0;
        var s = new DelegateSequentialSystem(onLateUpdate: (world, _, dt) =>
        {
            Assert.Same(w, world);
            Assert.Equal(0.05f, dt);
            n++;
        });
        s.OnLateUpdate(w, w.QueryChunks(SystemQuerySpec.Empty), 0.05f);
        Assert.Equal(1, n);
    }

    [Fact]
    public void Constructor_throws_when_all_delegates_null()
    {
        Assert.Throws<ArgumentException>(() => new DelegateSequentialSystem());
    }

    [Fact]
    public void Optional_onStart_invoked_by_scheduler()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var n = 0;
        var s = new DelegateSequentialSystem(
            onLateUpdate: (_, _, _) => { },
            onStart: (_, _) => n++);
        sched.RegisterSequential("x", s);
        sched.RunFrame(new World(), 0.05f);
        Assert.Equal(1, n);
    }
}
