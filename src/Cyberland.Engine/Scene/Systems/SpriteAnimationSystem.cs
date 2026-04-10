using System.Collections.Generic;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: for entities with both <see cref="SpriteAnimation"/> and <see cref="Sprite"/>, advances time and recomputes atlas <see cref="Sprite.UvRect"/> from a uniform grid.
/// </summary>
public sealed class SpriteAnimationSystem : IParallelSystem
{
    /// <inheritdoc />
    public void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions)
    {
        var chunks = new List<ComponentChunkView<SpriteAnimation>>();
        foreach (var chunk in world.QueryChunks<SpriteAnimation>())
            chunks.Add(chunk);

        if (chunks.Count == 0)
            return;

        Parallel.ForEach(chunks, parallelOptions, chunk =>
        {
            var ents = chunk.Entities;
            var anims = chunk.Components;
            for (var i = 0; i < chunk.Count; i++)
            {
                var id = ents[i];
                if (!world.Components<Sprite>().Contains(id))
                    continue;

                ref var a = ref anims[i];
                if (a.FrameCount <= 0 || a.SecondsPerFrame <= 0f || a.AtlasColumns <= 0)
                    continue;

                a.ElapsedSeconds += deltaSeconds;
                ref var spr = ref world.Components<Sprite>().Get(id);
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
