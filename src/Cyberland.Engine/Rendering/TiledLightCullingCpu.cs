using System;
using System.Threading.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Extracts world position, radius, and swapchain-space AABB from a light struct so tile binning and viewport
/// culling can be generic over light type. The JIT devirtualizes calls through the struct constraint.
/// </summary>
internal interface ILightAccessor<TLight>
{
    Vector2D<float> GetPositionWorld(in TLight light);
    float GetRadius(in TLight light);

    /// <summary>
    /// Computes the axis-aligned bounding box of the light in swapchain pixels. Point lights use a simple
    /// circle AABB from the already-projected center and scaled radius; spot lights project the cone's key
    /// vertices through <paramref name="cam"/> to produce a geometrically correct AABB regardless of camera
    /// rotation.
    /// </summary>
    void GetSwapchainAabb(in TLight light, Vector2D<float> centerSwapchainPx, float radiusSwapchainPx,
                          in ShadowSdfCamera cam,
                          out float minX, out float minY, out float maxX, out float maxY);
}

/// <summary><see cref="ILightAccessor{TLight}"/> for <see cref="PointLight"/> — circle AABB.</summary>
internal readonly struct PointLightAccessor : ILightAccessor<PointLight>
{
    public Vector2D<float> GetPositionWorld(in PointLight light) => light.PositionWorld;
    public float GetRadius(in PointLight light) => light.Radius;

    public void GetSwapchainAabb(in PointLight light, Vector2D<float> centerSwapchainPx, float radiusSwapchainPx,
                                 in ShadowSdfCamera cam,
                                 out float minX, out float minY, out float maxX, out float maxY)
    {
        minX = centerSwapchainPx.X - radiusSwapchainPx;
        minY = centerSwapchainPx.Y - radiusSwapchainPx;
        maxX = centerSwapchainPx.X + radiusSwapchainPx;
        maxY = centerSwapchainPx.Y + radiusSwapchainPx;
    }
}

/// <summary><see cref="ILightAccessor{TLight}"/> for <see cref="SpotLight"/> — tight cone AABB.</summary>
internal readonly struct SpotLightAccessor : ILightAccessor<SpotLight>
{
    public Vector2D<float> GetPositionWorld(in SpotLight light) => light.PositionWorld;
    public float GetRadius(in SpotLight light) => light.Radius;

    public void GetSwapchainAabb(in SpotLight light, Vector2D<float> centerSwapchainPx, float radiusSwapchainPx,
                                 in ShadowSdfCamera cam,
                                 out float minX, out float minY, out float maxX, out float maxY)
    {
        SpotLightBounds.ComputeProjectedConeAabb(in light, in cam,
            out minX, out minY, out maxX, out maxY);
    }
}

