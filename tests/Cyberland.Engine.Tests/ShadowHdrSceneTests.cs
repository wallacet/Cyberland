using System.Text.Json;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Regression tests for the HDR demo scene's SDF cone-trace shadow pipeline. Asserts via the canonical CPU mirror
/// (<see cref="ShadowSdfSamplingCpu"/>) that the scene produces hard shadow behind pillars and fully-lit visibility
/// in open areas.
/// </summary>
/// <remarks>
/// All coordinates in this file are in <b>world</b> space (+Y up, pixels). The CPU mirror runs the same math the
/// GLSL <c>sdfSoftShadow</c> performs at runtime. Pillar and light positions are parsed from the canonical
/// <c>mods/Cyberland.Demo/Content/Scenes/hdr.json</c> scene file so changes to the scene propagate into tests.
/// </remarks>
public sealed class ShadowHdrSceneTests
{
    private const string HdrSceneRelativePath = "../../../../mods/Cyberland.Demo/Content/Scenes/hdr.json";

    private static ShadowSdfCamera HdrCamera() => ShadowSdfCamera.SyntheticCamera(
        cameraPosWorld: new Vector2D<float>(640f, 360f),
        cameraRotRadians: 0f,
        viewportSizeWorld: new Vector2D<int>(1280, 720),
        swapchainSizePx: new Vector2D<int>(1280, 720),
        sdfScale: 1f);

