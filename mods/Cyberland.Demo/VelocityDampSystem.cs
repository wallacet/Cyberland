using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Demo;

/// <summary>
/// Parallel per-chunk damp: each chunk exposes a contiguous <see cref="Velocity"/> span (SIMD on the packed floats).
/// </summary>
public sealed class VelocityDampSystem : IParallelSystem
{
    public void OnParallelUpdate(World world, ParallelOptions parallelOptions)
    {
        var chunks = new List<ComponentChunkView<Velocity>>();
        foreach (var chunk in world.QueryChunks<Velocity>())
            chunks.Add(chunk);

        if (chunks.Count == 0)
            return;

        Parallel.ForEach(chunks, parallelOptions, static chunk =>
        {
            var v = chunk.Components;
            var f = MemoryMarshal.Cast<Velocity, float>(v);
            SimdFloat.MultiplyInPlace(f, 0.999f);
        });
    }
}
