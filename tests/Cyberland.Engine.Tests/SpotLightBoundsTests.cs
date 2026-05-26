using System;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class SpotLightBoundsTests
{
    [Fact]
    public void ComputeConeAabb_narrow_cone_encloses_apex_and_edge_rays()
    {
        var pos = new Vector2D<float>(100f, 100f);
        var dir = new Vector2D<float>(1f, 0f);
        float radius = 50f;
        float outerAngle = MathF.PI / 8f; // 22.5° — narrow, won't trigger wide-cone branch

        SpotLightBounds.ComputeConeAabb(pos, dir, radius, outerAngle, out var aabbMin, out var aabbMax);

        // Apex must be enclosed.
        Assert.True(aabbMin.X <= pos.X && aabbMin.Y <= pos.Y);
        Assert.True(aabbMax.X >= pos.X && aabbMax.Y >= pos.Y);
        // Edge ray endpoints at cos(outerAngle)*radius reach must be inside.
        var edgeReach = MathF.Cos(outerAngle) * radius;
        Assert.True(aabbMax.X >= pos.X + edgeReach - 0.01f);
    }

    [Fact]
    public void ComputeConeAabb_wide_cone_includes_midpoint()
    {
        // outerAngle > π/4 triggers the wide-cone branch (lines 60-65).
        var pos = new Vector2D<float>(50f, 50f);
        var dir = new Vector2D<float>(0f, 1f);
        float radius = 80f;
        float outerAngle = MathF.PI / 3f; // 60° — wide

        SpotLightBounds.ComputeConeAabb(pos, dir, radius, outerAngle, out var aabbMin, out var aabbMax);

        // The midpoint (pos + dir * radius) must be inside the AABB.
        var midX = pos.X + dir.X * radius;
        var midY = pos.Y + dir.Y * radius;
        Assert.True(aabbMin.X <= midX && aabbMax.X >= midX);
        Assert.True(aabbMin.Y <= midY && aabbMax.Y >= midY);

        // AABB should be wider than if only edge rays were considered (sanity check).
        Assert.True(aabbMax.Y >= pos.Y + radius * 0.9f);
    }

    [Fact]
    public void ComputeConeAabb_very_wide_cone_bounds_larger_than_narrow()
    {
        var pos = new Vector2D<float>(0f, 0f);
        var dir = new Vector2D<float>(1f, 0f);
        float radius = 100f;

        SpotLightBounds.ComputeConeAabb(pos, dir, radius, MathF.PI / 8f, out var narrowMin, out var narrowMax);
        SpotLightBounds.ComputeConeAabb(pos, dir, radius, MathF.PI / 2.5f, out var wideMin, out var wideMax);

        float narrowArea = (narrowMax.X - narrowMin.X) * (narrowMax.Y - narrowMin.Y);
        float wideArea = (wideMax.X - wideMin.X) * (wideMax.Y - wideMin.Y);
        Assert.True(wideArea >= narrowArea, $"Wide cone AABB area ({wideArea}) should be >= narrow ({narrowArea})");
    }

    [Fact]
    public void ComputeProjectedConeAabb_rotated_camera_covers_projected_cone()
    {
        // 60° half-angle cone aimed world-right, with a 45° camera rotation.
        // The projected AABB must enclose the apex and both edge endpoints after projection.
        var light = new SpotLight
        {
            PositionWorld = new Vector2D<float>(100f, 100f),
            DirectionWorld = new Vector2D<float>(1f, 0f),
            Radius = 80f,
            OuterConeRadians = MathF.PI / 3f,
            Intensity = 1f,
            Color = new Vector3D<float>(1, 1, 1),
        };
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(100f, 100f),
            cameraRotRadians: MathF.PI / 4f,
            viewportSizeWorld: new Vector2D<int>(320, 240),
            swapchainSizePx: new Vector2D<int>(320, 240),
            sdfScale: 1f);

        SpotLightBounds.ComputeProjectedConeAabb(in light, in cam,
            out var minX, out var minY, out var maxX, out var maxY);

        // All key world-space vertices should project inside the AABB.
        var apexSw = cam.WorldToSwapchainPx(light.PositionWorld);
        Assert.True(minX <= apexSw.X + 0.01f && maxX >= apexSw.X - 0.01f, "Apex X not enclosed");
        Assert.True(minY <= apexSw.Y + 0.01f && maxY >= apexSw.Y - 0.01f, "Apex Y not enclosed");

        var cosA = MathF.Cos(light.OuterConeRadians);
        var sinA = MathF.Sin(light.OuterConeRadians);
        var d = light.DirectionWorld;
        var r = light.Radius;

        var ep0World = new Vector2D<float>(
            light.PositionWorld.X + (d.X * cosA - d.Y * sinA) * r,
            light.PositionWorld.Y + (d.X * sinA + d.Y * cosA) * r);
        var ep0Sw = cam.WorldToSwapchainPx(ep0World);
        Assert.True(minX <= ep0Sw.X + 0.01f && maxX >= ep0Sw.X - 0.01f, "Edge+ X not enclosed");
        Assert.True(minY <= ep0Sw.Y + 0.01f && maxY >= ep0Sw.Y - 0.01f, "Edge+ Y not enclosed");

        var ep1World = new Vector2D<float>(
            light.PositionWorld.X + (d.X * cosA + d.Y * sinA) * r,
            light.PositionWorld.Y + (-d.X * sinA + d.Y * cosA) * r);
        var ep1Sw = cam.WorldToSwapchainPx(ep1World);
        Assert.True(minX <= ep1Sw.X + 0.01f && maxX >= ep1Sw.X - 0.01f, "Edge- X not enclosed");
        Assert.True(minY <= ep1Sw.Y + 0.01f && maxY >= ep1Sw.Y - 0.01f, "Edge- Y not enclosed");

        // Wide cone midpoint should also be enclosed.
        var midWorld = new Vector2D<float>(
            light.PositionWorld.X + d.X * r,
            light.PositionWorld.Y + d.Y * r);
        var midSw = cam.WorldToSwapchainPx(midWorld);
        Assert.True(minX <= midSw.X + 0.01f && maxX >= midSw.X - 0.01f, "Mid X not enclosed");
        Assert.True(minY <= midSw.Y + 0.01f && maxY >= midSw.Y - 0.01f, "Mid Y not enclosed");
    }

    [Fact]
    public void ComputeProjectedConeAabb_zero_rotation_matches_flat_aabb()
    {
        // With zero camera rotation the projected AABB should agree with ComputeConeAabb
        // (up to Y-flip since swapchain is +Y down and ComputeConeAabb is space-agnostic).
        var light = new SpotLight
        {
            PositionWorld = new Vector2D<float>(50f, 50f),
            DirectionWorld = new Vector2D<float>(0f, 1f),
            Radius = 60f,
            OuterConeRadians = MathF.PI / 6f,
            Intensity = 1f,
            Color = new Vector3D<float>(1, 1, 1),
        };
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(50f, 50f),
            cameraRotRadians: 0f,
            viewportSizeWorld: new Vector2D<int>(400, 300),
            swapchainSizePx: new Vector2D<int>(400, 300),
            sdfScale: 1f);

        SpotLightBounds.ComputeProjectedConeAabb(in light, in cam,
            out var pMinX, out var pMinY, out var pMaxX, out var pMaxY);

        // AABB must have non-zero area
        Assert.True(pMaxX - pMinX > 1f, "Projected AABB too narrow in X");
        Assert.True(pMaxY - pMinY > 1f, "Projected AABB too narrow in Y");

        // Apex projects to viewport center
        var apexSw = cam.WorldToSwapchainPx(light.PositionWorld);
        Assert.True(pMinX <= apexSw.X + 0.01f && pMaxX >= apexSw.X - 0.01f);
    }
}
