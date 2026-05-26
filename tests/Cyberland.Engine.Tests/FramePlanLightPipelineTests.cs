using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Verifies the sort → cull → clamp pipeline ordering: off-screen lights are removed
/// before the capacity cap is applied, so visible on-screen lights are never displaced.
/// </summary>
public sealed class FramePlanLightPipelineTests
{
    private static ShadowSdfCamera MakeCam(
        Vector2D<float> posWorld = default,
        float rotation = 0f,
        int vpW = 320,
        int vpH = 240)
    {
        return ShadowSdfCamera.SyntheticCamera(
            posWorld,
            rotation,
            new Vector2D<int>(vpW, vpH),
            new Vector2D<int>(vpW, vpH),
            sdfScale: 1f);
    }

    private static void StampSubmissionIndices(PointLight[] lights, int count)
    {
        for (var i = 0; i < count; i++)
            lights[i].SubmissionIndex = i;
    }

    private static void StampSubmissionIndices(SpotLight[] lights, int count)
    {
        for (var i = 0; i < count; i++)
            lights[i].SubmissionIndex = i;
    }

    private static void StampSubmissionIndices(DirectionalLight[] lights, int count)
    {
        for (var i = 0; i < count; i++)
            lights[i].SubmissionIndex = i;
    }

    /// <summary>
    /// Simulates the shadow-disable CastsShadow strip that <c>FramePlanBuilder.Build</c> applies when
    /// <see cref="ShadowSettings.Enabled"/> is false. The actual logic lives in VulkanRenderer (excluded
    /// from coverage); this helper mirrors it so pipeline tests can verify post-strip sort invariants.
    /// </summary>
    private static void StripAllCastsShadow(PointLight[] lights, int count)
    {
        for (var i = 0; i < count; i++)
            lights[i].CastsShadow = false;
    }

    private static void StripAllCastsShadow(SpotLight[] lights, int count)
    {
        for (var i = 0; i < count; i++)
            lights[i].CastsShadow = false;
    }

    private static void StripAllCastsShadow(DirectionalLight[] lights, int count)
    {
        for (var i = 0; i < count; i++)
            lights[i].CastsShadow = false;
    }

    [Fact]
    public void ShadowDisable_strips_CastsShadow_on_all_light_types()
    {
        var points = new PointLight[]
        {
            new() { PositionWorld = default, Radius = 10f, Intensity = 1f, CastsShadow = true },
            new() { PositionWorld = default, Radius = 10f, Intensity = 1f, CastsShadow = false },
        };
        var spots = new SpotLight[]
        {
            new() { PositionWorld = default, Radius = 10f, Intensity = 1f, CastsShadow = true },
        };
        var dirs = new DirectionalLight[]
        {
            new() { DirectionWorld = new Silk.NET.Maths.Vector2D<float>(0f, 1f), Intensity = 1f, CastsShadow = true },
            new() { DirectionWorld = new Silk.NET.Maths.Vector2D<float>(1f, 0f), Intensity = 0.5f, CastsShadow = true },
        };

        StripAllCastsShadow(points, points.Length);
        StripAllCastsShadow(spots, spots.Length);
        StripAllCastsShadow(dirs, dirs.Length);

        for (var i = 0; i < points.Length; i++)
            Assert.False(points[i].CastsShadow, $"Point[{i}] should have CastsShadow stripped");
        for (var i = 0; i < spots.Length; i++)
            Assert.False(spots[i].CastsShadow, $"Spot[{i}] should have CastsShadow stripped");
        for (var i = 0; i < dirs.Length; i++)
            Assert.False(dirs[i].CastsShadow, $"Dir[{i}] should have CastsShadow stripped");
    }

    [Fact]
    public void Sort_prioritizes_shadow_casting_lights_across_all_types()
    {
        // Point lights: shadow-casting should sort before non-shadow at equal weight.
        var points = new PointLight[]
        {
            new() { PositionWorld = new(0f, 0f), Radius = 10f, Intensity = 1f, CastsShadow = false },
            new() { PositionWorld = new(1f, 0f), Radius = 10f, Intensity = 1f, CastsShadow = true },
        };
        StampSubmissionIndices(points, 2);
        LightSubmissionOrdering.SortPointLights(points, 2);
        Assert.True(points[0].CastsShadow, "Shadow-casting point should sort first");

        // Spot lights.
        var spots = new SpotLight[]
        {
            new() { PositionWorld = new(0f, 0f), Radius = 10f, Intensity = 1f, CastsShadow = false },
            new() { PositionWorld = new(1f, 0f), Radius = 10f, Intensity = 1f, CastsShadow = true },
        };
        StampSubmissionIndices(spots, 2);
        LightSubmissionOrdering.SortSpotLights(spots, 2);
        Assert.True(spots[0].CastsShadow, "Shadow-casting spot should sort first");

        // Directional lights.
        var dirs = new DirectionalLight[]
        {
            new() { DirectionWorld = new(0f, 1f), Intensity = 1f, CastsShadow = false },
            new() { DirectionWorld = new(1f, 0f), Intensity = 1f, CastsShadow = true },
        };
        StampSubmissionIndices(dirs, 2);
        LightSubmissionOrdering.SortDirectionalLights(dirs, 2);
        Assert.True(dirs[0].CastsShadow, "Shadow-casting directional should sort first");
    }

