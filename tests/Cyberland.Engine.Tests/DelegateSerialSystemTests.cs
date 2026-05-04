using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Engine.Tests;

public sealed class DelegateSerialSystemTests
{
    [Fact]
    public void OnLateUpdate_invokes_delegate()
    {
        var w = new World();
        var n = 0;
        var s = new DelegateSerialSystem(onLateUpdate: (_, dt) =>
        {
            Assert.Equal(0.05f, dt);
            n++;
        });
        s.OnLateUpdate(w.QueryChunks(SystemQuerySpec.Empty), 0.05f);
        Assert.Equal(1, n);
    }

    [Fact]
    public void Constructor_throws_when_all_delegates_null()
    {
        Assert.Throws<ArgumentException>(() => new DelegateSerialSystem());
    }

    [Fact]
    public void Optional_onStart_invoked_by_scheduler()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var n = 0;
        var s = new DelegateSerialSystem(
            onLateUpdate: (_, _) => { },
            onStart: (_, _) => n++);
        sched.RegisterSerial("x", s);
        sched.RunFrame(new World(), 0.05f);
        Assert.Equal(1, n);
    }
}
