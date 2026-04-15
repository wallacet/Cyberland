using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: for entities with <see cref="SpriteAnimation"/>, advances time and recomputes atlas
/// <see cref="Sprite.UvRect"/> from a uniform grid.
/// </summary>
public sealed class SpriteAnimationSystem : IParallelSystem, IParallelLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SpriteAnimation>();

    private readonly List<MultiComponentChunkView> _chunks = new();

    /// <inheritdoc />
    public void OnParallelLateUpdate(World world, ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _chunks.Clear();
        foreach (var chunk in query)
            _chunks.Add(chunk);

        if (_chunks.Count == 0)
            return;

        var spriteStore = world.Components<Sprite>();
        Parallel.For(0, _chunks.Count, parallelOptions, idx =>
        {
            var chunk = _chunks[idx];
            var anims = chunk.Column<SpriteAnimation>(0);
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var a = ref anims[i];
                if (a.FrameCount <= 0 || a.SecondsPerFrame <= 0f || a.AtlasColumns <= 0)
                    continue;

                a.ElapsedSeconds += deltaSeconds;
                ref var spr = ref spriteStore.GetOrAdd(entities[i]);
                SpriteAnimationMath.Apply(ref a, ref spr);
            }
        });
    }
}

/// <summary>Shared UV math for flipbook animation (kept static for unit tests).</summary>
public static class SpriteAnimationMath
{
    /// <summary>Computes <paramref name="spr"/>.<see cref="Sprite.UvRect"/> from <paramref name="a"/>’s time and grid layout.</summary>
    public static void Apply(ref SpriteAnimation a, ref Sprite spr)
    {
        var frame = (int)MathF.Floor(a.ElapsedSeconds / a.SecondsPerFrame);
        if (a.Loop)
            frame = (int)((frame % a.FrameCount + a.FrameCount) % a.FrameCount);
        else
            frame = Math.Clamp(frame, 0, a.FrameCount - 1);

        var cols = a.AtlasColumns;
        var rows = (a.FrameCount + cols - 1) / cols;
        var fx = frame % cols;
        var fy = frame / cols;

        var uw = 1f / cols;
        var uh = 1f / rows;
        var u0 = fx * uw;
        var v0 = fy * uh;
        spr.UvRect = new Vector4D<float>(u0, v0, u0 + uw, v0 + uh);
    }
}
