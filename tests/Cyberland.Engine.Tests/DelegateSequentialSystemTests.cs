using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class DelegateSequentialSystemTests
{
    [Fact]
    public void OnUpdate_invokes_delegate()
    {
        var w = new World();
        var n = 0;
        var s = new DelegateSequentialSystem((world, dt) =>
        {
            Assert.Same(w, world);
            Assert.Equal(0.05f, dt);
            n++;
        });
        s.OnUpdate(w, 0.05f);
        Assert.Equal(1, n);
    }

    [Fact]
    public void Constructor_throws_on_null_delegate()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegateSequentialSystem(null!));
    }

    [Fact]
    public void Optional_onStart_invoked_by_scheduler()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var n = 0;
        var s = new DelegateSequentialSystem(
            (_, _) => { },
            _ => n++);
        sched.RegisterSequential("x", s);
        sched.RunFrame(new World(), 0.05f);
        Assert.Equal(1, n);
    }
}
