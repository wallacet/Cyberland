using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: for each <see cref="Tilemap"/> entity, looks up grid indices in <see cref="Hosting.GameHostServices.Tilemaps"/> and issues textured quads per solid cell.
/// </summary>
public sealed class TilemapRenderSystem : IParallelSystem
{
    private readonly GameHostServices _host;

    /// <param name="host">Requires both <see cref="Hosting.GameHostServices.Renderer"/> and <see cref="Hosting.GameHostServices.Tilemaps"/>.</param>
    public TilemapRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        var store = _host.Tilemaps;
        if (r is null || store is null)
            return;

        var chunks = new List<ComponentChunkView<Tilemap>>();
        foreach (var chunk in world.QueryChunks<Tilemap>())
            chunks.Add(chunk);

        if (chunks.Count == 0)
            return;

        Parallel.ForEach(chunks, parallelOptions, chunk =>
        {
            var ents = chunk.Entities;
            var maps = chunk.Components;
            for (var i = 0; i < chunk.Count; i++)
            {
                var id = ents[i];
                ref readonly var tm = ref maps[i];
                if (!store.TryGet(id, out var mem, out var cols, out var rows))
                    continue;

                var span = mem.Span;
                var tw = tm.TileWidth;
                var th = tm.TileHeight;
                var ox = tm.OriginX;
                var oy = tm.OriginY;
                var tid = tm.AtlasAlbedoTextureId;
                var minI = tm.NonEmptyTileMinIndex;

                for (var y = 0; y < rows; y++)
                for (var x = 0; x < cols; x++)
                {
                    var ti = span[y * cols + x];
                    if (ti < minI)
                        continue;

                    var cx = ox + (x + 0.5f) * tw;
                    var cy = oy + (y + 0.5f) * th;
                    var req = new SpriteDrawRequest
                    {
                        CenterWorld = new Vector2D<float>(cx, cy),
                        HalfExtentsWorld = new Vector2D<float>(tw * 0.48f, th * 0.48f),
                        RotationRadians = 0f,
                        Layer = tm.Layer,
                        SortKey = tm.SortKey + x * 0.001f + y * 0.0001f,
                        AlbedoTextureId = tid,
                        NormalTextureId = r.DefaultNormalTextureId,
                        EmissiveTextureId = -1,
                        ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f),
                        Alpha = 1f,
                        EmissiveTint = default,
                        EmissiveIntensity = 0f,
                        DepthHint = tm.SortKey,
                        UvRect = default
                    };
                    r.SubmitSprite(in req);
                }
            }
        });
    }
}