    [Fact]
    public void SortCullClamp_spot_lights_keeps_visible_when_offscreen_exceed_cap()
    {
        const int onScreenCount = 260;
        const int offScreenCount = 30;
        const int totalCount = onScreenCount + offScreenCount;

        var cam = MakeCam();

        var lights = new SpotLight[totalCount];

        for (var i = 0; i < onScreenCount; i++)
        {
            lights[i] = new SpotLight
            {
                PositionWorld = new((i % 16) * 8f - 64f, (i / 16) * 8f - 64f),
                DirectionWorld = new(0f, -1f),
                Radius = 50f,
                InnerConeRadians = 0.3f,
                OuterConeRadians = 0.8f,
                Intensity = 1f,
                Color = new(0f, 1f, 0f),
            };
        }

        for (var i = 0; i < offScreenCount; i++)
        {
            lights[onScreenCount + i] = new SpotLight
            {
                PositionWorld = new(9000f + i, 9000f + i),
                DirectionWorld = new(0f, -1f),
                Radius = 5f,
                InnerConeRadians = 0.3f,
                OuterConeRadians = 0.8f,
                Intensity = 1f,
                Color = new(1f, 0f, 0f),
            };
        }

        StampSubmissionIndices(lights, totalCount);
        LightSubmissionOrdering.SortSpotLights(lights, totalCount);

        var afterCull = LightViewportCulling.CullSpotLights(lights, totalCount, in cam);
        Assert.Equal(onScreenCount, afterCull);

        var finalCount = SubmissionClamp.ClampWithDropCount(
            afterCull, DeferredRenderingConstants.MaxSpotLights, out var dropped);
        Assert.Equal(DeferredRenderingConstants.MaxSpotLights, finalCount);
        Assert.Equal(onScreenCount - DeferredRenderingConstants.MaxSpotLights, dropped);

        for (var i = 0; i < finalCount; i++)
        {
            Assert.Equal(1f, lights[i].Color.Y);
            Assert.Equal(0f, lights[i].Color.X);
        }
    }

    [Fact]
    public void SortClamp_directional_lights_preserves_brightest()
    {
        const int count = 20;
        var lights = new DirectionalLight[count];
        for (var i = 0; i < count; i++)
        {
            lights[i] = new DirectionalLight
            {
                DirectionWorld = new(MathF.Cos(i * 0.3f), MathF.Sin(i * 0.3f)),
                Intensity = count - i,
                Color = new(1f, 1f, 1f),
                CastsShadow = i < 5,
            };
        }

        StampSubmissionIndices(lights, count);
        LightSubmissionOrdering.SortDirectionalLights(lights, count);

        var finalCount = SubmissionClamp.ClampWithDropCount(
            count, DeferredRenderingConstants.MaxDirectionalLights, out var dropped);
        Assert.Equal(DeferredRenderingConstants.MaxDirectionalLights, finalCount);
        Assert.Equal(count - DeferredRenderingConstants.MaxDirectionalLights, dropped);

        // Shadow-casters should precede non-shadow-casters among the survivors.
        var firstNonShadow = -1;
        for (var i = 0; i < finalCount; i++)
        {
            if (!lights[i].CastsShadow && firstNonShadow < 0)
                firstNonShadow = i;
            if (firstNonShadow >= 0 && lights[i].CastsShadow)
                Assert.Fail("Shadow-caster found after non-shadow-caster — sort order violated");
        }
    }

    [Fact]
    public void SortCullClamp_keeps_visible_lights_when_offscreen_exceed_cap()
    {
        const int onScreenCount = 1030;
        const int offScreenCount = 50;
        const int totalCount = onScreenCount + offScreenCount;

        var cam = MakeCam();

        var lights = new PointLight[totalCount];

        // Place 1030 lights within the viewport (spread around center).
        for (var i = 0; i < onScreenCount; i++)
        {
            lights[i] = new PointLight
            {
                PositionWorld = new Vector2D<float>(
                    (i % 32) * 5f - 80f,
                    (i / 32) * 5f - 80f),
                Radius = 40f,
                Intensity = 1f,
                Color = new Vector3D<float>(0f, 1f, 0f),
            };
        }

        // Place 50 lights far off-screen.
        for (var i = 0; i < offScreenCount; i++)
        {
            lights[onScreenCount + i] = new PointLight
            {
                PositionWorld = new Vector2D<float>(9000f + i, 9000f + i),
                Radius = 5f,
                Intensity = 1f,
                Color = new Vector3D<float>(1f, 0f, 0f),
            };
        }

        // 1. Stamp + Sort (matches FramePlanBuilder.Build order).
        StampSubmissionIndices(lights, totalCount);
        LightSubmissionOrdering.SortPointLights(lights, totalCount);

        // 2. Cull off-screen lights (the corrected ordering).
        var afterCull = LightViewportCulling.CullPointLights(lights, totalCount, in cam);

        // All off-screen lights should be removed, leaving only on-screen.
        Assert.Equal(onScreenCount, afterCull);

        // 3. Clamp to max capacity.
        var finalCount = SubmissionClamp.ClampWithDropCount(
            afterCull, DeferredRenderingConstants.MaxPointLights, out var dropped);

        Assert.Equal(DeferredRenderingConstants.MaxPointLights, finalCount);
        Assert.Equal(onScreenCount - DeferredRenderingConstants.MaxPointLights, dropped);

        // All surviving lights must be on-screen (green channel = 1, red = 0).
        for (var i = 0; i < finalCount; i++)
        {
            Assert.Equal(1f, lights[i].Color.Y);
            Assert.Equal(0f, lights[i].Color.X);
        }
    }
}
