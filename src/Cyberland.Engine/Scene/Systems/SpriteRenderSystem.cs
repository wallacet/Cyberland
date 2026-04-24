using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Default engine sprite pass: walks <see cref="Sprite"/> chunks with <see cref="Transform"/> and
/// submits <see cref="SpriteDrawRequest"/>s. Mods normally attach components and let this system draw—no custom <see cref="IRenderer.SubmitSprite"/> calls.
/// </summary>
public sealed class SpriteRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Sprite, Transform>();

    /// <param name="host">Must expose a non-null <see cref="Hosting.GameHostServices.Renderer"/> after startup.</param>
    public SpriteRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer!;
        
        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref readonly var spr = ref chunk.Column<Sprite>()[i];
                if (!spr.Visible)
                    return;

                ref readonly var transform = ref chunk.Column<Transform>()[i];
                // Decompose the world matrix once per sprite: reading the PRS properties via a ref readonly transform
                // would decompose (via a defensive copy) on every property access.
                TransformMath.DecomposeToPRS(transform.WorldMatrix, out var worldPos, out var worldRad, out var worldScale);
                var hx = spr.HalfExtents.X * worldScale.X;
                var hy = spr.HalfExtents.Y * worldScale.Y;

                var req = new SpriteDrawRequest
                {
                    CenterWorld = worldPos,
                    HalfExtentsWorld = new Vector2D<float>(hx, hy),
                    RotationRadians = worldRad,
                    Layer = spr.Layer,
                    SortKey = spr.SortKey,
                    AlbedoTextureId = spr.AlbedoTextureId,
                    NormalTextureId = spr.NormalTextureId,
                    EmissiveTextureId = spr.EmissiveTextureId,
                    ColorMultiply = spr.ColorMultiply,
                    Alpha = spr.Alpha,
                    EmissiveTint = spr.EmissiveTint,
                    EmissiveIntensity = spr.EmissiveIntensity,
                    DepthHint = spr.DepthHint,
                    UvRect = spr.UvRect,
                    Transparent = spr.Transparent,
                    Space = spr.Space
                };

                r.SubmitSprite(in req);
            });
        }
    }
}
