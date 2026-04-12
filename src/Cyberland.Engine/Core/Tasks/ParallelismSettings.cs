namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Controls how aggressively we fan out CPU work. Zero means "use all logical processors."
/// </summary>
/// <remarks>
/// <see cref="CreateParallelOptions"/> returns a <strong>reused</strong> <see cref="ParallelOptions"/> instance owned by this object
/// (updated from <see cref="MaxConcurrency"/> each call) so the frame scheduler does not allocate per frame.
/// </remarks>
public sealed class ParallelismSettings
{
    private readonly ParallelOptions _parallelOptions = new();
    private readonly int _logicalProcessorCount = Environment.ProcessorCount;

    /// <summary>Max concurrent tasks for Parallel.For / Task.Run fan-out. 0 = Environment.ProcessorCount.</summary>
    public int MaxConcurrency { get; set; }

    /// <summary>Builds <see cref="ParallelOptions"/> for parallel ECS phases using <see cref="MaxConcurrency"/>.</summary>
    /// <returns>The same reusable instance each time, with <see cref="ParallelOptions.MaxDegreeOfParallelism"/> synced from <see cref="MaxConcurrency"/> (or all cores when zero).</returns>
    public ParallelOptions CreateParallelOptions()
    {
        var max = MaxConcurrency <= 0 ? _logicalProcessorCount : MaxConcurrency;
        if (_parallelOptions.MaxDegreeOfParallelism != max)
            _parallelOptions.MaxDegreeOfParallelism = max;
        return _parallelOptions;
    }
}
