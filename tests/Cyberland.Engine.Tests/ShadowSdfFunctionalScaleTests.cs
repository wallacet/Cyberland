using System.Diagnostics;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Functional scale tests for the SDF cone-trace shadow pipeline at RTS-scale (hundreds of dynamic shadow-casting lights).
/// These tests validate algorithmic correctness, deterministic ordering, and reasonable CPU wall-time bounds for the
/// shared mirror path. GPU performance is verified separately via the host (<c>Run-CyberlandDemo-Test.ps1 -Demo rts</c>);
/// the in-process tests here are the canary that the algorithm doesn't blow up under realistic light counts.
/// </summary>
/// <remarks>
/// <para>
/// All inputs in <b>world</b> space (+Y up). The CPU SDF uses the same <see cref="ShadowSdfCamera"/> the renderer
/// constructs each frame so the test's results match what the renderer would produce.
/// </para>
/// </remarks>
public sealed class ShadowSdfFunctionalScaleTests
{
    private static ShadowSdfCamera SmallCamera() => ShadowSdfCamera.SyntheticCamera(
        cameraPosWorld: new Vector2D<float>(64f, 36f),
        cameraRotRadians: 0f,
        viewportSizeWorld: new Vector2D<int>(128, 72),
        swapchainSizePx: new Vector2D<int>(128, 72),
        sdfScale: 1f);

    [Fact]
    public void Stress_500_lights_against_50_occluders_traces_in_finite_time()
    {
        var cam = SmallCamera();
        var occluders = new ShadowOccluder2D[50];
        for (var i = 0; i < occluders.Length; i++)
        {
            var x = 8f + (i % 10) * 12f;
            var y = 8f + (i / 10) * 12f;
            occluders[i] = new ShadowOccluder2D(new Vector2D<float>(x, y), new Vector2D<float>(2f, 2f), 0f);
        }
        var sdf = ShadowDistanceFieldCpu.Build(occluders, in cam);

        var sw = Stopwatch.StartNew();
        var litCount = 0;
        var occludedCount = 0;
        for (var li = 0; li < 500; li++)
        {
            var lightWorld = new Vector2D<float>((li * 1.3f) % 128f, (li * 0.7f) % 72f);
            var fragWorld = new Vector2D<float>((li * 5.1f + 17f) % 128f, (li * 3.2f + 11f) % 72f);
            var vis = ShadowSdfSamplingCpu.SoftShadow(fragWorld, lightWorld, sdf, in cam);
            if (vis > 0.5f) litCount++; else occludedCount++;
        }
        sw.Stop();

        // 500 traces × 32 samples ≈ 16K SDF taps; CPU mirror should complete well under a second.
        Assert.True(sw.ElapsedMilliseconds < 5000, $"500-light SDF trace took {sw.ElapsedMilliseconds} ms");
        Assert.True(litCount > 0, "expected some lit fragments");
        Assert.True(occludedCount > 0, "expected some occluded fragments");
    }

