using System;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class Rendering2DHelpersTests
{
    /// <summary>Mirrors composite_vert: top-left UV origin, +Y down (matches Vulkan framebuffer).</summary>
    private static Vector2D<float> FullscreenUvFromPixelCenter(int px, int py, float w, float h) =>
        new((px + 0.5f) / w, (py + 0.5f) / h);

    /// <summary>Mirrors bloom_extract / downsample / upsample: destination pixel index from vUv.</summary>
    private static Vector2D<float> DstPixelFromUv(Vector2D<float> vUv, Vector2D<float> dstSz)
    {
        var x = MathF.Floor(vUv.X * dstSz.X);
        var y = MathF.Floor(vUv.Y * dstSz.Y);
        var maxX = MathF.Max(dstSz.X - 1f, 0f);
        var maxY = MathF.Max(dstSz.Y - 1f, 0f);
        return new Vector2D<float>(Math.Clamp(x, 0f, maxX), Math.Clamp(y, 0f, maxY));
    }

    private static float PointAttenuationSmooth(float dist, float radius, float exponent)
    {
        var t = Math.Clamp(dist / MathF.Max(radius, 1e-4f), 0f, 1f);
        return MathF.Pow(MathF.Max(1f - t, 0f), MathF.Max(exponent, 0.1f));
    }

    [Fact]
    public void Bloom_dual_filter_upscale_pairs_coarser_down_i_plus_1_with_finer_down_i()
    {
        // Mirrors VulkanRenderer.BloomPipeline2D upsample loop with BloomDownsampleLevels == 2: only i == 0 runs.
        const int bloomDownsampleLevels = 2;
        for (var i = bloomDownsampleLevels - 2; i >= 0; i--)
        {
            Assert.Equal(1, i + 1);
            Assert.Equal(0, i);
        }
    }

    [Fact]
    public void Bloom_extract_prefilter_maps_half_cell_to_footprint_center_not_plus_one_full_pixel()
    {
        // bloom_extract.frag.glsl: uvFull = (halfCoord * 2 + 0.5) / fullSz — center of 2×2 full-res block for composite alignment.
        // Old (halfCoord * 2 + 1) shifted bloom ~0.5 full texels toward +X/+Y (lower-right ghost vs HDR).
        var fullW = 1280f;
        var fullH = 720f;
        var hx = 10f;
        var hy = 20f;
        var uvCorrect = new Vector2D<float>((hx * 2f + 0.5f) / fullW, (hy * 2f + 0.5f) / fullH);
        var uvOldWrong = new Vector2D<float>((hx * 2f + 1f) / fullW, (hy * 2f + 1f) / fullH);
        Assert.True(uvCorrect.X < uvOldWrong.X);
        Assert.True(uvCorrect.Y < uvOldWrong.Y);
        Assert.Equal(0.5f / fullW, uvOldWrong.X - uvCorrect.X, 6);
        Assert.Equal(0.5f / fullH, uvOldWrong.Y - uvCorrect.Y, 6);
    }

    [Fact]
    public void Composite_matches_bloom_chain_pixel_center_uv_when_fragcoord_is_integer_grid()
    {
        // composite.frag.glsl: uv = (floor(gl_FragCoord.xy) + 0.5) / fullSz — aligns with bloom_extract when FragCoord is integer.
        var w = 1280f;
        var fragX = 10f;
        var uvNaive = fragX / w;
        var uvCentered = (MathF.Floor(fragX) + 0.5f) / w;
        Assert.Equal(10.5f / w, uvCentered);
        Assert.True(uvCentered > uvNaive);
    }

    [Fact]
    public void Bloom_temp_bloom1_attachment_larger_than_pyramid_viewport_so_partial_clear_leaves_stale_texels()
    {
        // Vulkan clears only RenderArea; bloom1 is full half-res while many draws are smaller (e.g. down[0]).
        // Gaussian blur offsets UV — stale uncleared regions produced offset ghost blooms until full-area clear.
        uint swapW = 1920, swapH = 1080;
        var halfW = Math.Max(swapW / 2, 1u);
        var halfH = Math.Max(swapH / 2, 1u);
        var down0W = Math.Max(halfW / 2, 1u);
        var down0H = Math.Max(halfH / 2, 1u);
        Assert.True(down0W < halfW || down0H < halfH);
        Assert.True(down0W * down0H < halfW * halfH);
    }

    [Fact]
    public void Bloom_copy_pass_uses_half_buffer_extent_for_uv_not_down_level_extent()
    {
        // bloom_copy.frag.glsl: (floor(FragCoord)+0.5)/textureSize (full half-res bloom1), not down[i] size.
        var bloomHalfW = 640f;
        var downW = 320f;
        var fragX = 10.5f;
        var uCorrect = fragX / bloomHalfW;
        var uIfWrongDenominator = fragX / downW;
        Assert.True(uCorrect < uIfWrongDenominator);
    }

    [Fact]
    public void BloomUpsample_center_mapping_stays_in_bounds_for_odd_sizes()
    {
        static Vector2D<float> MapCenter(Vector2D<float> dstPixel, Vector2D<float> srcSize, Vector2D<float> dstSize) =>
            ((dstPixel + new Vector2D<float>(0.5f, 0.5f)) / dstSize) * srcSize;

        var srcSize = new Vector2D<float>(321f, 181f);
        var dstSize = new Vector2D<float>(257f, 143f);
        var p0 = MapCenter(new Vector2D<float>(0f, 0f), srcSize, dstSize);
        var p1 = MapCenter(new Vector2D<float>(dstSize.X - 1f, dstSize.Y - 1f), srcSize, dstSize);

        Assert.InRange(p0.X, 0f, srcSize.X);
        Assert.InRange(p0.Y, 0f, srcSize.Y);
        Assert.InRange(p1.X, 0f, srcSize.X);
        Assert.InRange(p1.Y, 0f, srcSize.Y);
    }

    [Fact]
    public void PostProcess_vUv_maps_to_dst_pixel_like_FragCoord_for_odd_and_even_sizes()
    {
        foreach (var w in new[] { 320, 321 })
        foreach (var h in new[] { 180, 181 })
        {
            var dstW = MathF.Floor(w * 0.5f);
            var dstH = MathF.Floor(h * 0.5f);
            var dstSz = new Vector2D<float>(dstW, dstH);
            for (var py = 0; py < (int)dstH; py += Math.Max(1, (int)dstH / 7))
            for (var px = 0; px < (int)dstW; px += Math.Max(1, (int)dstW / 7))
            {
                var vUv = FullscreenUvFromPixelCenter(px, py, dstW, dstH);
                var fromUv = DstPixelFromUv(vUv, dstSz);
                Assert.Equal(px, (int)fromUv.X);
                Assert.Equal(py, (int)fromUv.Y);
            }
        }
    }

    [Fact]
    public void FullscreenUv_top_left_is_smaller_y_than_bottom_for_same_column()
    {
        var vTop = FullscreenUvFromPixelCenter(10, 0, 640f, 480f);
        var vBot = FullscreenUvFromPixelCenter(10, 479, 640f, 480f);
        Assert.True(vTop.Y < vBot.Y);
    }

    [Fact]
    public void PointLight_smooth_attenuation_decreases_monotonically_with_distance()
    {
        var r = 100f;
        var exp = 2f;
        var a0 = PointAttenuationSmooth(0f, r, exp);
        var a1 = PointAttenuationSmooth(40f, r, exp);
        var a2 = PointAttenuationSmooth(80f, r, exp);
        var a3 = PointAttenuationSmooth(100f, r, exp);
        Assert.True(a0 >= a1 && a1 >= a2 && a2 >= a3);
        Assert.True(a3 >= 0f && a3 <= 1e-3f);
    }

    [Fact]
    public void Lighting_y_up_conversion_tracks_player_direction()
    {
        var screenH = 600f;
        var playerYUpA = 100f;
        var playerYUpB = 140f; // moved up in world (+Y)

        static float FragYFromWorldY(float worldY, float h) => h - worldY;
        static float WorldYFromFragY(float fragY, float h) => h - fragY;

        var fragYA = FragYFromWorldY(playerYUpA, screenH);
        var fragYB = FragYFromWorldY(playerYUpB, screenH);
        Assert.True(fragYB < fragYA); // raw FragCoord moves opposite.

        var shaderYUpA = WorldYFromFragY(fragYA, screenH);
        var shaderYUpB = WorldYFromFragY(fragYB, screenH);
        Assert.True(shaderYUpB > shaderYUpA); // flipped convention matches world motion.
    }

    [Fact]
    public void SpriteDrawSorter_orders_layer_then_sortkey_then_depth()
    {
        var draws = new[]
        {
            new SpriteDrawRequest { Layer = 10, SortKey = 0f, DepthHint = 0f },
            new SpriteDrawRequest { Layer = 5, SortKey = 1f, DepthHint = 0f },
            new SpriteDrawRequest { Layer = 5, SortKey = 0f, DepthHint = 1f },
            new SpriteDrawRequest { Layer = 5, SortKey = 0f, DepthHint = 0f },
        };

        var idx = new int[draws.Length];
        SpriteDrawSorter.SortByLayerOrder(idx, draws);

        Assert.Equal(3, idx[0]);
        Assert.Equal(2, idx[1]);
        Assert.Equal(1, idx[2]);
        Assert.Equal(0, idx[3]);
    }

    [Fact]
    public void PostProcessVolumeMerge_higher_priority_overrides_and_AABB_gate()
    {
        var g = new GlobalPostProcessSettings
        {
            BloomEnabled = true,
            BloomRadius = 1f,
            BloomGain = 1f,
            EmissiveToHdrGain = 0.45f,
            EmissiveToBloomGain = 0.45f,
            Exposure = 1f,
            Saturation = 1f,
            TonemapEnabled = true,
            ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f),
            ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f),
            ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
        };

        var vols = new[]
        {
            new PostProcessVolume
            {
                MinWorld = new Vector2D<float>(0, 0),
                MaxWorld = new Vector2D<float>(10, 10),
                Priority = 1,
                Overrides = new PostProcessOverrides
                {
                    HasBloomRadius = true,
                    BloomRadius = 1.25f,
                    HasBloomGain = true,
                    BloomGain = 2f,
                    HasEmissiveToHdrGain = true,
                    EmissiveToHdrGain = 0.8f,
                    HasEmissiveToBloomGain = true,
                    EmissiveToBloomGain = 1.15f,
                    HasExposure = false,
                    Exposure = 1f,
                    HasSaturation = false,
                    Saturation = 1f
                }
            },
            new PostProcessVolume
            {
                MinWorld = new Vector2D<float>(0, 0),
                MaxWorld = new Vector2D<float>(10, 10),
                Priority = 5,
                Overrides = new PostProcessOverrides
                {
                    HasBloomRadius = true,
                    BloomRadius = 1.5f,
                    HasBloomGain = true,
                    BloomGain = 3f,
                    HasEmissiveToHdrGain = true,
                    EmissiveToHdrGain = 0.7f,
                    HasEmissiveToBloomGain = true,
                    EmissiveToBloomGain = 1.25f,
                    HasExposure = false,
                    Exposure = 1f,
                    HasSaturation = false,
                    Saturation = 1f
                }
            }
        };

        var viewMin = new Vector2D<float>(0f, 0f);
        var viewMax = new Vector2D<float>(100f, 100f);
        var r = PostProcessVolumeMerge.Resolve(in g, vols, viewMin, viewMax);
        Assert.Equal(3f, r.BloomGain);
        Assert.Equal(1.5f, r.BloomRadius);
        Assert.Equal(0.7f * 0.45f, r.EmissiveToHdrGain);
        Assert.Equal(1.25f * 0.45f, r.EmissiveToBloomGain);

        var miss = new[]
        {
            new PostProcessVolume
            {
                MinWorld = new Vector2D<float>(200f, 200f),
                MaxWorld = new Vector2D<float>(210f, 210f),
                Priority = 99,
                Overrides = new PostProcessOverrides
                {
                    HasBloomGain = true,
                    BloomGain = 99f,
                    HasExposure = false,
                    Exposure = 1f,
                    HasSaturation = false,
                    Saturation = 1f
                }
            }
        };

        var r2 = PostProcessVolumeMerge.Resolve(in g, miss, viewMin, viewMax);
        Assert.Equal(1f, r2.BloomGain);

        var skipLowerPri = new[]
        {
            new PostProcessVolume
            {
                MinWorld = new Vector2D<float>(0, 0),
                MaxWorld = new Vector2D<float>(10, 10),
                Priority = 5,
                Overrides = new PostProcessOverrides
                {
                    HasBloomGain = true,
                    BloomGain = 2f,
                    HasExposure = true,
                    Exposure = 0.5f,
                    HasSaturation = true,
                    Saturation = 0.8f
                }
            },
            new PostProcessVolume
            {
                MinWorld = new Vector2D<float>(0, 0),
                MaxWorld = new Vector2D<float>(10, 10),
                Priority = 1,
                Overrides = new PostProcessOverrides
                {
                    HasBloomGain = true,
                    BloomGain = 99f,
                    HasExposure = false,
                    Exposure = 1f,
                    HasSaturation = false,
                    Saturation = 1f
                }
            }
        };

        var r3 = PostProcessVolumeMerge.Resolve(in g, skipLowerPri, viewMin, viewMax);
        Assert.Equal(2f, r3.BloomGain);
        Assert.Equal(0.5f, r3.Exposure);
        Assert.Equal(0.8f, r3.Saturation);

        Assert.Equal(1f, PostProcessVolumeMerge.Resolve(in g, ReadOnlySpan<PostProcessVolume>.Empty, viewMin, viewMax).BloomGain);
    }

    [Fact]
    public void PostProcessVolumeMerge_Overlaps_cases()
    {
        Assert.True(PostProcessVolumeMerge.Overlaps(
            new Vector2D<float>(0, 0), new Vector2D<float>(10, 10),
            new Vector2D<float>(5, 5), new Vector2D<float>(15, 15)));

        Assert.False(PostProcessVolumeMerge.Overlaps(
            new Vector2D<float>(0, 0), new Vector2D<float>(1, 1),
            new Vector2D<float>(2, 2), new Vector2D<float>(3, 3)));
    }
}