/// <summary>
/// Production CPU tile binner: partitions point lights into a regular grid covering the <b>physical viewport</b>
/// (letterboxed region within the swapchain) so the tiled deferred lighting shader iterates only the lights that
/// overlap each tile. Called every frame by <c>UpdateTileLightBins</c>, which writes results into the persistent
/// mapped <c>_ssboTileBins</c> / <c>_ssboTileIndices</c> SSBOs consumed by
/// <c>tiled_deferred_lighting.frag.glsl</c> (set 1, bindings 4–5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate space.</b> The tile grid covers the <b>physical viewport</b> (the letterboxed sub-rect of the
/// swapchain), not the full swapchain. Tile (0,0) starts at <see cref="ShadowSdfCamera.PhysicalOffsetSwapchainPx"/>.
/// Light AABBs from <see cref="ILightAccessor{TLight}.GetSwapchainAabb"/> are in full swapchain space and are
/// shifted by <c>-PhysicalOffsetSwapchainPx</c> before the overlap test, converting them to grid-local coordinates.
/// This avoids binning tiles in letterbox bars that the deferred lighting scissor never shades.
/// </para>
/// <para>
/// <b>Thread safety.</b> When a <c>ParallelOptions</c> is supplied, rows are processed in parallel via
/// <see cref="Parallel.For(int, int, ParallelOptions, System.Action{int})"/>. Each row writes to a disjoint slice of
/// the output bins and indices, so no locks are required. The underlying memory is pinned for the duration of the
/// parallel dispatch. The production caller (<c>VulkanRenderer.UpdateTileLightBins</c>) passes
/// <see cref="Core.Tasks.ParallelismSettings.CreateParallelOptions"/> from the host so row processing scales with the
/// configured core count.
/// </para>
/// <para>Also used by engine tests as a CPU reference oracle for tile-culling contracts.</para>
/// </remarks>
internal static class TiledLightCullingCpu
{
    /// <summary>Result for a single tile: count of lights that intersect, plus their indices.</summary>
    public readonly struct TileBin
    {
        /// <summary>Number of lights that intersect this tile.</summary>
        public readonly int Count;
        /// <summary>Offset into the per-frame index buffer where this tile's indices begin.</summary>
        public readonly int IndexOffset;

        /// <summary>Constructs a tile bin entry.</summary>
        public TileBin(int count, int indexOffset)
        {
            Count = count;
            IndexOffset = indexOffset;
        }
    }

    /// <summary>
    /// Bin <paramref name="lights"/> into a regular tile grid covering the swapchain. Returns the number of tiles written.
    /// </summary>
    /// <param name="lights">Point lights (world position + world radius).</param>
    /// <param name="lightCount">Valid light count.</param>
    /// <param name="cam">Per-frame camera + SDF mapping.</param>
    /// <param name="tileSizeSwapchainPx">Tile width/height in swapchain pixels (≥ 1).</param>
    /// <param name="maxLightsPerTile">Per-tile cap; overflow is truncated by submission order.</param>
    /// <param name="bins">Output bins, sized at least <c>ceil(W/tile) * ceil(H/tile)</c>.</param>
    /// <param name="indices">Output index buffer, sized at least <c>maxLightsPerTile * tileCount</c>.</param>
    /// <param name="parallelOptions">
    /// When non-null, tile rows are processed in parallel. Each row writes to a disjoint slice so no locking
    /// is needed. Pass <c>MaxDegreeOfParallelism = 1</c> in tests for deterministic single-threaded behavior.
    /// </param>
    public static unsafe int Bin(
        PointLight[] lights,
        int lightCount,
        in ShadowSdfCamera cam,
        int tileSizeSwapchainPx,
        int maxLightsPerTile,
        Span<TileBin> bins,
        Span<int> indices,
        ParallelOptions? parallelOptions = null)
    {
        if (tileSizeSwapchainPx <= 0)
            tileSizeSwapchainPx = 1;
        var physW = MathF.Max(cam.PhysicalSizeSwapchainPx.X, 1f);
        var physH = MathF.Max(cam.PhysicalSizeSwapchainPx.Y, 1f);
        var tilesX = (int)MathF.Ceiling(physW / tileSizeSwapchainPx);
        var tilesY = (int)MathF.Ceiling(physH / tileSizeSwapchainPx);
        // MathF.Ceiling on a positive value is always >= 1, so tilesX/tilesY are always >= 1 — no extra guard needed.
        var totalTiles = tilesX * tilesY;
        if (totalTiles > bins.Length)
            totalTiles = bins.Length;

        // Low-traffic frames (few rows × few lights) are cheaper to process serially than to dispatch.
        if (parallelOptions != null && tilesY > 1 && (long)tilesY * lightCount >= 256)
        {
            // Pin the Span-backing memory so raw pointers remain stable across Parallel.For worker threads.
            // Each row writes to bins[row*tilesX .. (row+1)*tilesX-1] and the corresponding index slice —
            // disjoint regions, so no synchronization is needed.
            fixed (TileBin* binsPtr = bins)
            fixed (int* indicesPtr = indices)
            {
                var bp = (nint)binsPtr;
                var ip = (nint)indicesPtr;
                var bLen = bins.Length;
                var iLen = indices.Length;
                var camSnap = cam;
                var tileSize = tileSizeSwapchainPx;
                var tx = tilesX;
                var total = totalTiles;
                var maxPer = maxLightsPerTile;

                // Pin must outlive every worker — keep this Parallel.For synchronous.
                // If this is ever refactored to Parallel.ForEachAsync, the pin scope must be restructured.
                Parallel.For(0, tilesY, parallelOptions, tileY =>
                {
                    BinRowPtr<PointLight, PointLightAccessor>(lights, lightCount, camSnap, tileSize, maxPer,
                           (TileBin*)bp, bLen, (int*)ip, iLen, tx, total, tileY);
                });
            }
        }
        else
        {
            for (var tileY = 0; tileY < tilesY && tileY * tilesX < totalTiles; tileY++)
                BinRowSpan<PointLight, PointLightAccessor>(lights, lightCount, in cam, tileSizeSwapchainPx, maxLightsPerTile,
                           bins, indices, tilesX, totalTiles, tileY);
        }

        return totalTiles;
    }

