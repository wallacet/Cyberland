namespace Cyberland.Engine.Core.Tasks;

/// <summary>
/// Controls how aggressively we fan out CPU work. Zero means "use all logical processors."
/// </summary>
public sealed class ParallelismSettings
{
    /// <summary>Max concurrent tasks for Parallel.For / Task.Run fan-out. 0 = Environment.ProcessorCount.</summary>
    public int MaxConcurrency { get; set; }

    /// <summary>Builds <see cref="ParallelOptions"/> for <see cref="Cyberland.Engine.Core.Ecs.IParallelSystem.OnParallelUpdate"/> using <see cref="MaxConcurrency"/>.</summary>
    /// <returns>Options with <see cref="ParallelOptions.MaxDegreeOfParallelism"/> set from <see cref="MaxConcurrency"/> (or all cores when zero).</returns>
    public ParallelOptions CreateParallelOptions()
    {
        var max = MaxConcurrency <= 0 ? Environment.ProcessorCount : MaxConcurrency;
        return new ParallelOptions { MaxDegreeOfParallelism = max };
    }
}
