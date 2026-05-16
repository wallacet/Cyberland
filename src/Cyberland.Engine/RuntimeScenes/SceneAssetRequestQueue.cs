namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Priority asset job queue processed under <see cref="ISceneRuntime.PumpAsync"/> budgets.
/// </summary>
public sealed class SceneAssetRequestQueue
{
    internal sealed class Job : IComparable<Job>
    {
        public required int Priority;
        public required Action Action;
        public required int ByteBudgetHint;

        public int CompareTo(Job? other)
        {
            if (other is null)
                return 1;
            var c = Priority.CompareTo(other.Priority);
            return c != 0 ? c : 0;
        }
    }

    private readonly List<Job> _heap = new();
    private readonly object _gate = new();

    /// <summary>Enqueues work; lower <paramref name="priority"/> runs first.</summary>
    public void Enqueue(int priority, Action work, int byteBudgetHint = 0)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (_gate)
        {
            _heap.Add(new Job { Priority = priority, Action = work, ByteBudgetHint = byteBudgetHint });
            _heap.Sort();
        }
    }

    /// <summary>Runs up to <paramref name="maxJobs"/> jobs while decode bytes stay under <paramref name="maxDecodeBytes"/>.</summary>
    public int Drain(int maxJobs, int maxDecodeBytes)
    {
        var jobs = 0;
        var bytes = 0;
        while (jobs < maxJobs && bytes < maxDecodeBytes)
        {
            Job? job;
            lock (_gate)
            {
                if (_heap.Count == 0)
                    break;
                job = _heap[0];
                _heap.RemoveAt(0);
            }

            job.Action();
            jobs++;
            bytes += Math.Max(0, job.ByteBudgetHint);
        }

        return jobs;
    }

    /// <summary>Clears queued jobs (e.g. scene cancelled).</summary>
    public void Clear()
    {
        lock (_gate)
            _heap.Clear();
    }
}
