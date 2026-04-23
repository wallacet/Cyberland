using System.Collections.Generic;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene;

/// <summary>
/// ECS world iteration for deferred lighting and post-process submission.
/// </summary>
/// <remarks>
/// Engine policy keeps <c>Scene/Systems</c> implementations from calling <see cref="World.QueryChunks"/> directly
/// so runtime systems stay aligned with scheduler-provided archetypes where possible; these multi-query submit paths are centralized here.
/// </remarks>
internal static class DeferredSubmissionQueries
{
    public static void SubmitBestAmbientLight(World world, IRenderer r)
    {
        EntityId bestId = default;
        AmbientLightSource best = default;
        var have = false;
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<AmbientLightSource>()))
        {
            var ents = chunk.Entities;
            var col = chunk.Column<AmbientLightSource>(0);
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var row = ref col[i];
                if (!row.Active)
                    continue;
                var e = ents[i];
                if (!have || e.Raw > bestId.Raw)
                {
                    bestId = e;
                    best = row;
                    have = true;
                }
            }
        }

        if (have)
        {
            var payload = new AmbientLight
            {
                Color = best.Color,
                Intensity = best.Intensity
            };
            r.SubmitAmbientLight(in payload);
        }
    }

    public static void SubmitBestDirectionalLight(World world, IRenderer r)
    {
        EntityId bestId = default;
        DirectionalLightSource best = default;
        Transform bestTransform = default;
        var have = false;
        var directionalSources = world.Components<DirectionalLightSource>();
        var transforms = world.Components<Transform>();
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<DirectionalLightSource, Transform>()))
        {
            var ents = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = ents[i];
                ref readonly var row = ref directionalSources.Get(entity);
                if (!row.Active)
                    continue;
                if (!have || entity.Raw > bestId.Raw)
                {
                    bestId = entity;
                    best = row;
                    bestTransform = transforms.Get(entity);
                    have = true;
                }
            }
        }

        if (have)
        {
            var dir = DirectionFromWorldRotation(bestTransform.WorldRotationRadians);
            var payload = new DirectionalLight
            {
                DirectionWorld = dir,
                Color = best.Color,
                Intensity = best.Intensity,
                CastsShadow = best.CastsShadow
            };
            r.SubmitDirectionalLight(in payload);
        }
    }

    public static void SubmitBestSpotLight(World world, IRenderer r)
    {
        EntityId bestId = default;
        SpotLightSource best = default;
        Transform bestTransform = default;
        var have = false;
        var spotSources = world.Components<SpotLightSource>();
        var transforms = world.Components<Transform>();
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<SpotLightSource, Transform>()))
        {
            var ents = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = ents[i];
                ref readonly var row = ref spotSources.Get(entity);
                if (!row.Active)
                    continue;
                if (!have || entity.Raw > bestId.Raw)
                {
                    bestId = entity;
                    best = row;
                    bestTransform = transforms.Get(entity);
                    have = true;
                }
            }
        }

        if (have)
        {
            var dir = DirectionFromWorldRotation(bestTransform.WorldRotationRadians);
            var radiusScale = MaxAbsScale(bestTransform.WorldScale);
            var payload = new SpotLight
            {
                PositionWorld = bestTransform.WorldPosition,
                DirectionWorld = dir,
                Radius = best.Radius * radiusScale,
                InnerConeRadians = best.InnerConeRadians,
                OuterConeRadians = best.OuterConeRadians,
                Color = best.Color,
                Intensity = best.Intensity,
                CastsShadow = best.CastsShadow
            };
            r.SubmitSpotLight(in payload);
        }
    }

    public static void CollectPointLightChunks(World world, List<MultiComponentChunkView> into)
    {
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<PointLightSource, Transform>()))
            into.Add(chunk);
    }

    public static void CollectPostProcessVolumeChunks(World world, List<MultiComponentChunkView> into)
    {
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<PostProcessVolumeSource>()))
            into.Add(chunk);
    }

    private static float MaxAbsScale(in Silk.NET.Maths.Vector2D<float> scale) =>
        MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y));

    private static Silk.NET.Maths.Vector2D<float> DirectionFromWorldRotation(float radians) =>
        new(MathF.Cos(radians), MathF.Sin(radians));
}