    /// <summary>
    /// Reads pillar occluder entities (sprites with <c>castsShadow: true</c>) from the HDR scene JSON.
    /// Returns them ordered by localX ascending so index 0 = center pillar (x≈553), index 1 = right pillar (x≈920).
    /// </summary>
    private static ShadowOccluder2D[] ReadPillarsFromScene()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, HdrSceneRelativePath));
        using var doc = JsonDocument.Parse(json);
        var entities = doc.RootElement.GetProperty("entities");
        var pillars = new List<ShadowOccluder2D>();
        foreach (var entity in entities.EnumerateArray())
        {
            var components = entity.GetProperty("components");
            JsonElement transformData = default;
            JsonElement spriteData = default;
            var castsShadow = false;
            foreach (var comp in components.EnumerateArray())
            {
                var type = comp.GetProperty("type").GetString();
                if (type == "cyberland.engine/transform")
                    transformData = comp.GetProperty("data");
                else if (type == "cyberland.engine/sprite")
                {
                    spriteData = comp.GetProperty("data");
                    if (spriteData.TryGetProperty("castsShadow", out var cs) && cs.GetBoolean())
                        castsShadow = true;
                }
            }
            if (!castsShadow) continue;
            if (spriteData.ValueKind == JsonValueKind.Undefined) continue;
            if (!spriteData.TryGetProperty("halfExtents", out var he)) continue;

            var x = transformData.TryGetProperty("localX", out var lx) ? (float)lx.GetDouble() : 0f;
            var y = transformData.TryGetProperty("localY", out var ly) ? (float)ly.GetDouble() : 0f;
            var hx = (float)he.GetProperty("x").GetDouble();
            var hy = (float)he.GetProperty("y").GetDouble();
            pillars.Add(new ShadowOccluder2D(new Vector2D<float>(x, y), new Vector2D<float>(hx, hy), 0f));
        }
        pillars.Sort((a, b) => a.CenterWorld.X.CompareTo(b.CenterWorld.X));
        return pillars.ToArray();
    }

    /// <summary>Reads spot-light position from the HDR scene (entity with <c>cyberland.engine/spot-light</c>).</summary>
    private static Vector2D<float> ReadSpotLightPosition()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, HdrSceneRelativePath));
        using var doc = JsonDocument.Parse(json);
        foreach (var entity in doc.RootElement.GetProperty("entities").EnumerateArray())
        {
            var components = entity.GetProperty("components");
            var hasSpot = false;
            JsonElement transformData = default;
            foreach (var comp in components.EnumerateArray())
            {
                var type = comp.GetProperty("type").GetString();
                if (type == "cyberland.engine/spot-light")
                    hasSpot = true;
                else if (type == "cyberland.engine/transform")
                    transformData = comp.GetProperty("data");
            }
            if (!hasSpot) continue;
            var x = transformData.TryGetProperty("localX", out var lx) ? (float)lx.GetDouble() : 0f;
            var y = transformData.TryGetProperty("localY", out var ly) ? (float)ly.GetDouble() : 0f;
            return new Vector2D<float>(x, y);
        }
        throw new InvalidOperationException("No spot-light entity found in HDR scene JSON.");
    }

    /// <summary>Reads warm point-light position (entity with <c>cyberland.demo/warm-point-tag</c>).</summary>
    private static Vector2D<float> ReadWarmPointLightPosition()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, HdrSceneRelativePath));
        using var doc = JsonDocument.Parse(json);
        foreach (var entity in doc.RootElement.GetProperty("entities").EnumerateArray())
        {
            var components = entity.GetProperty("components");
            var hasTag = false;
            JsonElement transformData = default;
            foreach (var comp in components.EnumerateArray())
            {
                var type = comp.GetProperty("type").GetString();
                if (type == "cyberland.demo/warm-point-tag")
                    hasTag = true;
                else if (type == "cyberland.engine/transform")
                    transformData = comp.GetProperty("data");
            }
            if (!hasTag) continue;
            var x = transformData.TryGetProperty("localX", out var lx) ? (float)lx.GetDouble() : 0f;
            var y = transformData.TryGetProperty("localY", out var ly) ? (float)ly.GetDouble() : 0f;
            return new Vector2D<float>(x, y);
        }
        throw new InvalidOperationException("No warm-point-tag entity found in HDR scene JSON.");
    }

    [Fact]
    public void HdrSpot_occludes_fragment_behind_center_pillar()
    {
        var pillars = ReadPillarsFromScene();
        Assert.True(pillars.Length >= 1, "Expected at least one shadow-occluder pillar in hdr.json");
        var centerPillar = pillars[0];
        var cam = HdrCamera();
        var sdf = ShadowDistanceFieldCpu.Build(new[] { centerPillar }, in cam);

        var lightWorld = ReadSpotLightPosition();
        var fragWorld = new Vector2D<float>(centerPillar.CenterWorld.X + 150f, centerPillar.CenterWorld.Y);

        var vis = ShadowSdfSamplingCpu.SoftShadow(fragWorld, lightWorld, sdf, in cam);
        Assert.True(vis < 0.1f, $"expected occluded (<0.1) behind pillar, got {vis}");
    }

    [Fact]
    public void HdrSpot_open_cone_is_fully_lit()
    {
        var pillars = ReadPillarsFromScene();
        Assert.True(pillars.Length >= 2, "Expected at least two shadow-occluder pillars in hdr.json");
        var cam = HdrCamera();
        var sdf = ShadowDistanceFieldCpu.Build(pillars, in cam);

        var lightWorld = ReadSpotLightPosition();
        // Midpoint between light and center pillar — in the open cone before occlusion.
        var openConeWorld = new Vector2D<float>(
            (lightWorld.X + pillars[0].CenterWorld.X) * 0.5f,
            (lightWorld.Y + pillars[0].CenterWorld.Y) * 0.5f);

        var vis = ShadowSdfSamplingCpu.SoftShadow(openConeWorld, lightWorld, sdf, in cam);
        Assert.True(vis > 0.85f, $"expected nearly fully lit before pillar, got {vis}");
    }

    [Fact]
    public void HdrWarmPoint_occludes_fragment_behind_right_pillar()
    {
        var pillars = ReadPillarsFromScene();
        Assert.True(pillars.Length >= 2, "Expected at least two shadow-occluder pillars in hdr.json");
        var rightPillar = pillars[^1];
        var cam = HdrCamera();
        var sdf = ShadowDistanceFieldCpu.Build(new[] { rightPillar }, in cam);

        var lightWorld = ReadWarmPointLightPosition();
        var fragWorld = new Vector2D<float>(rightPillar.CenterWorld.X - 70f, rightPillar.CenterWorld.Y);

        var vis = ShadowSdfSamplingCpu.SoftShadow(fragWorld, lightWorld, sdf, in cam);
        Assert.True(vis < 0.1f, $"expected occluded behind right pillar, got {vis}");
    }

    [Fact]
    public void HdrWarmPoint_open_sight_line_is_fully_lit()
    {
        var pillars = ReadPillarsFromScene();
        Assert.True(pillars.Length >= 2, "Expected at least two shadow-occluder pillars in hdr.json");
        var rightPillar = pillars[^1];
        var cam = HdrCamera();
        var sdf = ShadowDistanceFieldCpu.Build(new[] { rightPillar }, in cam);

        var lightWorld = ReadWarmPointLightPosition();
        // Point past the light, away from the pillar — no occluder between this fragment and the warm point light.
        var openWorld = new Vector2D<float>(lightWorld.X + 130f, lightWorld.Y - 16f);

        var vis = ShadowSdfSamplingCpu.SoftShadow(openWorld, lightWorld, sdf, in cam);
        Assert.True(vis > 0.85f, $"expected fully lit on open line of sight, got {vis}");
    }

    [Fact]
    public void HdrScene_empty_occluder_set_returns_full_visibility()
    {
        var cam = HdrCamera();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);

        var lightWorld = ReadSpotLightPosition();
        var fragWorld = new Vector2D<float>(700f, 373f);

        var vis = ShadowSdfSamplingCpu.SoftShadow(fragWorld, lightWorld, sdf, in cam);
        Assert.Equal(1f, vis, 3);
    }

    [Fact]
    public void DirectionalShadow_occludes_fragment_behind_occluder()
    {
        var cam = HdrCamera();
        var occluder = new ShadowOccluder2D(
            centerWorld: new Vector2D<float>(640f, 360f),
            halfExtentsWorld: new Vector2D<float>(40f, 80f),
            rotationRadians: 0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        // Directional light coming from the right (+X direction toward the light).
        var lightDirWorld = new Vector2D<float>(1f, 0f);
        const float traceWorldDist = 500f;

        // Fragment to the left of the occluder — the occluder blocks the ray toward +X.
        var fragBehindWorld = new Vector2D<float>(500f, 360f);
        var visBehind = ShadowSdfSamplingCpu.DirectionalSoftShadow(
            fragBehindWorld, lightDirWorld, traceWorldDist, sdf, in cam);
        Assert.True(visBehind < 0.1f, $"expected occluded behind occluder, got {visBehind}");

        // Fragment to the right of the occluder — nothing between it and the light direction.
        var fragOpenWorld = new Vector2D<float>(780f, 360f);
        var visOpen = ShadowSdfSamplingCpu.DirectionalSoftShadow(
            fragOpenWorld, lightDirWorld, traceWorldDist, sdf, in cam);
        Assert.True(visOpen > 0.85f, $"expected fully lit on open side, got {visOpen}");
    }

    [Fact]
    public void DirectionalShadow_no_occluders_returns_full_visibility()
    {
        var cam = HdrCamera();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);

        var lightDirWorld = new Vector2D<float>(0f, 1f);
        var fragWorld = new Vector2D<float>(640f, 360f);
        var vis = ShadowSdfSamplingCpu.DirectionalSoftShadow(
            fragWorld, lightDirWorld, 400f, sdf, in cam);
        Assert.Equal(1f, vis, 3);
    }

    [Fact]
    public void HdrScene_shadows_disabled_all_positive_sdf_returns_full_visibility()
    {
        // When Shadows.Enabled == false, the renderer builds no occluders, producing an all-65500f SDF.
        // The cone-trace must return 1.0 for every fragment regardless of geometry.
        var cam = HdrCamera();
        var sdfSize = cam.SdfSizePx;
        var sdf = new float[sdfSize.X * sdfSize.Y];
        Array.Fill(sdf, 65500f);

        var lightWorld = ReadSpotLightPosition();
        var fragWorld = new Vector2D<float>(700f, 373f);

        var vis = ShadowSdfSamplingCpu.SoftShadow(fragWorld, lightWorld, sdf, in cam);
        Assert.Equal(1f, vis, 3);
    }
}
