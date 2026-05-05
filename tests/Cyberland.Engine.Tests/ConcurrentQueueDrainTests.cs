using System.Collections.Concurrent;
using Cyberland.Engine.Rendering;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class ConcurrentQueueDrainTests
{
    [Fact]
    public void DrainToScratch_empty_queue_yields_zero_and_empty_array()
    {
        var q = new ConcurrentQueue<int>();
        int[]? scratch = null;
        var n = ConcurrentQueueDrain.DrainToScratch(q, ref scratch, out var result);
        Assert.Equal(0, n);
        Assert.Same(Array.Empty<int>(), result);
    }

    [Fact]
    public void DrainToScratch_dequeues_all_elements_even_when_scratch_starts_undersized()
    {
        var q = new ConcurrentQueue<int>();
        for (var i = 0; i < 200; i++)
            q.Enqueue(i);

        int[]? scratch = new int[3];
        var n = ConcurrentQueueDrain.DrainToScratch(q, ref scratch, out var result);
        Assert.Equal(200, n);
        Assert.Empty(q);
        for (var i = 0; i < 200; i++)
            Assert.Equal(i, result[i]);
    }
}
