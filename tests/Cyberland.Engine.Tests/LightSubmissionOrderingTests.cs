using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class LightSubmissionOrderingTests
{
    [Fact]
    public void SortPointLights_orders_shadow_first_then_descending_visual_weight()
    {
        var lights = new[]
        {
            new PointLight { Radius = 2f, Intensity = 1f, SubmissionIndex = 0 },
            new PointLight { Radius = 8f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 1 },
            new PointLight { Radius = 4f, Intensity = 2f, SubmissionIndex = 2 },
        };

        LightSubmissionOrdering.SortPointLights(lights, lights.Length);

        Assert.True(lights[0].CastsShadow);
        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(2, lights[1].SubmissionIndex);
        Assert.Equal(0, lights[2].SubmissionIndex);
    }

    [Fact]
    public void SortPointLights_descending_weight_decides_when_shadow_matches()
    {
        var lights = new[]
        {
            new PointLight { Radius = 3f, Intensity = 0.8f, SubmissionIndex = 0 },
            new PointLight { Radius = 3f, Intensity = 1.2f, SubmissionIndex = 1 },
        };

        LightSubmissionOrdering.SortPointLights(lights, lights.Length);

        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(0, lights[1].SubmissionIndex);
    }

    [Fact]
    public void SortPointLights_uses_submission_index_tiebreaker()
    {
        var lights = new[]
        {
            new PointLight { Radius = 5f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 10 },
            new PointLight { Radius = 5f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 3 }
        };

        LightSubmissionOrdering.SortPointLights(lights, lights.Length);

        Assert.Equal(3, lights[0].SubmissionIndex);
        Assert.Equal(10, lights[1].SubmissionIndex);
    }

    [Fact]
    public void SortPointLights_handles_small_counts()
    {
        var lights = new[]
        {
            new PointLight { Radius = 5f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 10 },
            new PointLight { Radius = 5f, Intensity = 1f, CastsShadow = false, SubmissionIndex = 1 },
            new PointLight { Radius = 5f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 3 },
        };

        LightSubmissionOrdering.SortPointLights(lights, 1);
        Assert.Equal(10, lights[0].SubmissionIndex);

        LightSubmissionOrdering.SortPointLights(lights, lights.Length);

        Assert.True(lights[0].CastsShadow);
        Assert.Equal(3, lights[0].SubmissionIndex);
        Assert.True(lights[1].CastsShadow);
        Assert.Equal(10, lights[1].SubmissionIndex);
        Assert.False(lights[2].CastsShadow);
    }

    [Fact]
    public void BrightestPointLights_survive_overflow_after_clamp()
    {
        const int total = 1100;
        const int cap = DeferredRenderingConstants.MaxPointLights;
        var lights = new PointLight[total];
        for (int i = 0; i < total; i++)
        {
            lights[i] = new PointLight
            {
                Radius = 1f + i * 0.01f,
                Intensity = 1f + i * 0.1f,
                SubmissionIndex = i,
            };
        }

        LightSubmissionOrdering.SortPointLights(lights, total);

        float minSurvivorWeight = lights[cap - 1].Intensity * lights[cap - 1].Radius * lights[cap - 1].Radius;
        for (int i = cap; i < total; i++)
        {
            float w = lights[i].Intensity * lights[i].Radius * lights[i].Radius;
            Assert.True(w <= minSurvivorWeight,
                $"Dropped light at [{i}] (weight {w}) should not exceed survivor minimum ({minSurvivorWeight}).");
        }
    }

    [Fact]
    public void SortSpotLights_orders_shadow_first_then_descending_visual_weight()
    {
        var lights = new[]
        {
            new SpotLight { Radius = 3f, Intensity = 1f, SubmissionIndex = 0 },
            new SpotLight { Radius = 5f, Intensity = 2f, CastsShadow = true, SubmissionIndex = 1 },
            new SpotLight { Radius = 5f, Intensity = 1f, SubmissionIndex = 2 },
        };

        LightSubmissionOrdering.SortSpotLights(lights, lights.Length);

        Assert.True(lights[0].CastsShadow);
        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(2, lights[1].SubmissionIndex);
        Assert.Equal(0, lights[2].SubmissionIndex);
    }

    [Fact]
    public void SortSpotLights_descending_weight_decides_when_shadow_matches()
    {
        var lights = new[]
        {
            new SpotLight { Radius = 5f, Intensity = 1.0f, SubmissionIndex = 0 },
            new SpotLight { Radius = 5f, Intensity = 1.1f, SubmissionIndex = 1 },
        };

        LightSubmissionOrdering.SortSpotLights(lights, lights.Length);

        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(0, lights[1].SubmissionIndex);
    }

    [Fact]
    public void SortSpotLights_uses_position_X_tiebreaker()
    {
        var lights = new[]
        {
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = true, PositionWorld = new Vector2D<float>(10f, 0f), SubmissionIndex = 0 },
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = true, PositionWorld = new Vector2D<float>(5f, 0f), SubmissionIndex = 1 },
        };

        LightSubmissionOrdering.SortSpotLights(lights, lights.Length);

        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(0, lights[1].SubmissionIndex);
    }

    [Fact]
    public void SortSpotLights_uses_position_Y_tiebreaker()
    {
        var lights = new[]
        {
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = true, PositionWorld = new Vector2D<float>(5f, 10f), SubmissionIndex = 0 },
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = true, PositionWorld = new Vector2D<float>(5f, 3f), SubmissionIndex = 1 },
        };

        LightSubmissionOrdering.SortSpotLights(lights, lights.Length);

        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(0, lights[1].SubmissionIndex);
    }

    [Fact]
    public void SortSpotLights_uses_submission_index_tiebreaker()
    {
        var lights = new[]
        {
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 7 },
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 2 }
        };

        LightSubmissionOrdering.SortSpotLights(lights, lights.Length);

        Assert.Equal(2, lights[0].SubmissionIndex);
        Assert.Equal(7, lights[1].SubmissionIndex);
    }

    [Fact]
    public void SortSpotLights_handles_small_counts()
    {
        var lights = new[]
        {
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 7 },
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = false, SubmissionIndex = 1 },
            new SpotLight { Radius = 5f, Intensity = 1f, CastsShadow = true, SubmissionIndex = 3 },
        };

        LightSubmissionOrdering.SortSpotLights(lights, 1);
        Assert.Equal(7, lights[0].SubmissionIndex);

        LightSubmissionOrdering.SortSpotLights(lights, lights.Length);

        Assert.True(lights[0].CastsShadow);
        Assert.Equal(3, lights[0].SubmissionIndex);
        Assert.True(lights[1].CastsShadow);
        Assert.Equal(7, lights[1].SubmissionIndex);
        Assert.False(lights[2].CastsShadow);
    }

    [Fact]
    public void BrightestSpotLights_survive_overflow_after_clamp()
    {
        const int total = 300;
        const int cap = DeferredRenderingConstants.MaxSpotLights;
        var lights = new SpotLight[total];
        for (int i = 0; i < total; i++)
        {
            lights[i] = new SpotLight
            {
                Radius = 1f + i * 0.01f,
                Intensity = 1f + i * 0.1f,
                SubmissionIndex = i,
            };
        }

        LightSubmissionOrdering.SortSpotLights(lights, total);

        float minSurvivorWeight = lights[cap - 1].Intensity * lights[cap - 1].Radius * lights[cap - 1].Radius;
        for (int i = cap; i < total; i++)
        {
            float w = lights[i].Intensity * lights[i].Radius * lights[i].Radius;
            Assert.True(w <= minSurvivorWeight,
                $"Dropped light at [{i}] (weight {w}) should not exceed survivor minimum ({minSurvivorWeight}).");
        }
    }

    [Fact]
    public void SortDirectionalLights_orders_shadow_first_then_descending_intensity()
    {
        var lights = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 0.2f },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0f, 1f), Intensity = 1f, CastsShadow = true },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0f, 1f), Intensity = 0.1f }
        };

        LightSubmissionOrdering.SortDirectionalLights(lights, lights.Length);

        Assert.True(lights[0].CastsShadow);
        Assert.Equal(1f, lights[0].Intensity);
        Assert.Equal(0.2f, lights[1].Intensity);
        Assert.Equal(0.1f, lights[2].Intensity);
    }

    [Fact]
    public void SortDirectionalLights_direction_tiebreak()
    {
        var lights = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0f, 1f), Intensity = 1f },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0f, -1f), Intensity = 1f },
        };

        LightSubmissionOrdering.SortDirectionalLights(lights, lights.Length);

        Assert.Equal(-1f, lights[0].DirectionWorld.Y);
        Assert.Equal(1f, lights[1].DirectionWorld.Y);
    }

    [Fact]
    public void SortDirectionalLights_color_tiebreak()
    {
        var lights = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.3f, 0.2f) },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.2f) },
        };

        LightSubmissionOrdering.SortDirectionalLights(lights, lights.Length);

        Assert.Equal(0.2f, lights[0].Color.Y, 3);
        Assert.Equal(0.3f, lights[1].Color.Y, 3);
    }

    [Fact]
    public void SortDirectionalLights_exercises_full_chain_and_small_counts()
    {
        var lights = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = true },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 0.8f, Color = new Vector3D<float>(0.1f, 0.2f, 0.35f), CastsShadow = false },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0.5f, 0.5f), Intensity = 0.5f, Color = new Vector3D<float>(0.1f, 0.2f, 0.25f), CastsShadow = false }
        };

        LightSubmissionOrdering.SortDirectionalLights(lights, 1);
        LightSubmissionOrdering.SortDirectionalLights(lights, lights.Length);

        Assert.True(lights[0].CastsShadow);
        Assert.Equal(1f, lights[0].Intensity);
        Assert.Equal(0.8f, lights[1].Intensity);
        Assert.Equal(0.5f, lights[2].Intensity);
    }

    [Fact]
    public void SortDirectionalLights_covers_shadow_and_direction_tiebreaks()
    {
        var byShadow = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = true },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = false }
        };
        LightSubmissionOrdering.SortDirectionalLights(byShadow, byShadow.Length);
        Assert.True(byShadow[0].CastsShadow);

        var byDir = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = false },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0.5f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = false }
        };
        LightSubmissionOrdering.SortDirectionalLights(byDir, byDir.Length);
        Assert.Equal(0.5f, byDir[0].DirectionWorld.X, 3);
    }

    [Fact]
    public void SortDirectionalLights_shadow_casters_survive_overflow()
    {
        const int total = 20;
        const int cap = DeferredRenderingConstants.MaxDirectionalLights;
        var lights = new DirectionalLight[total];
        for (int i = 0; i < total; i++)
        {
            lights[i] = new DirectionalLight
            {
                DirectionWorld = new Vector2D<float>(0.1f * i, 0.5f),
                Intensity = 0.5f + i * 0.1f,
                SubmissionIndex = i,
            };
        }
        lights[0].CastsShadow = true;
        lights[0].Intensity = 0.1f;

        LightSubmissionOrdering.SortDirectionalLights(lights, total);

        // Shadow caster must be in the first `cap` entries (survives clamp).
        var shadowInSurvivors = false;
        for (int i = 0; i < cap; i++)
        {
            if (lights[i].CastsShadow) { shadowInSurvivors = true; break; }
        }
        Assert.True(shadowInSurvivors, "Shadow-casting directional must survive overflow.");
    }

    [Fact]
    public void SortDirectionalLights_uses_submission_index_tiebreaker()
    {
        var lights = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = true, SubmissionIndex = 9 },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = true, SubmissionIndex = 1 }
        };

        LightSubmissionOrdering.SortDirectionalLights(lights, lights.Length);

        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(9, lights[1].SubmissionIndex);
    }

    [Fact]
    public void SortAmbientLights_orders_descending_intensity_then_color_then_index()
    {
        var lights = new[]
        {
            new AmbientLight { Intensity = 0.5f, Color = new Vector3D<float>(1f, 1f, 1f), SubmissionIndex = 0 },
            new AmbientLight { Intensity = 1.0f, Color = new Vector3D<float>(1f, 1f, 1f), SubmissionIndex = 1 },
            new AmbientLight { Intensity = 0.8f, Color = new Vector3D<float>(0.5f, 0.5f, 0.5f), SubmissionIndex = 2 },
        };

        LightSubmissionOrdering.SortAmbientLights(lights, lights.Length);

        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(2, lights[1].SubmissionIndex);
        Assert.Equal(0, lights[2].SubmissionIndex);
    }

    [Fact]
    public void SortAmbientLights_uses_submission_index_tiebreaker()
    {
        var lights = new[]
        {
            new AmbientLight { Intensity = 1f, Color = new Vector3D<float>(1f, 1f, 1f), SubmissionIndex = 7 },
            new AmbientLight { Intensity = 1f, Color = new Vector3D<float>(1f, 1f, 1f), SubmissionIndex = 2 },
        };

        LightSubmissionOrdering.SortAmbientLights(lights, lights.Length);

        Assert.Equal(2, lights[0].SubmissionIndex);
        Assert.Equal(7, lights[1].SubmissionIndex);
    }

    [Fact]
    public void SortAmbientLights_handles_single_element()
    {
        var lights = new[]
        {
            new AmbientLight { Intensity = 1f, Color = new Vector3D<float>(1f, 0f, 0f), SubmissionIndex = 0 },
            new AmbientLight { Intensity = 2f, Color = new Vector3D<float>(0f, 1f, 0f), SubmissionIndex = 1 },
        };

        LightSubmissionOrdering.SortAmbientLights(lights, 1);
        Assert.Equal(0, lights[0].SubmissionIndex);
    }

    [Fact]
    public void SortAmbientLights_deterministic_survivors_after_clamp()
    {
        const int total = 40;
        var lights = new AmbientLight[total];
        for (var i = 0; i < total; i++)
        {
            lights[i] = new AmbientLight
            {
                Intensity = 0.1f + i * 0.05f,
                Color = new Vector3D<float>(0.1f * (i % 3), 0.2f * (i % 5), 0.3f),
                SubmissionIndex = i,
            };
        }

        LightSubmissionOrdering.SortAmbientLights(lights, total);

        // After sorting, brightest (highest intensity) should be first.
        for (var i = 0; i < total - 1; i++)
        {
            Assert.True(lights[i].Intensity >= lights[i + 1].Intensity,
                $"Ambient [{i}] intensity {lights[i].Intensity} should be >= [{i + 1}] intensity {lights[i + 1].Intensity}");
        }

        // Clamping at MaxAmbientLights keeps the brightest.
        var cap = System.Math.Min(DeferredRenderingConstants.MaxAmbientLights, total);
        var minSurvivorIntensity = lights[cap - 1].Intensity;
        for (var i = cap; i < total; i++)
        {
            Assert.True(lights[i].Intensity <= minSurvivorIntensity,
                $"Dropped ambient [{i}] intensity {lights[i].Intensity} should not exceed survivor min {minSurvivorIntensity}");
        }
    }

    [Fact]
    public void SortAmbientLights_color_tiebreak()
    {
        var lights = new[]
        {
            new AmbientLight { Intensity = 1f, Color = new Vector3D<float>(0.5f, 0.2f, 0.3f), SubmissionIndex = 0 },
            new AmbientLight { Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), SubmissionIndex = 1 },
        };

        LightSubmissionOrdering.SortAmbientLights(lights, lights.Length);

        Assert.Equal(1, lights[0].SubmissionIndex);
        Assert.Equal(0, lights[1].SubmissionIndex);
    }
}
