using System;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class LightViewportCullingTests
{
    private static ShadowSdfCamera MakeCam(
        Vector2D<float> posWorld = default,
        float rotation = 0f,
        int vpW = 320,
        int vpH = 240,
        int swapW = -1,
        int swapH = -1)
    {
        if (swapW < 0) swapW = vpW;
        if (swapH < 0) swapH = vpH;
        return ShadowSdfCamera.SyntheticCamera(
            posWorld,
            rotation,
            new Vector2D<int>(vpW, vpH),
            new Vector2D<int>(swapW, swapH),
            sdfScale: 1f);
    }

    [Fact]
    public void CullPointLights_keeps_onscreen_lights()
    {
        var cam = MakeCam();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(0f, 0f), Radius = 50f, Intensity = 1f, Color = new Vector3D<float>(1, 1, 1) },
        };
        var n = LightViewportCulling.CullPointLights(lights, 1, in cam);
        Assert.Equal(1, n);
    }

    [Fact]
    public void CullPointLights_removes_far_offscreen_light()
    {
        var cam = MakeCam();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(5000f, 5000f), Radius = 10f, Intensity = 1f, Color = new Vector3D<float>(1, 1, 1) },
        };
        var n = LightViewportCulling.CullPointLights(lights, 1, in cam);
        Assert.Equal(0, n);
    }

    [Fact]
    public void CullPointLights_keeps_edge_overlapping_light()
    {
        var cam = MakeCam();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(160f + 25f, 0f), Radius = 30f, Intensity = 1f, Color = new Vector3D<float>(1, 1, 1) },
        };
        var n = LightViewportCulling.CullPointLights(lights, 1, in cam);
        Assert.Equal(1, n);
    }

    [Fact]
    public void CullPointLights_compacts_mixed_array()
    {
        var cam = MakeCam();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(0f, 0f), Radius = 20f, Intensity = 1f, Color = new Vector3D<float>(1, 0, 0) },
            new PointLight { PositionWorld = new Vector2D<float>(9999f, 9999f), Radius = 5f, Intensity = 1f, Color = new Vector3D<float>(0, 1, 0) },
            new PointLight { PositionWorld = new Vector2D<float>(50f, 50f), Radius = 30f, Intensity = 1f, Color = new Vector3D<float>(0, 0, 1) },
        };
        var n = LightViewportCulling.CullPointLights(lights, 3, in cam);
        Assert.Equal(2, n);
        Assert.Equal(1f, lights[0].Color.X);
        Assert.Equal(1f, lights[1].Color.Z);
    }

    [Fact]
    public void CullPointLights_returns_count_when_zero_or_negative()
    {
        var cam = MakeCam();
        Assert.Equal(0, LightViewportCulling.CullPointLights([], 0, in cam));
        Assert.Equal(-1, LightViewportCulling.CullPointLights([], -1, in cam));
    }

    [Fact]
    public void CullPointLights_drops_all_when_scale_zero()
    {
        // default(ShadowSdfCamera) has PhysicalScale == 0 — world→swapchain is degenerate, so all lights are dropped.
        var cam = default(ShadowSdfCamera);
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(9999f, 9999f), Radius = 5f, Intensity = 1f },
        };
        Assert.Equal(0, LightViewportCulling.CullPointLights(lights, 1, in cam));
    }

    [Fact]
    public void CullSpotLights_keeps_onscreen_and_removes_offscreen()
    {
        var cam = MakeCam();
        var lights = new[]
        {
            new SpotLight { PositionWorld = new Vector2D<float>(0f, 0f), Radius = 50f, DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(1, 1, 1) },
            new SpotLight { PositionWorld = new Vector2D<float>(9999f, 9999f), Radius = 5f, DirectionWorld = new Vector2D<float>(0f, 1f), Intensity = 1f, Color = new Vector3D<float>(1, 1, 1) },
        };
        var n = LightViewportCulling.CullSpotLights(lights, 2, in cam);
        Assert.Equal(1, n);
        Assert.Equal(0f, lights[0].PositionWorld.X);
    }

    [Fact]
    public void CullSpotLights_returns_count_when_zero()
    {
        var cam = MakeCam();
        Assert.Equal(0, LightViewportCulling.CullSpotLights([], 0, in cam));
    }

    [Fact]
    public void CullSpotLights_drops_all_when_scale_zero()
    {
        var camZero = default(ShadowSdfCamera);
        var lights = new[] { new SpotLight { PositionWorld = new Vector2D<float>(9999f, 9999f), Radius = 5f } };
        Assert.Equal(0, LightViewportCulling.CullSpotLights(lights, 1, in camZero));
    }

    [Fact]
    public void CullSpotLights_compacts_mixed_array()
    {
        var cam = MakeCam();
        var lights = new[]
        {
            new SpotLight { PositionWorld = new Vector2D<float>(0f, 0f), Radius = 50f, DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(1, 0, 0) },
            new SpotLight { PositionWorld = new Vector2D<float>(9999f, 9999f), Radius = 5f, DirectionWorld = new Vector2D<float>(0f, 1f), Intensity = 1f, Color = new Vector3D<float>(0, 1, 0) },
            new SpotLight { PositionWorld = new Vector2D<float>(50f, 50f), Radius = 30f, DirectionWorld = new Vector2D<float>(0f, -1f), Intensity = 1f, Color = new Vector3D<float>(0, 0, 1) },
        };
        var n = LightViewportCulling.CullSpotLights(lights, 3, in cam);
        Assert.Equal(2, n);
        Assert.Equal(1f, lights[0].Color.X);
        Assert.Equal(1f, lights[1].Color.Z);
    }

    [Fact]
    public void CullPointLights_works_with_rotated_camera()
    {
        var cam = MakeCam(rotation: MathF.PI / 4f);
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(0f, 0f), Radius = 30f, Intensity = 1f, Color = new Vector3D<float>(1, 1, 1) },
            new PointLight { PositionWorld = new Vector2D<float>(5000f, 5000f), Radius = 10f, Intensity = 1f, Color = new Vector3D<float>(1, 1, 1) },
        };
        var n = LightViewportCulling.CullPointLights(lights, 2, in cam);
        Assert.Equal(1, n);
    }

    [Fact]
    public void CullPointLights_large_radius_keeps_offcenter_light()
    {
        var cam = MakeCam();
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(500f, 0f), Radius = 600f, Intensity = 1f, Color = new Vector3D<float>(1, 1, 1) },
        };
        var n = LightViewportCulling.CullPointLights(lights, 1, in cam);
        Assert.Equal(1, n);
    }

    [Fact]
    public void CullPointLights_rejects_light_in_letterbox_bar()
    {
        // Viewport 320x240 in a 640x240 swapchain → centered with 160px bars on each side.
        var cam = MakeCam(vpW: 320, vpH: 240, swapW: 640, swapH: 240);
        // Light at world (0,0) projects to center of viewport, then offset → swapchain x=320 → inside physical rect.
        // Light at world (-300, 0) projects well to the left → swapchain x < 160 → inside left letterbox bar.
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(0f, 0f), Radius = 20f, Intensity = 1f, Color = new Vector3D<float>(1, 0, 0) },
            new PointLight { PositionWorld = new Vector2D<float>(-300f, 0f), Radius = 10f, Intensity = 1f, Color = new Vector3D<float>(0, 1, 0) },
        };
        var n = LightViewportCulling.CullPointLights(lights, 2, in cam);
        Assert.Equal(1, n);
        Assert.Equal(1f, lights[0].Color.X);
    }

    [Fact]
    public void CullSpotLights_rejects_light_in_letterbox_bar()
    {
        var cam = MakeCam(vpW: 320, vpH: 240, swapW: 640, swapH: 240);
        var lights = new[]
        {
            new SpotLight { PositionWorld = new Vector2D<float>(0f, 0f), Radius = 20f, DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(1, 0, 0) },
            new SpotLight { PositionWorld = new Vector2D<float>(-300f, 0f), Radius = 10f, DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0, 1, 0) },
        };
        var n = LightViewportCulling.CullSpotLights(lights, 2, in cam);
        Assert.Equal(1, n);
        Assert.Equal(1f, lights[0].Color.X);
    }

    [Fact]
    public void CullSpotLights_wide_cone_rotated_camera_keeps_onscreen()
    {
        // Wide 60° spot aimed world-right, camera rotated 45°. The cone is on-screen (at camera pos)
        // and must NOT be culled — this is the regression case for the space-mismatch bug.
        var cam = MakeCam(rotation: MathF.PI / 4f);
        var lights = new[]
        {
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f),
                DirectionWorld = new Vector2D<float>(1f, 0f),
                Radius = 100f,
                OuterConeRadians = MathF.PI / 3f,
                Intensity = 1f,
                Color = new Vector3D<float>(1, 1, 1),
            },
        };
        var n = LightViewportCulling.CullSpotLights(lights, 1, in cam);
        Assert.Equal(1, n);
    }
}