    /// <summary>
    /// Bin <paramref name="lights"/> (spot lights) into a regular tile grid covering the swapchain using
    /// cone-aware AABBs. Unlike point light binning which tests a full circle, this computes the tight
    /// projected AABB of each spot cone via <see cref="SpotLightBounds.ComputeProjectedConeAabb"/> and
    /// only tests tiles overlapping that AABB. Returns the number of tiles written.
    /// </summary>
    /// <param name="lights">Spot lights (world position + direction + radius + angles).</param>
    /// <param name="lightCount">Valid light count.</param>
    /// <param name="cam">Per-frame camera + SDF mapping.</param>
    /// <param name="tileSizeSwapchainPx">Tile width/height in swapchain pixels (≥ 1).</param>
    /// <param name="maxLightsPerTile">Per-tile cap; overflow is truncated by submission order.</param>
    /// <param name="bins">Output bins, sized at least <c>ceil(W/tile) * ceil(H/tile)</c>.</param>
    /// <param name="indices">Output index buffer, sized at least <c>maxLightsPerTile * tileCount</c>.</param>
    /// <param name="parallelOptions">
    /// When non-null, tile rows are processed in parallel. Each row writes to a disjoint slice so no locking
    /// is needed. Pass <c>MaxDegreeOfParallelism = 1</c> in tests for deterministic single-threaded behavior.
    /// </param>
    public static unsafe int BinSpotLights(
        SpotLight[] lights,
        int lightCount,
        in ShadowSdfCamera cam,
        int tileSizeSwapchainPx,
        int maxLightsPerTile,
        Span<TileBin> bins,
        Span<int> indices,
        ParallelOptions? parallelOptions = null)
    {
        if (tileSizeSwapchainPx <= 0)
            tileSizeSwapchainPx = 1;
        var physW = MathF.Max(cam.PhysicalSizeSwapchainPx.X, 1f);
        var physH = MathF.Max(cam.PhysicalSizeSwapchainPx.Y, 1f);
        var tilesX = (int)MathF.Ceiling(physW / tileSizeSwapchainPx);
        var tilesY = (int)MathF.Ceiling(physH / tileSizeSwapchainPx);
        var totalTiles = tilesX * tilesY;
        if (totalTiles > bins.Length)
            totalTiles = bins.Length;

        if (parallelOptions != null && tilesY > 1 && (long)tilesY * lightCount >= 256)
        {
            fixed (TileBin* binsPtr = bins)
            fixed (int* indicesPtr = indices)
            {
                var bp = (nint)binsPtr;
                var ip = (nint)indicesPtr;
                var bLen = bins.Length;
                var iLen = indices.Length;
                var camSnap = cam;
                var tileSize = tileSizeSwapchainPx;
                var tx = tilesX;
                var total = totalTiles;
                var maxPer = maxLightsPerTile;

                // Pin must outlive every worker — keep this Parallel.For synchronous.
                // If this is ever refactored to Parallel.ForEachAsync, the pin scope must be restructured.
                Parallel.For(0, tilesY, parallelOptions, tileY =>
                {
                    BinRowPtr<SpotLight, SpotLightAccessor>(lights, lightCount, camSnap, tileSize, maxPer,
                               (TileBin*)bp, bLen, (int*)ip, iLen, tx, total, tileY);
                });
            }
        }
        else
        {
            for (var tileY = 0; tileY < tilesY && tileY * tilesX < totalTiles; tileY++)
                BinRowSpan<SpotLight, SpotLightAccessor>(lights, lightCount, in cam, tileSizeSwapchainPx, maxLightsPerTile,
                               bins, indices, tilesX, totalTiles, tileY);
        }

        return totalTiles;
    }

