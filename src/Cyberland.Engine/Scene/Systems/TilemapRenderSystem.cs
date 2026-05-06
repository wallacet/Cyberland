using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: for each <see cref="Tilemap"/> entity with <see cref="Transform"/>, looks up grid indices in
/// <see cref="Hosting.GameHostServices.Tilemaps"/> and issues textured quads per solid cell. Cell positions are built in
/// local grid space (+Y down for increasing row index) and mapped through the resolved world matrix.
/// </summary>
public sealed class TilemapRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private static readonly Vector4D<float> OpaqueWhite = new(1f, 1f, 1f, 1f);
    private const int SubmissionBatchSize = 1024;

    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Tilemap, Transform>();

    /// <param name="host">Requires both <see cref="Hosting.GameHostServices.Renderer"/> and <see cref="Hosting.GameHostServices.Tilemaps"/>.</param>
    public TilemapRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        var camera = _host.CameraRuntimeState;
        var store = _host.Tilemaps;
        if (store is null)
            return;

        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                var id = chunk.Entities[i];
                ref readonly var tm = ref chunk.Column<Tilemap>()[i];
                ref readonly var transform = ref chunk.Column<Transform>()[i];
                if (!store.TryGet(id, out var mem, out var cols, out var rows))
                    return;

                var span = mem.Span;
                var tw = tm.TileWidth;
                var th = tm.TileHeight;
                var tid = tm.AtlasAlbedoTextureId;
                var minI = tm.NonEmptyTileMinIndex;
                var defaultNormal = r.DefaultNormalTextureId;
                var atlasCols = tm.AtlasColumns <= 0 ? 1 : tm.AtlasColumns;
                var atlasRows = tm.AtlasRows <= 0 ? 1 : tm.AtlasRows;
                var uvStepX = 1f / atlasCols;
                var uvStepY = 1f / atlasRows;
                var batch = ArrayPool<SpriteDrawRequest>.Shared.Rent(SubmissionBatchSize);
                try
                {
                    var batchCount = 0;

                    // WorldMatrix is already the composed TRS; avoid the old decompose-then-rebuild round trip and pull
                    // scale / rotation for per-tile extents and orientation from one decomposition.
                    var worldM = transform.WorldMatrix;
                    TransformMath.DecomposeToPRS(worldM, out _, out var worldRad, out var worldScale);

                    var startX = 0;
                    var startY = 0;
                    var endX = cols;
                    var endY = rows;
                    if (camera.Valid &&
                        TryComputeVisibleTileBounds(
                            in camera,
                            worldM,
                            tw,
                            th,
                            cols,
                            rows,
                            out startX,
                            out startY,
                            out endX,
                            out endY) &&
                        (startX >= endX || startY >= endY))
                    {
                        return;
                    }

                    for (var y = startY; y < endY; y++)
                    for (var x = startX; x < endX; x++)
                    {
                        var ti = span[y * cols + x];
                        if (ti < minI)
                            continue;

                        var local = new Vector2((x + 0.5f) * tw, -((y + 0.5f) * th));
                        var centerWorldV = Vector2.Transform(local, worldM);
                        var centerWorld = new Vector2D<float>(centerWorldV.X, centerWorldV.Y);

                        var hx = tw * 0.48f * worldScale.X;
                        var hy = th * 0.48f * worldScale.Y;
                        var atlasIndex = ti - minI;
                        var atlasX = atlasIndex % atlasCols;
                        var atlasY = (atlasIndex / atlasCols) % atlasRows;
                        var req = new SpriteDrawRequest
                        {
                            CenterWorld = centerWorld,
                            HalfExtentsWorld = new Vector2D<float>(hx, hy),
                            RotationRadians = worldRad,
                            Layer = tm.Layer,
                            SortKey = tm.SortKey + x * 0.001f + y * 0.0001f,
                            AlbedoTextureId = tid,
                            NormalTextureId = defaultNormal,
                            EmissiveTextureId = TextureId.MaxValue,
                            ColorMultiply = OpaqueWhite,
                            Alpha = 1f,
                            EmissiveTint = default,
                            EmissiveIntensity = 0f,
                            DepthHint = tm.SortKey,
                            UvRect = new Vector4D<float>(
                                atlasX * uvStepX,
                                atlasY * uvStepY,
                                (atlasX + 1) * uvStepX,
                                (atlasY + 1) * uvStepY)
                        };
                        if (batchCount >= SubmissionBatchSize)
                        {
                            r.SubmitSprites(batch.AsSpan(0, batchCount));
                            batchCount = 0;
                        }
                        batch[batchCount++] = req;
                    }

                    if (batchCount > 0)
                        r.SubmitSprites(batch.AsSpan(0, batchCount));
                }
                finally
                {
                    ArrayPool<SpriteDrawRequest>.Shared.Return(batch);
                }
            });
        }
    }

    private static bool TryComputeVisibleTileBounds(
        in CameraRuntimeState camera,
        Matrix3x2 worldMatrix,
        float tileWidth,
        float tileHeight,
        int cols,
        int rows,
        out int startX,
        out int startY,
        out int endX,
        out int endY)
    {
        startX = 0;
        startY = 0;
        endX = cols;
        endY = rows;

        if (tileWidth <= 0f || tileHeight <= 0f || cols <= 0 || rows <= 0)
            return true;

        var viewport = new Vector2D<float>(camera.ViewportSizeWorld.X, camera.ViewportSizeWorld.Y);
        var topLeft = CameraProjection.ViewportPixelToWorld(new Vector2D<float>(0f, 0f), camera.PositionWorld, camera.RotationRadians, viewport);
        var topRight = CameraProjection.ViewportPixelToWorld(new Vector2D<float>(viewport.X, 0f), camera.PositionWorld, camera.RotationRadians, viewport);
        var bottomLeft = CameraProjection.ViewportPixelToWorld(new Vector2D<float>(0f, viewport.Y), camera.PositionWorld, camera.RotationRadians, viewport);
        var bottomRight = CameraProjection.ViewportPixelToWorld(new Vector2D<float>(viewport.X, viewport.Y), camera.PositionWorld, camera.RotationRadians, viewport);

        if (!Matrix3x2.Invert(worldMatrix, out var inverseWorld))
            return false;

        var localTopLeft = Vector2.Transform(new Vector2(topLeft.X, topLeft.Y), inverseWorld);
        var localTopRight = Vector2.Transform(new Vector2(topRight.X, topRight.Y), inverseWorld);
        var localBottomLeft = Vector2.Transform(new Vector2(bottomLeft.X, bottomLeft.Y), inverseWorld);
        var localBottomRight = Vector2.Transform(new Vector2(bottomRight.X, bottomRight.Y), inverseWorld);

        var minLocalX = MathF.Min(MathF.Min(localTopLeft.X, localTopRight.X), MathF.Min(localBottomLeft.X, localBottomRight.X));
        var maxLocalX = MathF.Max(MathF.Max(localTopLeft.X, localTopRight.X), MathF.Max(localBottomLeft.X, localBottomRight.X));
        var minLocalY = MathF.Min(MathF.Min(localTopLeft.Y, localTopRight.Y), MathF.Min(localBottomLeft.Y, localBottomRight.Y));
        var maxLocalY = MathF.Max(MathF.Max(localTopLeft.Y, localTopRight.Y), MathF.Max(localBottomLeft.Y, localBottomRight.Y));

        // Expand by half a tile so partially visible cells are still included at edges.
        minLocalX -= tileWidth * 0.5f;
        maxLocalX += tileWidth * 0.5f;
        minLocalY -= tileHeight * 0.5f;
        maxLocalY += tileHeight * 0.5f;

        startX = Math.Max(0, (int)MathF.Floor(minLocalX / tileWidth));
        endX = Math.Min(cols, (int)MathF.Ceiling(maxLocalX / tileWidth));

        // Local tile rows increase downward in world submission: local Y for row r is -(r + 0.5) * tileHeight.
        startY = Math.Max(0, (int)MathF.Floor((-maxLocalY) / tileHeight));
        endY = Math.Min(rows, (int)MathF.Ceiling((-minLocalY) / tileHeight));
        return true;
    }
}
