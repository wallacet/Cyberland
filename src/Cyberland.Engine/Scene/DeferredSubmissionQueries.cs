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
        AmbientLight best = default;
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
                    best = row.Light;
                    have = true;
                }
            }
        }

        if (have)
            r.SubmitAmbientLight(in best);
    }

    public static void SubmitBestDirectionalLight(World world, IRenderer r)
    {
        EntityId bestId = default;
        DirectionalLight best = default;
        var have = false;
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<DirectionalLightSource>()))
        {
            var ents = chunk.Entities;
            var col = chunk.Column<DirectionalLightSource>(0);
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var row = ref col[i];
                if (!row.Active)
                    continue;
                var e = ents[i];
                if (!have || e.Raw > bestId.Raw)
                {
                    bestId = e;
                    best = row.Light;
                    have = true;
                }
            }
        }

        if (have)
            r.SubmitDirectionalLight(in best);
    }

    public static void SubmitBestSpotLight(World world, IRenderer r)
    {
        EntityId bestId = default;
        SpotLight best = default;
        var have = false;
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<SpotLightSource>()))
        {
            var ents = chunk.Entities;
            var col = chunk.Column<SpotLightSource>(0);
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var row = ref col[i];
                if (!row.Active)
                    continue;
                var e = ents[i];
                if (!have || e.Raw > bestId.Raw)
                {
                    bestId = e;
                    best = row.Light;
                    have = true;
                }
            }
        }

        if (have)
            r.SubmitSpotLight(in best);
    }

    public static void CollectPointLightChunks(World world, List<MultiComponentChunkView> into)
    {
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<PointLightSource>()))
            into.Add(chunk);
    }

    public static void CollectPostProcessVolumeChunks(World world, List<MultiComponentChunkView> into)
    {
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<PostProcessVolumeSource>()))
            into.Add(chunk);
    }
}