    /// <summary>Returns <c>(tilesX, tilesY)</c> for the given camera's physical viewport and tile size.</summary>
    public static Vector2D<int> TileCounts(in ShadowSdfCamera cam, int tileSizeSwapchainPx)
    {
        if (tileSizeSwapchainPx <= 0)
            tileSizeSwapchainPx = 1;
        var tilesX = (int)MathF.Ceiling(MathF.Max(cam.PhysicalSizeSwapchainPx.X, 1f) / tileSizeSwapchainPx);
        var tilesY = (int)MathF.Ceiling(MathF.Max(cam.PhysicalSizeSwapchainPx.Y, 1f) / tileSizeSwapchainPx);
        return new Vector2D<int>(tilesX, tilesY);
    }

    /// <summary>
    /// Resolves an effective tile size that keeps the total tile count within <see cref="DeferredRenderingConstants.MaxTileGridCells"/>.
    /// When the swapchain is large enough that the default tile size would exceed the SSBO capacity, the tile size is
    /// doubled iteratively until the grid fits. Returns the effective tile size and tile counts that callers must use
    /// consistently for both CPU binning and GPU push constants.
    /// </summary>
    public static (int EffectiveTileSizePx, Vector2D<int> TileCounts) ResolveEffectiveTileGrid(
        in ShadowSdfCamera cam, int baseTileSizePx)
    {
        if (baseTileSizePx <= 0)
            baseTileSizePx = 1;
        var tileSizePx = baseTileSizePx;
        while (true)
        {
            var counts = TileCounts(in cam, tileSizePx);
            if ((long)counts.X * counts.Y <= DeferredRenderingConstants.MaxTileGridCells)
                return (tileSizePx, counts);
            tileSizePx *= 2;
        }
    }

