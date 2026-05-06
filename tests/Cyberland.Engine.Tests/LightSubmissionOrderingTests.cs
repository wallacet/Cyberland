using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class LightSubmissionOrderingTests
{
    [Fact]
    public void SortPointLights_orders_by_position_then_radius_then_intensity()
    {
        var lights = new[]
        {
            new PointLight { PositionWorld = new Vector2D<float>(5f, 3f), Radius = 4f, Intensity = 2f, Color = new Vector3D<float>(1f, 0f, 0f) },
            new PointLight { PositionWorld = new Vector2D<float>(1f, 3f), Radius = 8f, Intensity = 1f, Color = new Vector3D<float>(0f, 1f, 0f) },
            new PointLight { PositionWorld = new Vector2D<float>(1f, 3f), Radius = 2f, Intensity = 1f, Color = new Vector3D<float>(0f, 0f, 1f) }
        };

        LightSubmissionOrdering.SortPointLights(lights, lights.Length);

        Assert.Equal(1f, lights[0].PositionWorld.X);
        Assert.Equal(2f, lights[0].Radius);
        Assert.Equal(8f, lights[1].Radius);
        Assert.Equal(5f, lights[2].PositionWorld.X);
    }

    [Fact]
    public void SortSpotLights_orders_by_position_then_direction()
    {
        var lights = new[]
        {
            new SpotLight { PositionWorld = new Vector2D<float>(2f, 2f), DirectionWorld = new Vector2D<float>(1f, 0f), Radius = 8f },
            new SpotLight { PositionWorld = new Vector2D<float>(1f, 2f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 8f },
            new SpotLight { PositionWorld = new Vector2D<float>(1f, 2f), DirectionWorld = new Vector2D<float>(0f, -1f), Radius = 8f }
        };

        LightSubmissionOrdering.SortSpotLights(lights, lights.Length);

        Assert.Equal(-1f, lights[0].DirectionWorld.Y);
        Assert.Equal(1f, lights[1].DirectionWorld.Y);
        Assert.Equal(2f, lights[2].PositionWorld.X);
    }

    [Fact]
    public void SortDirectionalLights_orders_by_direction_then_intensity()
    {
        var lights = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 0.2f },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0f, 1f), Intensity = 1f },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0f, 1f), Intensity = 0.1f }
        };

        LightSubmissionOrdering.SortDirectionalLights(lights, lights.Length);

        Assert.Equal(0f, lights[0].DirectionWorld.X);
        Assert.Equal(0.1f, lights[0].Intensity);
        Assert.Equal(1f, lights[1].Intensity);
        Assert.Equal(1f, lights[2].DirectionWorld.X);
    }

    [Fact]
    public void SortPointLights_uses_all_tie_breakers_and_handles_small_counts()
    {
        var lights = new[]
        {
            new PointLight
            {
                PositionWorld = new Vector2D<float>(2f, 2f), Radius = 5f, Intensity = 1f,
                FalloffExponent = 2f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = true
            },
            new PointLight
            {
                PositionWorld = new Vector2D<float>(2f, 2f), Radius = 5f, Intensity = 1f,
                FalloffExponent = 2f, Color = new Vector3D<float>(0.2f, 0.2f, 0.3f), CastsShadow = false
            },
            new PointLight
            {
                PositionWorld = new Vector2D<float>(2f, 2f), Radius = 5f, Intensity = 1f,
                FalloffExponent = 1.5f, Color = new Vector3D<float>(0.2f, 0.2f, 0.3f), CastsShadow = false
            }
        };

        LightSubmissionOrdering.SortPointLights(lights, 1);
        LightSubmissionOrdering.SortPointLights(lights, lights.Length);

        Assert.Equal(1.5f, lights[0].FalloffExponent);
        Assert.Equal(0.2f, lights[1].Color.Z);
        Assert.True(lights[2].Color.Z > lights[1].Color.Z);
    }

    [Fact]
    public void SortSpot_and_directional_lights_cover_full_tie_break_chain_and_small_counts()
    {
        var spots = new[]
        {
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f),
                DirectionWorld = new Vector2D<float>(0f, 1f),
                Radius = 5f,
                InnerConeRadians = 0.2f,
                OuterConeRadians = 0.8f,
                Intensity = 1f,
                Color = new Vector3D<float>(0.2f, 0.2f, 0.2f),
                CastsShadow = true
            },
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f),
                DirectionWorld = new Vector2D<float>(0f, 1f),
                Radius = 5f,
                InnerConeRadians = 0.2f,
                OuterConeRadians = 0.8f,
                Intensity = 1f,
                Color = new Vector3D<float>(0.2f, 0.2f, 0.25f),
                CastsShadow = false
            },
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f),
                DirectionWorld = new Vector2D<float>(0f, 0.8f),
                Radius = 4f,
                InnerConeRadians = 0.1f,
                OuterConeRadians = 0.7f,
                Intensity = 0.9f,
                Color = new Vector3D<float>(0.15f, 0.2f, 0.2f),
                CastsShadow = false
            }
        };
        LightSubmissionOrdering.SortSpotLights(spots, 1);
        LightSubmissionOrdering.SortSpotLights(spots, spots.Length);
        Assert.Equal(0.8f, spots[0].DirectionWorld.Y, 3);
        Assert.True(spots[1].CastsShadow);
        Assert.False(spots[2].CastsShadow);

        var directional = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = true },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.35f), CastsShadow = false },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(0.5f, 0.5f), Intensity = 0.8f, Color = new Vector3D<float>(0.1f, 0.2f, 0.25f), CastsShadow = false }
        };
        LightSubmissionOrdering.SortDirectionalLights(directional, 1);
        LightSubmissionOrdering.SortDirectionalLights(directional, directional.Length);
        Assert.Equal(0.5f, directional[0].DirectionWorld.X, 3);
        Assert.Equal(0.3f, directional[1].Color.Z, 3);
        Assert.Equal(0.35f, directional[2].Color.Z, 3);
    }

    [Fact]
    public void Sorters_reach_late_comparer_fields_when_earlier_keys_match()
    {
        var pointByFalloff = new[]
        {
            new PointLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f), Radius = 3f, Intensity = 0.9f, FalloffExponent = 2.2f,
                Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            },
            new PointLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f), Radius = 3f, Intensity = 0.9f, FalloffExponent = 1.1f,
                Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = true
            }
        };
        LightSubmissionOrdering.SortPointLights(pointByFalloff, pointByFalloff.Length);
        Assert.Equal(1.1f, pointByFalloff[0].FalloffExponent);

        var pointByIntensity = new[]
        {
            new PointLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f), Radius = 3f, Intensity = 1.2f, FalloffExponent = 1.1f,
                Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            },
            new PointLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f), Radius = 3f, Intensity = 0.8f, FalloffExponent = 1.1f,
                Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            }
        };
        LightSubmissionOrdering.SortPointLights(pointByIntensity, pointByIntensity.Length);
        Assert.Equal(0.8f, pointByIntensity[0].Intensity, 3);

        var pointByShadow = new[]
        {
            new PointLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f), Radius = 3f, Intensity = 0.9f, FalloffExponent = 1.1f,
                Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = true
            },
            new PointLight
            {
                PositionWorld = new Vector2D<float>(1f, 1f), Radius = 3f, Intensity = 0.9f, FalloffExponent = 1.1f,
                Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            }
        };
        LightSubmissionOrdering.SortPointLights(pointByShadow, pointByShadow.Length);
        Assert.False(pointByShadow[0].CastsShadow);

        var spotByInner = new[]
        {
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.3f, OuterConeRadians = 0.8f, Intensity = 1f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            },
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            }
        };
        LightSubmissionOrdering.SortSpotLights(spotByInner, spotByInner.Length);
        Assert.Equal(0.2f, spotByInner[0].InnerConeRadians, 3);

        var spotByRadius = new[]
        {
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 6f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            },
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            }
        };
        LightSubmissionOrdering.SortSpotLights(spotByRadius, spotByRadius.Length);
        Assert.Equal(5f, spotByRadius[0].Radius, 3);

        var spotByOuter = new[]
        {
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.9f, Intensity = 1f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            },
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            }
        };
        LightSubmissionOrdering.SortSpotLights(spotByOuter, spotByOuter.Length);
        Assert.Equal(0.8f, spotByOuter[0].OuterConeRadians, 3);

        var spotByIntensity = new[]
        {
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1.1f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            },
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1.0f, Color = new Vector3D<float>(0.2f, 0.2f, 0.2f), CastsShadow = false
            }
        };
        LightSubmissionOrdering.SortSpotLights(spotByIntensity, spotByIntensity.Length);
        Assert.Equal(1.0f, spotByIntensity[0].Intensity, 3);

        var spotByColorAndShadow = new[]
        {
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1.0f, Color = new Vector3D<float>(0.2f, 0.25f, 0.2f), CastsShadow = true
            },
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1.0f, Color = new Vector3D<float>(0.2f, 0.2f, 0.3f), CastsShadow = false
            },
            new SpotLight
            {
                PositionWorld = new Vector2D<float>(0f, 0f), DirectionWorld = new Vector2D<float>(0f, 1f), Radius = 5f,
                InnerConeRadians = 0.2f, OuterConeRadians = 0.8f, Intensity = 1.0f, Color = new Vector3D<float>(0.2f, 0.2f, 0.3f), CastsShadow = true
            }
        };
        LightSubmissionOrdering.SortSpotLights(spotByColorAndShadow, spotByColorAndShadow.Length);
        Assert.Equal(0.2f, spotByColorAndShadow[0].Color.Y, 3);
        Assert.False(spotByColorAndShadow[0].CastsShadow);

        var directionalByShadow = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = true },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = false }
        };
        LightSubmissionOrdering.SortDirectionalLights(directionalByShadow, directionalByShadow.Length);
        Assert.False(directionalByShadow[0].CastsShadow);

        var directionalByColorX = new[]
        {
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.3f, 0.2f, 0.3f), CastsShadow = false },
            new DirectionalLight { DirectionWorld = new Vector2D<float>(1f, 0f), Intensity = 1f, Color = new Vector3D<float>(0.1f, 0.2f, 0.3f), CastsShadow = false }
        };
        LightSubmissionOrdering.SortDirectionalLights(directionalByColorX, directionalByColorX.Length);
        Assert.Equal(0.1f, directionalByColorX[0].Color.X, 3);
    }
}
