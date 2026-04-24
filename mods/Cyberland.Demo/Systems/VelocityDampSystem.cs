using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo;

/// <summary>
/// Parallel fixed update: scales every <see cref="Velocity"/> component by a per-tick factor using <see cref="Parallel.ForEach"/>
/// over matching chunks. This mod keeps <see cref="IParallelSystem"/> here only—other demo systems are sequential because they
/// touch singletons or submit lights without chunk work.
/// </summary>
public sealed class VelocityDampSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Velocity>();

    // Reused across ticks to avoid list allocation churn in the fixed-step loop.
    private readonly List<MultiComponentChunkView> _chunks = new();

    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        var factor = MathF.Pow(0.999f, fixedDeltaSeconds * 60f);
        _chunks.Clear();
        foreach (var chunk in query)
            _chunks.Add(chunk);

        if (_chunks.Count == 0)
            return;

        Parallel.ForEach(_chunks, parallelOptions, chunk =>
        {
            var v = chunk.Column<Velocity>(0);
            var f = MemoryMarshal.Cast<Velocity, float>(v);
            SimdFloat.MultiplyInPlace(f, factor);
        });
    }
}
