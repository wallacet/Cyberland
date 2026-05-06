using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Default engine sprite pass: walks <see cref="Sprite"/> chunks with <see cref="Transform"/> and
/// submits <see cref="SpriteDrawRequest"/>s. Mods normally attach components and let this system draw—no custom <see cref="IRenderer.SubmitSprite"/> calls.
/// </summary>
public sealed class SpriteRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private const int ParallelRangeSize = 128;
    private const int SmallChunkSerialThreshold = 64;
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Sprite, Transform>();

    /// <param name="host">Must expose a non-null <see cref="Hosting.GameHostServices.Renderer"/> after startup.</param>
    public SpriteRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        var camera = _host.CameraRuntimeState;
        foreach (var chunk in query)
        {
            if (chunk.Count <= SmallChunkSerialThreshold)
            {
                SubmitChunkRange(chunk, 0, chunk.Count, in camera, r);
                continue;
            }

            Parallel.ForEach(Partitioner.Create(0, chunk.Count, ParallelRangeSize), parallelOptions, range =>
            {
                SubmitChunkRange(chunk, range.Item1, range.Item2, in camera, r);
            });
        }
    }

    private static void SubmitChunkRange(
        in MultiComponentChunkView chunk,
        int start,
        int endExclusive,
        in CameraRuntimeState camera,
        IRenderer renderer)
    {
        var requests = ArrayPool<SpriteDrawRequest>.Shared.Rent(endExclusive - start);
        var requestCount = 0;
        try
        {
            var sprites = chunk.Column<Sprite>();
            var transforms = chunk.Column<Transform>();
            for (var i = start; i < endExclusive; i++)
            {
                ref readonly var spr = ref sprites[i];
                if (!spr.Visible)
                    continue;

                ref readonly var transform = ref transforms[i];
                // Decompose the world matrix once per sprite: reading the PRS properties via a ref readonly transform
                // would decompose (via a defensive copy) on every property access.
                TransformMath.DecomposeToPRS(transform.WorldMatrix, out var worldPos, out var worldRad, out var worldScale);
                var hx = spr.HalfExtents.X * worldScale.X;
                var hy = spr.HalfExtents.Y * worldScale.Y;
                if (IsOutsideActiveCamera(in spr, worldPos, hx, hy, in camera))
                    continue;

                requests[requestCount++] = new SpriteDrawRequest
                {
                    CenterWorld = worldPos,
                    HalfExtentsWorld = new Vector2D<float>(hx, hy),
                    RotationRadians = worldRad,
                    Layer = spr.Layer,
                    SortKey = spr.SortKey,
                    AlbedoTextureId = spr.AlbedoTextureId,
                    NormalTextureId = spr.NormalTextureId,
                    EmissiveTextureId = spr.EmissiveTextureId,
                    ColorMultiply = spr.ColorMultiply,
                    Alpha = spr.Alpha,
                    EmissiveTint = spr.EmissiveTint,
                    EmissiveIntensity = spr.EmissiveIntensity,
                    DepthHint = spr.DepthHint,
                    UvRect = spr.UvRect,
                    Transparent = spr.Transparent,
                    Space = spr.Space
                };
            }

            if (requestCount > 0)
                renderer.SubmitSprites(requests.AsSpan(0, requestCount));
        }
        finally
        {
            ArrayPool<SpriteDrawRequest>.Shared.Return(requests);
        }
    }

    private static bool IsOutsideActiveCamera(
        in Sprite sprite,
        Vector2D<float> spriteCenterWorld,
        float halfExtentX,
        float halfExtentY,
        in CameraRuntimeState camera)
    {
        if (!camera.Valid || sprite.Space is CoordinateSpace.ViewportSpace or CoordinateSpace.SwapchainSpace)
            return false;

        var viewport = new Vector2D<float>(camera.ViewportSizeWorld.X, camera.ViewportSizeWorld.Y);
        if (viewport.X <= 0f || viewport.Y <= 0f)
            return false;

        var centerVp = CameraProjection.WorldToViewportPixel(
            spriteCenterWorld,
            camera.PositionWorld,
            camera.RotationRadians,
            viewport);
        var radius = MathF.Sqrt((halfExtentX * halfExtentX) + (halfExtentY * halfExtentY));
        return centerVp.X + radius < 0f ||
               centerVp.Y + radius < 0f ||
               centerVp.X - radius > viewport.X ||
               centerVp.Y - radius > viewport.Y;
    }
}
