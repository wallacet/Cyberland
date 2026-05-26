using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class ShadowSdfSamplingCpuTests
{
    private static ShadowSdfCamera DefaultCam() => ShadowSdfCamera.SyntheticCamera(
        cameraPosWorld: new Vector2D<float>(640f, 360f),
        cameraRotRadians: 0f,
        viewportSizeWorld: new Vector2D<int>(1280, 720),
        swapchainSizePx: new Vector2D<int>(1280, 720),
        sdfScale: 1f);

    [Fact]
    public void SoftShadow_returns_one_when_light_coincides_with_fragment()
    {
        var cam = DefaultCam();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        var pWorld = new Vector2D<float>(640f, 360f);
        var vis = ShadowSdfSamplingCpu.SoftShadow(pWorld, pWorld, sdf, in cam);
        Assert.Equal(1f, vis, 3);
    }

    [Fact]
    public void SoftShadow_returns_zero_when_occluder_blocks_path()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(50f, 200f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var vis = ShadowSdfSamplingCpu.SoftShadow(
            fragWorld: new Vector2D<float>(900f, 360f),
            lightWorld: new Vector2D<float>(300f, 360f),
            sdf, in cam);
        Assert.True(vis < 0.05f);
    }

    [Fact]
    public void SoftShadow_returns_one_in_open_line_of_sight()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(10f, 10f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var vis = ShadowSdfSamplingCpu.SoftShadow(
            fragWorld: new Vector2D<float>(900f, 600f),
            lightWorld: new Vector2D<float>(800f, 600f),
            sdf, in cam);
        Assert.True(vis > 0.95f);
    }

    [Fact]
    public void DirectionalSoftShadow_returns_one_for_zero_direction()
    {
        var cam = DefaultCam();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        var vis = ShadowSdfSamplingCpu.DirectionalSoftShadow(
            fragWorld: new Vector2D<float>(640f, 360f),
            lightDirWorld: new Vector2D<float>(0f, 0f),
            traceWorldDist: 500f,
            sdf, in cam);
        Assert.Equal(1f, vis, 3);
    }

    [Fact]
    public void DirectionalSoftShadow_traces_along_light_direction()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(700f, 360f),
            new Vector2D<float>(30f, 30f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        // Frag at (600, 360); light direction +X world. The occluder at (700, 360) lies along the ray.
        var vis = ShadowSdfSamplingCpu.DirectionalSoftShadow(
            fragWorld: new Vector2D<float>(600f, 360f),
            lightDirWorld: new Vector2D<float>(1f, 0f),
            traceWorldDist: 400f,
            sdf, in cam);
        Assert.True(vis < 0.1f);
    }

    [Fact]
    public void SoftShadow_visibility_is_clamped_to_unit_range()
    {
        var cam = DefaultCam();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        var vis = ShadowSdfSamplingCpu.SoftShadow(
            fragWorld: new Vector2D<float>(640f, 360f),
            lightWorld: new Vector2D<float>(800f, 360f),
            sdf, in cam,
            kSoft: 1e6f);
        Assert.True(vis >= 0f && vis <= 1f);
    }

    [Fact]
    public void SoftShadow_returns_one_when_shadows_disabled_no_occluders()
    {
        // When shadows are disabled, the renderer builds no occluders → SDF is all-positive.
        // Every frag-light pair should return fully lit (1.0).
        var cam = DefaultCam();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        foreach (var (frag, light) in new[]
        {
            (new Vector2D<float>(100f, 100f), new Vector2D<float>(1200f, 600f)),
            (new Vector2D<float>(640f, 360f), new Vector2D<float>(640f, 500f)),
            (new Vector2D<float>(300f, 200f), new Vector2D<float>(900f, 600f)),
        })
        {
            var vis = ShadowSdfSamplingCpu.SoftShadow(frag, light, sdf, in cam);
            Assert.Equal(1f, vis, 3);
        }
    }

    [Fact]
    public void SpotCone_penumbra_boundary_partial_visibility()
    {
        // A small square occluder creates a bounded shadow cone. A fragment whose ray to the light
        // just grazes the occluder edge should get partial visibility via the Quilez k*d/t ratio.
        var cam = DefaultCam();
        // Small pillar (15px half-width, 15px half-height) at the center of the viewport.
        // World-to-swapchain for DefaultCam: swapchain = (worldX, 720 - worldY).
        // Swapchain occluder: center (640, 360), x [625,655], y [345,375].
        // Shadow boundary at x=800 in swapchain spans y ~333..387; world y ~333..387 maps to
        // swapchain y=720-worldY so the boundary in world y is ~333..387.
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(15f, 15f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        var lightWorld = new Vector2D<float>(400f, 360f);

        // Fragment directly behind the pillar: fully occluded.
        var fragBlocked = new Vector2D<float>(800f, 360f);
        var visBlocked = ShadowSdfSamplingCpu.SoftShadow(fragBlocked, lightWorld, sdf, in cam);
        Assert.True(visBlocked < 0.05f, $"blocked fragment should be near zero, got {visBlocked}");

        // Fragment well outside the geometric shadow (world y=400 -> swapchain y=320, shadow
        // boundary at swapchain y~333). With low kSoft the Quilez ratio near the edge gives
        // partial visibility.
        var fragEdge = new Vector2D<float>(800f, 400f);
        var visEdge = ShadowSdfSamplingCpu.SoftShadow(fragEdge, lightWorld, sdf, in cam, kSoft: 4f);
        Assert.True(visEdge > 0f, $"penumbra fragment should not be fully occluded, got {visEdge}");
        Assert.True(visEdge < 1f, $"penumbra fragment should not be fully lit, got {visEdge}");
    }

    [Fact]
    public void DirectionalSoftShadow_under_camera_rotation_pi_over_4()
    {
        // Camera rotated pi/4 radians; directional trace along +X should still correctly occlude.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(640f, 360f),
            cameraRotRadians: MathF.PI / 4f,
            viewportSizeWorld: new Vector2D<int>(1280, 720),
            swapchainSizePx: new Vector2D<int>(1280, 720),
            sdfScale: 1f);

        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(750f, 360f),
            new Vector2D<float>(30f, 30f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        // Trace from (600, 360) toward +X: occluder at (750, 360) blocks the ray.
        var vis = ShadowSdfSamplingCpu.DirectionalSoftShadow(
            fragWorld: new Vector2D<float>(600f, 360f),
            lightDirWorld: new Vector2D<float>(1f, 0f),
            traceWorldDist: 400f,
            sdf, in cam);
        Assert.True(vis < 0.1f, $"expected occluded under rotated camera, got {vis}");
    }

    [Fact]
    public void SoftShadow_returns_one_when_sdf_all_65500f()
    {
        // Shadows.Enabled=false UBO path: SDF is filled with 65500f (fully lit sentinel).
        var cam = DefaultCam();
        var sdfSize = cam.SdfSizePx;
        var sdf = new float[sdfSize.X * sdfSize.Y];
        Array.Fill(sdf, 65500f);

        // Multiple fragment-light pairs should all return 1.0.
        foreach (var (frag, light) in new[]
        {
            (new Vector2D<float>(100f, 100f), new Vector2D<float>(1200f, 600f)),
            (new Vector2D<float>(640f, 360f), new Vector2D<float>(640f, 500f)),
            (new Vector2D<float>(300f, 200f), new Vector2D<float>(900f, 600f)),
        })
        {
            var vis = ShadowSdfSamplingCpu.SoftShadow(frag, light, sdf, in cam);
            Assert.Equal(1f, vis, 3);
        }
    }

    [Fact]
    public void DirectionalSoftShadow_depthBias_avoids_acne_near_occluder_edge()
    {
        var cam = DefaultCam();
        // Occluder at center; frag just beside it. Without bias, the first sample may hit the occluder
        // surface and false-shadow. With a non-zero bias the march starts past the surface.
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(20f, 200f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        // Fragment 25 world-px to the right of the occluder edge; direction +X (toward the light).
        var fragWorld = new Vector2D<float>(685f, 360f);
        var lightDir = new Vector2D<float>(1f, 0f);
        // Zero bias: near-surface fragment might get self-shadow acne.
        var visNoBias = ShadowSdfSamplingCpu.DirectionalSoftShadow(
            fragWorld, lightDir, 400f, sdf, in cam, depthBiasWorld: 0f);
        // Non-zero bias: initial march step jumps past the near-surface zone.
        var visWithBias = ShadowSdfSamplingCpu.DirectionalSoftShadow(
            fragWorld, lightDir, 400f, sdf, in cam, depthBiasWorld: 5f);
        // Bias version should be at least as lit (≥) as zero-bias; ideally fully lit for a clear path.
        Assert.True(visWithBias >= visNoBias,
            $"bias should improve visibility near occluder edge: noBias={visNoBias}, withBias={visWithBias}");
        Assert.True(visWithBias > 0.5f, $"expected mostly lit with bias, got {visWithBias}");
    }

    [Fact]
    public void SoftShadow_maxSamples_one_still_produces_valid_result()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(50f, 200f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        // With maxSamples=1, only one step is taken; result should still be in [0,1].
        var vis = ShadowSdfSamplingCpu.SoftShadow(
            fragWorld: new Vector2D<float>(900f, 360f),
            lightWorld: new Vector2D<float>(300f, 360f),
            sdf, in cam,
            maxSamples: 1);
        Assert.True(vis >= 0f && vis <= 1f, $"expected [0,1], got {vis}");
    }

    [Fact]
    public void SoftShadow_hard_hit_returns_zero()
    {
        var cam = DefaultCam();
        // Large occluder so the march immediately hits a texel with distSwapchainPx < 0.05.
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(200f, 200f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        // Fragment and light on opposite sides; ray goes straight through the occluder body.
        var vis = ShadowSdfSamplingCpu.SoftShadow(
            fragWorld: new Vector2D<float>(500f, 360f),
            lightWorld: new Vector2D<float>(800f, 360f),
            sdf, in cam);
        Assert.Equal(0f, vis, 3);
    }

    [Fact]
    public void DirectionalSoftShadow_returns_one_when_sdf_all_65500f()
    {
        // Mirrors the disabled-path for directional lights: renderer fills SDF with 65500f sentinel
        // when Shadows.Enabled=false (RecordShadowSdfPass clears to 65500f and skips JFA).
        var cam = DefaultCam();
        var sdfSize = cam.SdfSizePx;
        var sdf = new float[sdfSize.X * sdfSize.Y];
        Array.Fill(sdf, 65500f);

        foreach (var dir in new[]
        {
            new Vector2D<float>(1f, 0f),
            new Vector2D<float>(0f, 1f),
            new Vector2D<float>(-0.7f, 0.7f),
        })
        {
            var vis = ShadowSdfSamplingCpu.DirectionalSoftShadow(
                fragWorld: new Vector2D<float>(640f, 360f),
                lightDirWorld: dir,
                traceWorldDist: 500f,
                sdf, in cam);
            Assert.Equal(1f, vis, 3);
        }
    }

    [Fact]
    public void SwapchainFrag_returns_one_when_light_coincides_with_fragment()
    {
        var cam = DefaultCam();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        var pWorld = new Vector2D<float>(640f, 360f);
        var fragSwapchainPx = cam.WorldToSwapchainPx(pWorld);
        var vis = ShadowSdfSamplingCpu.SoftShadowSwapchainFrag(fragSwapchainPx, pWorld, sdf, in cam);
        Assert.Equal(1f, vis, 3);
    }

    [Fact]
    public void SwapchainFrag_returns_zero_when_occluder_blocks_path()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(50f, 200f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var fragWorld = new Vector2D<float>(900f, 360f);
        var fragSwapchainPx = cam.WorldToSwapchainPx(fragWorld);
        var vis = ShadowSdfSamplingCpu.SoftShadowSwapchainFrag(
            fragSwapchainPx,
            lightWorld: new Vector2D<float>(300f, 360f),
            sdf, in cam);
        Assert.True(vis < 0.05f);
    }

    [Fact]
    public void SwapchainFrag_returns_one_in_open_line_of_sight()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(10f, 10f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var fragWorld = new Vector2D<float>(900f, 600f);
        var fragSwapchainPx = cam.WorldToSwapchainPx(fragWorld);
        var vis = ShadowSdfSamplingCpu.SoftShadowSwapchainFrag(
            fragSwapchainPx,
            lightWorld: new Vector2D<float>(800f, 600f),
            sdf, in cam);
        Assert.True(vis > 0.95f);
    }

    [Fact]
    public void SwapchainFrag_matches_original_SoftShadow()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(30f, 30f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var fragWorld = new Vector2D<float>(750f, 360f);
        var lightWorld = new Vector2D<float>(500f, 360f);
        var fragSwapchainPx = cam.WorldToSwapchainPx(fragWorld);

        var original = ShadowSdfSamplingCpu.SoftShadow(fragWorld, lightWorld, sdf, in cam);
        var optimized = ShadowSdfSamplingCpu.SoftShadowSwapchainFrag(fragSwapchainPx, lightWorld, sdf, in cam);
        Assert.Equal(original, optimized, 3);
    }

    [Fact]
    public void SwapchainFrag_partial_occlusion_reduces_visibility()
    {
        var cam = DefaultCam();
        // Small occluder placed slightly off the direct path to create soft penumbra.
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(600f, 370f),
            new Vector2D<float>(8f, 8f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var fragWorld = new Vector2D<float>(800f, 360f);
        var lightWorld = new Vector2D<float>(400f, 360f);
        var fragSwapchainPx = cam.WorldToSwapchainPx(fragWorld);
        var vis = ShadowSdfSamplingCpu.SoftShadowSwapchainFrag(fragSwapchainPx, lightWorld, sdf, in cam,
            kSoft: 4f);
        Assert.True(vis > 0f && vis < 1f, $"Expected partial visibility, got {vis}");
    }

    [Fact]
    public void DirectionalSwapchainFrag_returns_one_for_zero_direction()
    {
        var cam = DefaultCam();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        var fragWorld = new Vector2D<float>(640f, 360f);
        var fragSwapchainPx = cam.WorldToSwapchainPx(fragWorld);
        var vis = ShadowSdfSamplingCpu.DirectionalSoftShadowSwapchainFrag(
            fragSwapchainPx, fragWorld,
            Vector2D<float>.Zero,
            500f, sdf, in cam);
        Assert.Equal(1f, vis, 3);
    }

    [Fact]
    public void DirectionalSwapchainFrag_matches_original()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(30f, 30f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var fragWorld = new Vector2D<float>(750f, 360f);
        var fragSwapchainPx = cam.WorldToSwapchainPx(fragWorld);
        var dir = new Vector2D<float>(1f, 0f);

        var original = ShadowSdfSamplingCpu.DirectionalSoftShadow(fragWorld, dir, 500f, sdf, in cam);
        var optimized = ShadowSdfSamplingCpu.DirectionalSoftShadowSwapchainFrag(
            fragSwapchainPx, fragWorld, dir, 500f, sdf, in cam);
        Assert.Equal(original, optimized, 3);
    }

    [Fact]
    public void SwapchainFragSwapchainLight_returns_one_when_coincident()
    {
        var cam = DefaultCam();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        var px = cam.WorldToSwapchainPx(new Vector2D<float>(640f, 360f));
        var vis = ShadowSdfSamplingCpu.SoftShadowSwapchainFragSwapchainLight(px, px, sdf, in cam);
        Assert.Equal(1f, vis, 3);
    }

    [Fact]
    public void SwapchainFragSwapchainLight_returns_zero_when_occluded()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(50f, 200f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var fragPx = cam.WorldToSwapchainPx(new Vector2D<float>(900f, 360f));
        var lightPx = cam.WorldToSwapchainPx(new Vector2D<float>(300f, 360f));
        var vis = ShadowSdfSamplingCpu.SoftShadowSwapchainFragSwapchainLight(fragPx, lightPx, sdf, in cam);
        Assert.True(vis < 0.05f);
    }

    [Fact]
    public void SwapchainFragSwapchainLight_matches_SwapchainFrag()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(30f, 30f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var fragWorld = new Vector2D<float>(750f, 360f);
        var lightWorld = new Vector2D<float>(500f, 360f);
        var fragPx = cam.WorldToSwapchainPx(fragWorld);
        var lightPx = cam.WorldToSwapchainPx(lightWorld);

        var original = ShadowSdfSamplingCpu.SoftShadowSwapchainFrag(fragPx, lightWorld, sdf, in cam);
        var optimized = ShadowSdfSamplingCpu.SoftShadowSwapchainFragSwapchainLight(fragPx, lightPx, sdf, in cam);
        Assert.Equal(original, optimized, 3);
    }

    [Fact]
    public void SwapchainFragSwapchainLight_returns_one_in_open_line_of_sight()
    {
        var cam = DefaultCam();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        var fragPx = cam.WorldToSwapchainPx(new Vector2D<float>(900f, 600f));
        var lightPx = cam.WorldToSwapchainPx(new Vector2D<float>(800f, 600f));
        var vis = ShadowSdfSamplingCpu.SoftShadowSwapchainFragSwapchainLight(fragPx, lightPx, sdf, in cam);
        Assert.True(vis > 0.95f, $"Expected fully lit in open LOS, got {vis}");
    }

    [Fact]
    public void SwapchainFragSwapchainLight_partial_occlusion_reduces_visibility()
    {
        var cam = DefaultCam();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(600f, 370f),
            new Vector2D<float>(8f, 8f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        var fragPx = cam.WorldToSwapchainPx(new Vector2D<float>(800f, 360f));
        var lightPx = cam.WorldToSwapchainPx(new Vector2D<float>(400f, 360f));
        var vis = ShadowSdfSamplingCpu.SoftShadowSwapchainFragSwapchainLight(fragPx, lightPx, sdf, in cam,
            kSoft: 4f);
        Assert.True(vis > 0f && vis < 1f, $"Expected partial visibility, got {vis}");
    }

    [Fact]
    public void OccluderAtCorner_produces_negative_sdf()
    {
        // Regression for P0 #2: an occluder at the extreme top-left must produce negative SDF at texel (0,0).
        // Before the half-texel encoding fix, the JFA seed at (0,0) aliased with the sentinel and was lost.
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(64f, 64f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(128, 128),
            swapchainSizePx: new Vector2D<int>(128, 128),
            sdfScale: 1f);

        // Place occluder at the camera position so its world center maps to the viewport center.
        // Shift it toward the top-left corner of the viewport so it covers texel (0,0) in the SDF.
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(4f, 124f),
            new Vector2D<float>(10f, 10f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        // Texel (0,0) should be inside the occluder → negative distance.
        Assert.True(sdf[0] < 0f, $"Expected negative SDF at corner texel (0,0), got {sdf[0]}");
    }
}
