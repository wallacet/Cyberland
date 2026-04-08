namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Controls how aggressively we fan out CPU work. Zero means "use all logical processors."
/// </summary>
public sealed class ParallelismSettings
{
    /// <summary>Max concurrent tasks for Parallel.For / Task.Run fan-out. 0 = Environment.ProcessorCount.</summary>
    public int MaxConcurrency { get; set; }

    public ParallelOptions CreateParallelOptions()
    {
        var max = MaxConcurrency <= 0 ? Environment.ProcessorCount : MaxConcurrency;
        return new ParallelOptions { MaxDegreeOfParallelism = max };
    }
}