    [Fact]
    public void Stress_emissive_promotion_handles_hundreds_of_bright_sprites()
    {
        var sprites = new SpriteDrawRequest[600];
        for (var i = 0; i < sprites.Length; i++)
        {
            sprites[i] = new SpriteDrawRequest
            {
                CenterWorld = new Vector2D<float>(i * 2f, i * 1.5f),
                HalfExtentsWorld = new Vector2D<float>(8f, 8f),
                EmissiveTint = new Vector3D<float>(0.5f, 0.7f, 1f),
                EmissiveIntensity = 2f,
                Space = CoordinateSpace.WorldSpace,
                ColorMultiply = new Vector4D<float>(1, 1, 1, 1),
                Alpha = 1f,
                UvRect = new Vector4D<float>(0, 0, 1, 1),
            };
        }

        Span<PointLight> dst = stackalloc PointLight[256];
        var n = EmissiveLightPromotionCpu.Promote(sprites, sprites.Length, 1.5f, 3f, 1f, maxPromoted: 256, dst);
        Assert.Equal(256, n);
        // First sprite emits at (0, 0); last should be (510, 382.5).
        Assert.Equal(0f, dst[0].PositionWorld.X);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void SdfScale_non_unity_produces_correct_shadow_visibility(float sdfScale)
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(640f, 360f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(1280, 720),
            swapchainSizePx: new Vector2D<int>(1280, 720),
            sdfScale: sdfScale);

        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(40f, 200f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        // Ray that must be blocked by the occluder — frag and light on opposite sides.
        var visBlocked = ShadowSdfSamplingCpu.SoftShadow(
            fragWorld: new Vector2D<float>(800f, 360f),
            lightWorld: new Vector2D<float>(400f, 360f),
            sdf, in cam);
        Assert.True(visBlocked < 0.1f, $"Expected blocked at sdfScale={sdfScale}, got {visBlocked}");

        // Ray that must be clear — frag and light both below the occluder's bottom edge (y=160).
        var visClear = ShadowSdfSamplingCpu.SoftShadow(
            fragWorld: new Vector2D<float>(800f, 100f),
            lightWorld: new Vector2D<float>(400f, 100f),
            sdf, in cam);
        Assert.True(visClear > 0.5f, $"Expected clear at sdfScale={sdfScale}, got {visClear}");
    }

    [Fact]
    public void Zero_occluders_produces_fully_lit_sdf()
    {
        var cam = SmallCamera();
        var sdf = ShadowDistanceFieldCpu.Build(ReadOnlySpan<ShadowOccluder2D>.Empty, in cam);

        // Every texel should be large positive (no shadows) because nothing blocks light.
        var minVal = float.MaxValue;
        for (var i = 0; i < sdf.Length; i++)
        {
            if (sdf[i] < minVal)
                minVal = sdf[i];
        }
        Assert.True(minVal > 100f, $"Expected all SDF values to be large positive with no occluders, min was {minVal}");

        // A cone trace through the empty SDF should return fully lit (visibility ≈ 1.0).
        var vis = ShadowSdfSamplingCpu.SoftShadow(
            fragWorld: new Vector2D<float>(80f, 36f),
            lightWorld: new Vector2D<float>(40f, 36f),
            sdf, in cam);
        Assert.True(vis > 0.99f, $"Expected fully lit with no occluders, got {vis}");
    }

    [Fact]
    public void Promoted_emissive_light_always_casts_shadow_even_when_sprite_does_not()
    {
        var sprites = new SpriteDrawRequest[1];
        sprites[0] = new SpriteDrawRequest
        {
            CenterWorld = new Vector2D<float>(100f, 50f),
            HalfExtentsWorld = new Vector2D<float>(8f, 8f),
            EmissiveTint = new Vector3D<float>(1f, 1f, 1f),
            EmissiveIntensity = 3f,
            Space = CoordinateSpace.WorldSpace,
            ColorMultiply = new Vector4D<float>(1, 1, 1, 1),
            Alpha = 1f,
            UvRect = new Vector4D<float>(0, 0, 1, 1),
            CastsShadow = false,
        };

        Span<PointLight> dst = stackalloc PointLight[4];
        var n = EmissiveLightPromotionCpu.Promote(sprites, sprites.Length, 1.5f, 3f, 1f, maxPromoted: 4, dst);
        Assert.Equal(1, n);
        Assert.True(dst[0].CastsShadow, "Promoted light must always cast shadows regardless of sprite.CastsShadow");
    }

    [Fact]
    public void Stress_tile_culling_500_lights_clamps_to_per_tile_cap()
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            new Vector2D<float>(640f, 360f),
            0f,
            new Vector2D<int>(1280, 720),
            new Vector2D<int>(1280, 720),
            sdfScale: 1f);
        var lights = new PointLight[500];
        for (var i = 0; i < lights.Length; i++)
        {
            lights[i] = new PointLight
            {
                PositionWorld = new Vector2D<float>(640f, 360f),
                Radius = 1000f,
            };
        }
        var tileCounts = TiledLightCullingCpu.TileCounts(in cam, DeferredRenderingConstants.TileSizeSwapchainPx);
        Span<TiledLightCullingCpu.TileBin> bins = stackalloc TiledLightCullingCpu.TileBin[tileCounts.X * tileCounts.Y];
        var indices = new int[bins.Length * DeferredRenderingConstants.MaxLightsPerTile];
        TiledLightCullingCpu.Bin(
            lights, lights.Length, in cam,
            DeferredRenderingConstants.TileSizeSwapchainPx,
            DeferredRenderingConstants.MaxLightsPerTile,
            bins, indices);
        for (var i = 0; i < bins.Length; i++)
        {
            Assert.True(bins[i].Count <= DeferredRenderingConstants.MaxLightsPerTile);
        }
    }
}
