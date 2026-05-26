using System.Threading.Tasks;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class TiledLightCullingCpuTests
{
    private static ShadowSdfCamera Cam128() => ShadowSdfCamera.SyntheticCamera(
        cameraPosWorld: new Vector2D<float>(64f, 36f),
        cameraRotRadians: 0f,
        viewportSizeWorld: new Vector2D<int>(128, 72),
        swapchainSizePx: new Vector2D<int>(128, 72),
        sdfScale: 1f);

    [Fact]
    public void TileCounts_divides_screen_size_by_tile_size_ceiled()
    {
        var cam = Cam128();
        var counts = TiledLightCullingCpu.TileCounts(in cam, tileSizeSwapchainPx: 16);
        Assert.Equal(8, counts.X); // 128 / 16
        Assert.Equal(5, counts.Y); // ceil(72 / 16) = 5

        // Non-positive tile size clamps to 1px tiles.
        var cnt = TiledLightCullingCpu.TileCounts(in cam, 0);
        Assert.Equal(128, cnt.X);
        Assert.Equal(72, cnt.Y);
    }

    [Fact]
    public void TileCounts_for_zero_swapchain_returns_at_least_one_tile()
    {
        var zeroCam = new ShadowSdfCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(0f, 0f),
            1f,
            new Vector2D<float>(0f, 0f),
            sdfScale: 1f);
        // Swapchain max-with-1 ensures at least one tile despite zero size.
        var counts = TiledLightCullingCpu.TileCounts(in zeroCam, 16);
        Assert.Equal(1, counts.X);
        Assert.Equal(1, counts.Y);
    }

    [Fact]
    public void Bin_assigns_lights_overlapping_each_tile_in_swapchain_aabb()
    {
        var cam = Cam128();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 5f },
            new PointLight { PositionWorld = new Vector2D<float>(60f, 50f), Radius = 5f },
        };
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[8 * 5];
        Span<int> indices = stackalloc int[8 * 5 * 4];
        var total = TiledLightCullingCpu.Bin(lights, 2, in cam, tileSizeSwapchainPx: 16, maxLightsPerTile: 4, bins, indices);
        Assert.Equal(40, total);

        var anyAssignment = false;
        for (var i = 0; i < total; i++)
        {
            if (bins[i].Count > 0)
            {
                anyAssignment = true;
                Assert.True(bins[i].Count <= 4);
            }
        }
        Assert.True(anyAssignment);
    }

    [Fact]
    public void Bin_truncates_overflow_to_maxLightsPerTile()
    {
        var cam = Cam128();
        // Many overlapping lights inside the same tile.
        var lights = new PointLight[20];
        for (var i = 0; i < lights.Length; i++)
        {
            lights[i] = new PointLight { PositionWorld = new Vector2D<float>(60f, 36f), Radius = 100f };
        }
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[8 * 5];
        Span<int> indices = stackalloc int[8 * 5 * 3];
        TiledLightCullingCpu.Bin(lights, lights.Length, in cam, 16, maxLightsPerTile: 3, bins, indices);
        for (var i = 0; i < bins.Length; i++)
        {
            Assert.True(bins[i].Count <= 3);
        }
    }

    [Fact]
    public void Bin_handles_zero_tile_size_by_clamping_to_one()
    {
        var cam = Cam128();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 5f },
        };
        // Very small bins span: just check it doesn't crash and writes something sane.
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[16];
        Span<int> indices = stackalloc int[16];
        // tileSize=0 -> 1px tiles internally, but bins span is small so we'll only fill 16 tiles.
        var total = TiledLightCullingCpu.Bin(lights, 1, in cam, tileSizeSwapchainPx: 0, maxLightsPerTile: 1, bins, indices);
        Assert.Equal(16, total);
    }

    [Fact]
    public void Bin_with_too_small_index_buffer_truncates_writes()
    {
        var cam = Cam128();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 100f },
        };
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[8 * 5];
        // indices intentionally short — exercises the "indexBase >= indices.Length" branch.
        Span<int> indices = stackalloc int[4];
        var total = TiledLightCullingCpu.Bin(lights, 1, in cam, 16, maxLightsPerTile: 4, bins, indices);
        Assert.Equal(40, total);
    }

    [Fact]
    public void Bin_1024_lights_produces_deterministic_output()
    {
        var cam = Cam128();
        const int n = 1024;
        var lights = new PointLight[n];
        var rng = new Random(42);
        for (var i = 0; i < n; i++)
        {
            lights[i] = new PointLight
            {
                PositionWorld = new Vector2D<float>(
                    (float)(rng.NextDouble() * 128),
                    (float)(rng.NextDouble() * 72)),
                Radius = 5f + (float)(rng.NextDouble() * 20)
            };
        }

        const int tileCount = 8 * 5;
        const int maxPerTile = 64;
        var bins1 = new TiledLightCullingCpu.TileBin[tileCount];
        var indices1 = new int[tileCount * maxPerTile];
        TiledLightCullingCpu.Bin(lights, n, in cam, 16, maxPerTile, bins1, indices1);

        // Run again with the same inputs and verify deterministic output.
        var bins2 = new TiledLightCullingCpu.TileBin[tileCount];
        var indices2 = new int[tileCount * maxPerTile];
        TiledLightCullingCpu.Bin(lights, n, in cam, 16, maxPerTile, bins2, indices2);

        for (var i = 0; i < tileCount; i++)
        {
            Assert.Equal(bins1[i].Count, bins2[i].Count);
            Assert.Equal(bins1[i].IndexOffset, bins2[i].IndexOffset);
        }

        Assert.True(indices1.AsSpan().SequenceEqual(indices2));
    }

    [Fact]
    public void TileCounts_at_extreme_resolution_stays_within_MaxTileGridCells()
    {
        // 7680x4320 (8K) with 64px tiles → 120×68 = 8160 tiles — exceeds MaxTileGridCells.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: default,
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(7680, 4320),
            swapchainSizePx: new Vector2D<int>(7680, 4320),
            sdfScale: 1f);

        var counts = TiledLightCullingCpu.TileCounts(in cam, DeferredRenderingConstants.TileSizeSwapchainPx);

        // Precondition: raw tile count exceeds SSBO capacity.
        Assert.True(counts.X * counts.Y > DeferredRenderingConstants.MaxTileGridCells,
            "Test precondition: 8K should exceed max tile cells.");

        // ResolveEffectiveTileGrid doubles tile size until the grid fits.
        var (effectiveTileSize, resolved) = TiledLightCullingCpu.ResolveEffectiveTileGrid(
            in cam, DeferredRenderingConstants.TileSizeSwapchainPx);
        Assert.True(resolved.X * resolved.Y <= DeferredRenderingConstants.MaxTileGridCells);
        Assert.True(effectiveTileSize >= DeferredRenderingConstants.TileSizeSwapchainPx);
    }

    [Fact]
    public void ResolveEffectiveTileGrid_returns_base_size_when_grid_fits()
    {
        var cam = Cam128();
        var (tileSizePx, counts) = TiledLightCullingCpu.ResolveEffectiveTileGrid(
            in cam, DeferredRenderingConstants.TileSizeSwapchainPx);
        Assert.Equal(DeferredRenderingConstants.TileSizeSwapchainPx, tileSizePx);
        Assert.Equal(TiledLightCullingCpu.TileCounts(in cam, tileSizePx), counts);
    }

    [Fact]
    public void ResolveEffectiveTileGrid_clamps_non_positive_base_to_one()
    {
        var cam = Cam128();
        var (tileSizePx, counts) = TiledLightCullingCpu.ResolveEffectiveTileGrid(in cam, 0);
        Assert.True(tileSizePx >= 1);
        Assert.True((long)counts.X * counts.Y <= DeferredRenderingConstants.MaxTileGridCells);
    }

    [Fact]
    public void ResolveEffectiveTileGrid_8K_bottom_screen_light_is_binned()
    {
        // Regression: at 8K the old truncation strategy dropped bottom-screen tiles entirely.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(3840f, 2160f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(7680, 4320),
            swapchainSizePx: new Vector2D<int>(7680, 4320),
            sdfScale: 1f);

        var (tileSizePx, tileCounts) = TiledLightCullingCpu.ResolveEffectiveTileGrid(
            in cam, DeferredRenderingConstants.TileSizeSwapchainPx);
        var totalTiles = tileCounts.X * tileCounts.Y;

        // Place a point light near the bottom-center of the screen (world Y below camera → swapchain Y near bottom).
        var lights = new[]
        {
            new PointLight
            {
                PositionWorld = new Vector2D<float>(3840f, 160f),
                Radius = 200f,
                Intensity = 1f
            }
        };

        var maxPerTile = DeferredRenderingConstants.MaxLightsPerTile;
        var bins = new TiledLightCullingCpu.TileBin[totalTiles];
        var indices = new int[totalTiles * maxPerTile];

        TiledLightCullingCpu.Bin(lights, 1, in cam, tileSizePx, maxPerTile, bins, indices,
            new ParallelOptions { MaxDegreeOfParallelism = 1 });

        // The light should appear in at least one bin near the bottom of the tile grid.
        var found = false;
        for (var i = 0; i < totalTiles; i++)
        {
            if (bins[i].Count > 0) { found = true; break; }
        }
        Assert.True(found, "Bottom-screen point light must be binned at 8K resolution.");
    }

    [Fact]
    public void Bin_parallel_matches_sequential_output()
    {
        var cam = Cam128();
        const int n = 1024;
        var lights = new PointLight[n];
        var rng = new Random(42);
        for (var i = 0; i < n; i++)
        {
            lights[i] = new PointLight
            {
                PositionWorld = new Vector2D<float>(
                    (float)(rng.NextDouble() * 128),
                    (float)(rng.NextDouble() * 72)),
                Radius = 5f + (float)(rng.NextDouble() * 20)
            };
        }

        const int tileCount = 8 * 5;
        const int maxPerTile = 64;

        // Sequential (no ParallelOptions)
        var binsSeq = new TiledLightCullingCpu.TileBin[tileCount];
        var indicesSeq = new int[tileCount * maxPerTile];
        TiledLightCullingCpu.Bin(lights, n, in cam, 16, maxPerTile, binsSeq, indicesSeq);

        // Parallel (uncapped thread count)
        var binsPar = new TiledLightCullingCpu.TileBin[tileCount];
        var indicesPar = new int[tileCount * maxPerTile];
        TiledLightCullingCpu.Bin(lights, n, in cam, 16, maxPerTile, binsPar, indicesPar,
            new ParallelOptions());

        for (var i = 0; i < tileCount; i++)
        {
            Assert.Equal(binsSeq[i].Count, binsPar[i].Count);
            Assert.Equal(binsSeq[i].IndexOffset, binsPar[i].IndexOffset);
        }
        Assert.True(indicesSeq.AsSpan().SequenceEqual(indicesPar));
    }

    [Fact]
    public void Bin_parallel_with_short_index_buffer_truncates_writes()
    {
        var cam = Cam128();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 100f },
        };
        var bins = new TiledLightCullingCpu.TileBin[8 * 5];
        var indices = new int[4];
        var total = TiledLightCullingCpu.Bin(lights, 1, in cam, 16, 4, bins, indices,
            new ParallelOptions());
        Assert.Equal(40, total);
    }

    [Fact]
    public void Bin_parallel_single_row_uses_parallel_path()
    {
        // Single-row grid: tilesY = 1, should fall back to sequential.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(8f, 4f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(128, 16),
            swapchainSizePx: new Vector2D<int>(128, 16),
            sdfScale: 1f);
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(8f, 4f), Radius = 10f },
        };
        var bins = new TiledLightCullingCpu.TileBin[8];
        var indices = new int[8 * 4];
        var total = TiledLightCullingCpu.Bin(lights, 1, in cam, 16, 4, bins, indices,
            new ParallelOptions { MaxDegreeOfParallelism = 1 });
        Assert.True(total > 0);
        var anyHit = false;
        for (var i = 0; i < total; i++)
            if (bins[i].Count > 0) anyHit = true;
        Assert.True(anyHit);
    }

    [Fact]
    public void Bin_parallel_capped_at_four_threads_matches_sequential()
    {
        var cam = Cam128();
        const int n = 1024;
        var lights = new PointLight[n];
        var rng = new Random(42);
        for (var i = 0; i < n; i++)
        {
            lights[i] = new PointLight
            {
                PositionWorld = new Vector2D<float>(
                    (float)(rng.NextDouble() * 128),
                    (float)(rng.NextDouble() * 72)),
                Radius = 5f + (float)(rng.NextDouble() * 20)
            };
        }

        const int tileCount = 8 * 5;
        const int maxPerTile = 64;

        var binsSerial = new TiledLightCullingCpu.TileBin[tileCount];
        var indicesSerial = new int[tileCount * maxPerTile];
        TiledLightCullingCpu.Bin(lights, n, in cam, 16, maxPerTile, binsSerial, indicesSerial);

        var binsPar = new TiledLightCullingCpu.TileBin[tileCount];
        var indicesPar = new int[tileCount * maxPerTile];
        TiledLightCullingCpu.Bin(lights, n, in cam, 16, maxPerTile, binsPar, indicesPar,
            new ParallelOptions { MaxDegreeOfParallelism = 4 });

        for (var i = 0; i < tileCount; i++)
        {
            Assert.Equal(binsSerial[i].Count, binsPar[i].Count);
            Assert.Equal(binsSerial[i].IndexOffset, binsPar[i].IndexOffset);
        }
        Assert.True(indicesSerial.AsSpan().SequenceEqual(indicesPar));
    }

    [Fact]
    public void SpotLightAccessor_returns_expected_fields()
    {
        var accessor = new SpotLightAccessor();
        var spot = new SpotLight
        {
            PositionWorld = new Vector2D<float>(42f, 99f),
            Radius = 17.5f,
            DirectionWorld = new Vector2D<float>(1f, 0f),
            OuterConeRadians = MathF.PI / 6f
        };
        Assert.Equal(new Vector2D<float>(42f, 99f), accessor.GetPositionWorld(in spot));
        Assert.Equal(17.5f, accessor.GetRadius(in spot));

        // Build a synthetic camera centered on the spot so projection is identity-like.
        var cam = ShadowSdfCamera.SyntheticCamera(
            spot.PositionWorld, 0f,
            new Vector2D<int>(200, 200),
            new Vector2D<int>(200, 200),
            sdfScale: 1f);
        var center = cam.WorldToSwapchainPx(spot.PositionWorld);
        accessor.GetSwapchainAabb(in spot, center, 17.5f, in cam,
            out var minX, out var minY, out var maxX, out var maxY);
        Assert.True(minX <= center.X);
        Assert.True(maxX >= center.X);
        Assert.True(minY <= center.Y);
        Assert.True(maxY >= center.Y);
    }

    [Fact]
    public void PointLightAccessor_GetSwapchainAabb_returns_circle_bounds()
    {
        var accessor = new PointLightAccessor();
        var light = new PointLight { PositionWorld = new Vector2D<float>(10f, 20f), Radius = 5f };
        var center = new Vector2D<float>(50f, 60f);
        var cam = ShadowSdfCamera.SyntheticCamera(
            default, 0f,
            new Vector2D<int>(200, 200),
            new Vector2D<int>(200, 200),
            sdfScale: 1f);
        accessor.GetSwapchainAabb(in light, center, 5f, in cam,
            out var minX, out var minY, out var maxX, out var maxY);
        Assert.Equal(45f, minX);
        Assert.Equal(55f, minY);
        Assert.Equal(55f, maxX);
        Assert.Equal(65f, maxY);
    }

    [Fact]
    public void BinSpotLights_assigns_lights_overlapping_each_tile()
    {
        var cam = Cam128();
        var lights = new[]
        {
            new SpotLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 5f },
            new SpotLight { PositionWorld = new Vector2D<float>(60f, 50f), Radius = 5f },
        };
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[8 * 5];
        Span<int> indices = stackalloc int[8 * 5 * 4];
        var total = TiledLightCullingCpu.BinSpotLights(lights, 2, in cam, tileSizeSwapchainPx: 16, maxLightsPerTile: 4, bins, indices);
        Assert.Equal(40, total);

        var anyAssignment = false;
        for (var i = 0; i < total; i++)
        {
            if (bins[i].Count > 0)
            {
                anyAssignment = true;
                Assert.True(bins[i].Count <= 4);
            }
        }
        Assert.True(anyAssignment);
    }

    [Fact]
    public void BinSpotLights_truncates_overflow_to_maxLightsPerTile()
    {
        var cam = Cam128();
        var lights = new SpotLight[20];
        for (var i = 0; i < lights.Length; i++)
        {
            lights[i] = new SpotLight { PositionWorld = new Vector2D<float>(60f, 36f), Radius = 100f };
        }
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[8 * 5];
        Span<int> indices = stackalloc int[8 * 5 * 3];
        TiledLightCullingCpu.BinSpotLights(lights, lights.Length, in cam, 16, maxLightsPerTile: 3, bins, indices);
        for (var i = 0; i < bins.Length; i++)
        {
            Assert.True(bins[i].Count <= 3);
        }
    }

    [Fact]
    public void BinSpotLights_parallel_matches_sequential()
    {
        var cam = Cam128();
        const int n = 256;
        var lights = new SpotLight[n];
        var rng = new Random(99);
        for (var i = 0; i < n; i++)
        {
            lights[i] = new SpotLight
            {
                PositionWorld = new Vector2D<float>(
                    (float)(rng.NextDouble() * 128),
                    (float)(rng.NextDouble() * 72)),
                Radius = 5f + (float)(rng.NextDouble() * 20)
            };
        }

        const int tileCount = 8 * 5;
        const int maxPerTile = 64;

        var binsSeq = new TiledLightCullingCpu.TileBin[tileCount];
        var indicesSeq = new int[tileCount * maxPerTile];
        TiledLightCullingCpu.BinSpotLights(lights, n, in cam, 16, maxPerTile, binsSeq, indicesSeq);

        var binsPar = new TiledLightCullingCpu.TileBin[tileCount];
        var indicesPar = new int[tileCount * maxPerTile];
        TiledLightCullingCpu.BinSpotLights(lights, n, in cam, 16, maxPerTile, binsPar, indicesPar,
            new ParallelOptions());

        for (var i = 0; i < tileCount; i++)
        {
            Assert.Equal(binsSeq[i].Count, binsPar[i].Count);
            Assert.Equal(binsSeq[i].IndexOffset, binsPar[i].IndexOffset);
        }
        Assert.True(indicesSeq.AsSpan().SequenceEqual(indicesPar));
    }

    [Fact]
    public void BinSpotLights_with_short_index_buffer_truncates_writes()
    {
        var cam = Cam128();
        var lights = new[]
        {
            new SpotLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 100f },
        };
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[8 * 5];
        Span<int> indices = stackalloc int[4];
        var total = TiledLightCullingCpu.BinSpotLights(lights, 1, in cam, 16, maxLightsPerTile: 4, bins, indices);
        Assert.Equal(40, total);
    }

    [Fact]
    public void BinSpotLights_clamps_zero_tile_size_to_one()
    {
        var cam = Cam128();
        var lights = new[]
        {
            new SpotLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 5f },
        };
        var bins = new TiledLightCullingCpu.TileBin[128 * 72];
        var indices = new int[128 * 72];
        var total = TiledLightCullingCpu.BinSpotLights(lights, 1, in cam, tileSizeSwapchainPx: 0, maxLightsPerTile: 1, bins, indices);
        Assert.True(total > 0);
    }

    [Fact]
    public void BinSpotLights_clamps_when_bins_smaller_than_grid()
    {
        var cam = Cam128();
        var lights = new[]
        {
            new SpotLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 5f },
        };
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[2];
        Span<int> indices = stackalloc int[2 * 4];
        var total = TiledLightCullingCpu.BinSpotLights(lights, 1, in cam, 16, maxLightsPerTile: 4, bins, indices);
        Assert.Equal(2, total);
    }

    [Fact]
    public void BinSpotLights_parallel_with_short_indices_exercises_guard()
    {
        var cam = Cam128();
        // Need tilesY * lightCount >= 256 to hit the parallel BinRowPtr path.
        // Cam128 gives tilesY = 5, so we need >= 52 lights (5 * 52 = 260 >= 256).
        var lights = new SpotLight[60];
        for (var i = 0; i < lights.Length; i++)
            lights[i] = new SpotLight { PositionWorld = new Vector2D<float>(20f, 20f), Radius = 100f };
        var bins = new TiledLightCullingCpu.TileBin[8 * 5];
        var indices = new int[4];
        var total = TiledLightCullingCpu.BinSpotLights(lights, lights.Length, in cam, 16, maxLightsPerTile: 4, bins, indices,
            new ParallelOptions { MaxDegreeOfParallelism = 4 });
        Assert.Equal(40, total);

        var anyOverflow = false;
        for (var i = 0; i < total; i++)
        {
            if (bins[i].IndexOffset >= indices.Length)
                anyOverflow = true;
        }
        Assert.True(anyOverflow);
    }

    [Fact]
    public void TileCounts_letterbox_uses_physical_viewport_not_swapchain()
    {
        // 320x240 viewport in 640x240 swapchain → 160px pillarbox bars on each side.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(160f, 120f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(320, 240),
            swapchainSizePx: new Vector2D<int>(640, 240),
            sdfScale: 1f);

        // Physical viewport: 320x240 (scale 1.0), offset (160, 0).
        Assert.Equal(160f, cam.PhysicalOffsetSwapchainPx.X);
        Assert.Equal(0f, cam.PhysicalOffsetSwapchainPx.Y);
        Assert.Equal(320f, cam.PhysicalSizeSwapchainPx.X);
        Assert.Equal(240f, cam.PhysicalSizeSwapchainPx.Y);

        var counts = TiledLightCullingCpu.TileCounts(in cam, 16);
        // Physical size 320/16 = 20, 240/16 = 15 — not swapchain 640/16 = 40.
        Assert.Equal(20, counts.X);
        Assert.Equal(15, counts.Y);
    }

    [Fact]
    public void Bin_letterbox_light_in_bar_is_not_binned()
    {
        // 320x240 viewport in 640x240 swapchain → 160px pillarbox bars on each side.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(160f, 120f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(320, 240),
            swapchainSizePx: new Vector2D<int>(640, 240),
            sdfScale: 1f);

        const int tileSizePx = 16;
        const int maxPerTile = 8;
        var tileCounts = TiledLightCullingCpu.TileCounts(in cam, tileSizePx);
        var totalTiles = tileCounts.X * tileCounts.Y;

        // A light far to the left in world space: projects into the left pillarbox bar.
        // World X = -30 → swapchain X = offset + ((-30 - 160) + 160) * scale = 160 + (-30)*1 = 130
        // which is < offset (160), so it's in the bar area (grid-local X < 0).
        var barLights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(-30f, 120f), Radius = 8f, Intensity = 1f },
        };

        var bins = new TiledLightCullingCpu.TileBin[totalTiles];
        var indices = new int[totalTiles * maxPerTile];
        TiledLightCullingCpu.Bin(barLights, 1, in cam, tileSizePx, maxPerTile, bins, indices);

        // The light is entirely within the left letterbox bar — no tile should contain it.
        var anyBinned = false;
        for (var i = 0; i < totalTiles; i++)
            if (bins[i].Count > 0) anyBinned = true;
        Assert.False(anyBinned, "Light in the pillarbox bar must not be binned into any tile.");
    }

    [Fact]
    public void Bin_letterbox_light_in_viewport_is_binned_correctly()
    {
        // 320x240 viewport in 640x240 swapchain → 160px pillarbox bars on each side.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(160f, 120f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(320, 240),
            swapchainSizePx: new Vector2D<int>(640, 240),
            sdfScale: 1f);

        const int tileSizePx = 16;
        const int maxPerTile = 8;
        var tileCounts = TiledLightCullingCpu.TileCounts(in cam, tileSizePx);
        var totalTiles = tileCounts.X * tileCounts.Y;

        // Light at camera center → projects to swapchain center (320, 120).
        // Grid-local: (320 - 160, 120 - 0) = (160, 120).
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(160f, 120f), Radius = 5f, Intensity = 1f },
        };

        var bins = new TiledLightCullingCpu.TileBin[totalTiles];
        var indices = new int[totalTiles * maxPerTile];
        TiledLightCullingCpu.Bin(lights, 1, in cam, tileSizePx, maxPerTile, bins, indices);

        // Verify the light is binned in the correct tile (matching GPU shader logic).
        var swPx = cam.WorldToSwapchainPx(lights[0].PositionWorld);
        var tileX = (int)Math.Floor((swPx.X - cam.PhysicalOffsetSwapchainPx.X) / tileSizePx);
        var tileY = (int)Math.Floor((swPx.Y - cam.PhysicalOffsetSwapchainPx.Y) / tileSizePx);
        var tileIdx = tileY * tileCounts.X + tileX;

        Assert.True(tileIdx >= 0 && tileIdx < totalTiles,
            $"Tile index {tileIdx} out of range [0, {totalTiles}).");
        Assert.True(bins[tileIdx].Count > 0,
            "Light in the physical viewport must be binned into the correct tile.");
    }

    [Fact]
    public void Bin_pillarboxed_viewport_indexes_correctly_for_gpu_fragcoord()
    {
        // 4:3 viewport on a 16:9 swapchain → pillarboxing (bars on left/right).
        // Tile grid now covers only the physical viewport, not the full swapchain.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(512f, 384f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(1024, 768),
            swapchainSizePx: new Vector2D<int>(1920, 1080),
            sdfScale: 1f);

        // Physical viewport: 1440x1080, offset (240, 0).
        // TileCounts should use physical size → ceil(1440/64) x ceil(1080/64) = 23 x 17.
        const int tileSizePx = 64;
        var tileCounts = TiledLightCullingCpu.TileCounts(in cam, tileSizePx);
        Assert.Equal(23, tileCounts.X);
        Assert.Equal(17, tileCounts.Y);

        // Light at camera center projects to swapchain center (960, 540).
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(512f, 384f), Radius = 30f, Intensity = 1f },
        };

        const int maxPerTile = 128;
        var totalTiles = tileCounts.X * tileCounts.Y;
        var bins = new TiledLightCullingCpu.TileBin[totalTiles];
        var indices = new int[totalTiles * maxPerTile];
        TiledLightCullingCpu.Bin(lights, 1, in cam, tileSizePx, maxPerTile, bins, indices);

        // Match the GPU shader: subtract physical offset before computing tile index.
        var swPx = cam.WorldToSwapchainPx(lights[0].PositionWorld);
        var tileX = (int)Math.Floor((swPx.X - cam.PhysicalOffsetSwapchainPx.X) / tileSizePx);
        var tileY = (int)Math.Floor((swPx.Y - cam.PhysicalOffsetSwapchainPx.Y) / tileSizePx);
        var tileIdx = tileY * tileCounts.X + tileX;

        Assert.True(tileIdx >= 0 && tileIdx < totalTiles,
            $"Computed tile index {tileIdx} out of range [0, {totalTiles}).");
        Assert.True(bins[tileIdx].Count > 0,
            "Center tile must contain the light — CPU/GPU agreement on pillarboxed viewport.");
    }
}
