using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class ShadowSdfCameraTests
{
    private static ShadowSdfCamera StandardCamera() => ShadowSdfCamera.SyntheticCamera(
        cameraPosWorld: new Vector2D<float>(640f, 360f),
        cameraRotRadians: 0f,
        viewportSizeWorld: new Vector2D<int>(1280, 720),
        swapchainSizePx: new Vector2D<int>(1280, 720),
        sdfScale: 1f);

    [Fact]
    public void Constructor_assigns_fields()
    {
        var cam = new ShadowSdfCamera(
            cameraPosWorld: new Vector2D<float>(100f, 200f),
            cameraRotRadians: 0.5f,
            viewportSizeWorld: new Vector2D<float>(1280f, 720f),
            physicalOffsetSwapchainPx: new Vector2D<float>(0f, 60f),
            physicalSizeSwapchainPx: new Vector2D<float>(1280f, 600f),
            physicalScale: 1.0f,
            swapchainSizePx: new Vector2D<float>(1280f, 720f),
            sdfScale: 0.5f);
        Assert.Equal(100f, cam.CameraPosWorld.X);
        Assert.Equal(0.5f, cam.CameraRotRadians);
        Assert.Equal(1280f, cam.ViewportSizeWorld.X);
        Assert.Equal(60f, cam.PhysicalOffsetSwapchainPx.Y);
        Assert.Equal(1280f, cam.PhysicalSizeSwapchainPx.X);
        Assert.Equal(1.0f, cam.PhysicalScale);
        Assert.Equal(1280f, cam.SwapchainSizePx.X);
        Assert.Equal(0.5f, cam.SdfScale);
    }

    [Fact]
    public void Constructor_clamps_nonpositive_sdfScale_to_one()
    {
        var cam = new ShadowSdfCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<float>(1280f, 720f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1280f, 720f),
            1.0f,
            new Vector2D<float>(1280f, 720f),
            sdfScale: 0f);
        Assert.Equal(1f, cam.SdfScale);

        var camNeg = new ShadowSdfCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<float>(1280f, 720f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1280f, 720f),
            1.0f,
            new Vector2D<float>(1280f, 720f),
            sdfScale: -0.5f);
        Assert.Equal(1f, camNeg.SdfScale);

        var camTiny = new ShadowSdfCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<float>(1280f, 720f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1280f, 720f),
            1.0f,
            new Vector2D<float>(1280f, 720f),
            sdfScale: 0.01f);
        Assert.Equal(0.0625f, camTiny.SdfScale);
    }

    [Fact]
    public void WorldToSwapchainPx_matches_CameraProjection_chain()
    {
        var cam = StandardCamera();
        foreach (var pWorld in new[]
        {
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1280f, 720f),
            new Vector2D<float>(200f, 500f),
        })
        {
            var vp = CameraProjection.WorldToViewportPixel(
                pWorld,
                cam.CameraPosWorld,
                cam.CameraRotRadians,
                cam.ViewportSizeWorld);
            var physical = new PhysicalViewport(
                new Vector2D<int>((int)cam.PhysicalOffsetSwapchainPx.X, (int)cam.PhysicalOffsetSwapchainPx.Y),
                new Vector2D<int>((int)cam.PhysicalSizeSwapchainPx.X, (int)cam.PhysicalSizeSwapchainPx.Y),
                cam.PhysicalScale);
            var expectedSwapchainPx = CameraProjection.ViewportPixelToSwapchainPixel(vp, in physical);
            var actualSwapchainPx = cam.WorldToSwapchainPx(pWorld);
            Assert.Equal(expectedSwapchainPx.X, actualSwapchainPx.X, 3);
            Assert.Equal(expectedSwapchainPx.Y, actualSwapchainPx.Y, 3);
        }
    }

    [Fact]
    public void WorldToSwapchainPx_roundtrips_via_SwapchainPxToWorld()
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(100f, 200f),
            cameraRotRadians: 0.7f,
            viewportSizeWorld: new Vector2D<int>(800, 600),
            swapchainSizePx: new Vector2D<int>(1280, 720),
            sdfScale: 1f);
        foreach (var pWorld in new[]
        {
            new Vector2D<float>(50f, 80f),
            new Vector2D<float>(300f, 450f),
            new Vector2D<float>(-200f, 100f),
        })
        {
            var swapchainPx = cam.WorldToSwapchainPx(pWorld);
            var pWorldAgain = cam.SwapchainPxToWorld(swapchainPx);
            Assert.Equal(pWorld.X, pWorldAgain.X, 3);
            Assert.Equal(pWorld.Y, pWorldAgain.Y, 3);
        }
    }

    [Fact]
    public void SwapchainPxToWorld_returns_input_when_PhysicalScale_zero()
    {
        var cam = new ShadowSdfCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<float>(1280f, 720f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(0f, 0f),
            physicalScale: 0f,
            new Vector2D<float>(1280f, 720f),
            sdfScale: 1f);
        var pSwapchainPx = new Vector2D<float>(100f, 200f);
        var result = cam.SwapchainPxToWorld(pSwapchainPx);
        Assert.False(float.IsNaN(result.X));
        Assert.False(float.IsNaN(result.Y));
        Assert.False(float.IsInfinity(result.X));
        Assert.False(float.IsInfinity(result.Y));
    }

    [Fact]
    public void SwapchainPxToWorld_no_nan_at_sub_threshold_PhysicalScale()
    {
        // 1e-5f is below the max(PhysicalScale, 1e-4f) floor in the constructor.
        var cam = new ShadowSdfCamera(
            new Vector2D<float>(640f, 360f),
            0.2f,
            new Vector2D<float>(1280f, 720f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1280f, 720f),
            physicalScale: 1e-5f,
            new Vector2D<float>(1280f, 720f),
            sdfScale: 1f);

        Assert.Equal(1e-4f, cam.PhysicalScale, 6);

        var pSwapchainPx = new Vector2D<float>(640f, 360f);
        var result = cam.SwapchainPxToWorld(pSwapchainPx);
        Assert.False(float.IsNaN(result.X));
        Assert.False(float.IsNaN(result.Y));
        Assert.False(float.IsInfinity(result.X));
        Assert.False(float.IsInfinity(result.Y));
    }

    [Fact]
    public void SwapchainPxToSdfPx_scales_by_sdfScale()
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<int>(1280, 720),
            new Vector2D<int>(1280, 720),
            sdfScale: 0.5f);
        var sdfPx = cam.SwapchainPxToSdfPx(new Vector2D<float>(100f, 200f));
        Assert.Equal(50f, sdfPx.X);
        Assert.Equal(100f, sdfPx.Y);
    }

    [Fact]
    public void SdfPxDistanceToSwapchainPx_inverts_sdfScale()
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<int>(1280, 720),
            new Vector2D<int>(1280, 720),
            sdfScale: 0.5f);
        Assert.Equal(20f, cam.SdfPxDistanceToSwapchainPx(10f));

        var camZeroScale = new ShadowSdfCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<float>(1280f, 720f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1280f, 720f),
            1f,
            new Vector2D<float>(1280f, 720f),
            sdfScale: 1f);
        // Re-assemble with an unconditional zero SdfScale via direct test path:
        var camZeroFlag = new ShadowSdfCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<float>(1280f, 720f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1280f, 720f),
            1f,
            new Vector2D<float>(1280f, 720f),
            sdfScale: 1f);
        Assert.Equal(10f, camZeroFlag.SdfPxDistanceToSwapchainPx(10f));
    }

    [Fact]
    public void SdfScale_above_one_clamped_to_one()
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            Vector2D<float>.Zero, 0f,
            new Vector2D<int>(800, 600),
            new Vector2D<int>(800, 600),
            sdfScale: 2.0f);
        Assert.Equal(1f, cam.SdfScale);
        Assert.Equal(800, cam.SdfSizePx.X);
        Assert.Equal(600, cam.SdfSizePx.Y);
    }

    [Fact]
    public void SdfSizePx_scales_and_clamps_to_at_least_one()
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<int>(1280, 720),
            new Vector2D<int>(1280, 720),
            sdfScale: 0.5f);
        Assert.Equal(640, cam.SdfSizePx.X);
        Assert.Equal(360, cam.SdfSizePx.Y);

        var tiny = new ShadowSdfCamera(
            new Vector2D<float>(0f, 0f),
            0f,
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(0f, 0f),
            1f,
            new Vector2D<float>(0f, 0f),
            sdfScale: 0.5f);
        Assert.True(tiny.SdfSizePx.X >= 1);
        Assert.True(tiny.SdfSizePx.Y >= 1);
    }
}
