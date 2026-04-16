using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: for each <see cref="Tilemap"/> entity, looks up grid indices in <see cref="Hosting.GameHostServices.Tilemaps"/> and issues textured quads per solid cell.
/// Cell centers are converted to world space (<see cref="WorldScreenSpace.ScreenPixelToWorldCenter"/>) before <see cref="IRenderer.SubmitSprite"/>.
/// </summary>
public sealed class TilemapRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private static readonly Vector4D<float> OpaqueWhite = new(1f, 1f, 1f, 1f);

    private readonly List<MultiComponentChunkView> _chunks = new();
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Tilemap>();

    /// <param name="host">Requires both <see cref="Hosting.GameHostServices.Renderer"/> and <see cref="Hosting.GameHostServices.Tilemaps"/>.</param>
    public TilemapRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelLateUpdate(World world, ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        var store = _host.Tilemaps;
        if (r is null || store is null)
            return;

        var fb = r.SwapchainPixelSize;

        _chunks.Clear();
        foreach (var chunk in query)
            _chunks.Add(chunk);

        if (_chunks.Count == 0)
            return;

        Parallel.For(0, _chunks.Count, parallelOptions, idx =>
        {
            var chunk = _chunks[idx];
            var ents = chunk.Entities;
            var maps = chunk.Column<Tilemap>(0);
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
                var defaultNormal = r.DefaultNormalTextureId;

                for (var y = 0; y < rows; y++)
                for (var x = 0; x < cols; x++)
                {
                    var ti = span[y * cols + x];
                    if (ti < minI)
                        continue;

                    var cx = ox + (x + 0.5f) * tw;
                    var cy = oy + (y + 0.5f) * th;
                    var centerWorld = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(cx, cy), fb);
                    var req = new SpriteDrawRequest
                    {
                        CenterWorld = centerWorld,
                        HalfExtentsWorld = new Vector2D<float>(tw * 0.48f, th * 0.48f),
                        RotationRadians = 0f,
                        Layer = tm.Layer,
                        SortKey = tm.SortKey + x * 0.001f + y * 0.0001f,
                        AlbedoTextureId = tid,
                        NormalTextureId = defaultNormal,
                        EmissiveTextureId = TextureId.MaxValue,
                        ColorMultiply = OpaqueWhite,
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
