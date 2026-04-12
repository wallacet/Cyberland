using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: for entities with both <see cref="SpriteAnimation"/> and <see cref="Sprite"/>, advances time and recomputes atlas <see cref="Sprite.UvRect"/> from a uniform grid.
/// Uses <see cref="World.QueryChunks{T0,T1}"/> so each row has paired columns (no per-entity random <see cref="ComponentStore{T}"/> lookups).
/// </summary>
public sealed class SpriteAnimationSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly List<ComponentChunkView2<SpriteAnimation, Sprite>> _chunks = new();

    /// <inheritdoc />
    public void OnParallelLateUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _chunks.Clear();
        foreach (var chunk in world.QueryChunks<SpriteAnimation, Sprite>())
            _chunks.Add(chunk);

        if (_chunks.Count == 0)
            return;

        Parallel.For(0, _chunks.Count, parallelOptions, idx =>
        {
            var chunk = _chunks[idx];
            var anims = chunk.Components0;
            var sprites = chunk.Components1;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var a = ref anims[i];
                if (a.FrameCount <= 0 || a.SecondsPerFrame <= 0f || a.AtlasColumns <= 0)
                    continue;

                a.ElapsedSeconds += deltaSeconds;
                ref var spr = ref sprites[i];
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