    /// <summary>Sequential path: processes one tile row using Span accessors. Generic over light type via <typeparamref name="TAccessor"/>.</summary>
    private static void BinRowSpan<TLight, TAccessor>(
        TLight[] lights, int lightCount, in ShadowSdfCamera cam,
        int tileSizeSwapchainPx, int maxLightsPerTile,
        Span<TileBin> bins, Span<int> indices,
        int tilesX, int totalTiles, int tileY)
        where TAccessor : struct, ILightAccessor<TLight>
    {
        var accessor = default(TAccessor);
        var offsetX = cam.PhysicalOffsetSwapchainPx.X;
        var offsetY = cam.PhysicalOffsetSwapchainPx.Y;
        var tileMinY = tileY * tileSizeSwapchainPx;
        var tileMaxY = tileMinY + tileSizeSwapchainPx;
        for (var tileX = 0; tileX < tilesX; tileX++)
        {
            var tileIndex = tileY * tilesX + tileX;
            if (tileIndex >= totalTiles) break;
            var tileMinX = tileX * tileSizeSwapchainPx;
            var tileMaxX = tileMinX + tileSizeSwapchainPx;

            var written = 0;
            var indexBase = tileIndex * maxLightsPerTile;
            if (indexBase >= indices.Length)
            {
                bins[tileIndex] = new TileBin(0, indexBase);
                continue;
            }
            for (var li = 0; li < lightCount && written < maxLightsPerTile; li++)
            {
                ref readonly var light = ref lights[li];
                var centerSwapchainPx = cam.WorldToSwapchainPx(accessor.GetPositionWorld(in light));
                var radiusSwapchainPx = MathF.Max(accessor.GetRadius(in light), 0f) * cam.PhysicalScale;
                accessor.GetSwapchainAabb(in light, centerSwapchainPx, radiusSwapchainPx, in cam,
                    out var lMinX, out var lMinY, out var lMaxX, out var lMaxY);
                // Shift from full-swapchain space to grid-local (physical viewport) space.
                lMinX -= offsetX; lMinY -= offsetY;
                lMaxX -= offsetX; lMaxY -= offsetY;
                if (lMaxX < tileMinX || lMinX > tileMaxX ||
                    lMaxY < tileMinY || lMinY > tileMaxY)
                    continue;
                if (indexBase + written < indices.Length)
                    indices[indexBase + written] = li;
                written++;
            }
            bins[tileIndex] = new TileBin(written, indexBase);
        }
    }

    /// <summary>Parallel path: processes one tile row using raw pointers (capturable by lambdas). Generic over light type via <typeparamref name="TAccessor"/>.</summary>
    private static unsafe void BinRowPtr<TLight, TAccessor>(
        TLight[] lights, int lightCount, ShadowSdfCamera cam,
        int tileSizeSwapchainPx, int maxLightsPerTile,
        TileBin* bins, int binsLen, int* indices, int indicesLen,
        int tilesX, int totalTiles, int tileY)
        where TAccessor : struct, ILightAccessor<TLight>
    {
        var accessor = default(TAccessor);
        var offsetX = cam.PhysicalOffsetSwapchainPx.X;
        var offsetY = cam.PhysicalOffsetSwapchainPx.Y;
        var tileMinY = tileY * tileSizeSwapchainPx;
        var tileMaxY = tileMinY + tileSizeSwapchainPx;
        for (var tileX = 0; tileX < tilesX; tileX++)
        {
            var tileIndex = tileY * tilesX + tileX;
            if (tileIndex >= totalTiles) break;
            var tileMinX = tileX * tileSizeSwapchainPx;
            var tileMaxX = tileMinX + tileSizeSwapchainPx;

            var written = 0;
            var indexBase = tileIndex * maxLightsPerTile;
            if (indexBase >= indicesLen)
            {
                bins[tileIndex] = new TileBin(0, indexBase);
                continue;
            }
            for (var li = 0; li < lightCount && written < maxLightsPerTile; li++)
            {
                ref readonly var light = ref lights[li];
                var centerSwapchainPx = cam.WorldToSwapchainPx(accessor.GetPositionWorld(in light));
                var radiusSwapchainPx = MathF.Max(accessor.GetRadius(in light), 0f) * cam.PhysicalScale;
                accessor.GetSwapchainAabb(in light, centerSwapchainPx, radiusSwapchainPx, in cam,
                    out var lMinX, out var lMinY, out var lMaxX, out var lMaxY);
                // Shift from full-swapchain space to grid-local (physical viewport) space.
                lMinX -= offsetX; lMinY -= offsetY;
                lMaxX -= offsetX; lMaxY -= offsetY;
                if (lMaxX < tileMinX || lMinX > tileMaxX ||
                    lMaxY < tileMinY || lMinY > tileMaxY)
                    continue;
                if (indexBase + written < indicesLen)
                    indices[indexBase + written] = li;
                written++;
            }
            bins[tileIndex] = new TileBin(written, indexBase);
        }
    }

}
