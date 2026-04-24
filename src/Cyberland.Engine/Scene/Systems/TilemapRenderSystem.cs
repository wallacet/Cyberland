using System.Numerics;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: for each <see cref="Tilemap"/> entity with <see cref="Transform"/>, looks up grid indices in
/// <see cref="Hosting.GameHostServices.Tilemaps"/> and issues textured quads per solid cell. Cell positions are built in
/// local grid space (+Y down for increasing row index) and mapped through the resolved world matrix.
/// </summary>
public sealed class TilemapRenderSystem : IParallelSystem, IParallelLateUpdate
{
    private static readonly Vector4D<float> OpaqueWhite = new(1f, 1f, 1f, 1f);

    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Tilemap, Transform>();

    /// <param name="host">Requires both <see cref="Hosting.GameHostServices.Renderer"/> and <see cref="Hosting.GameHostServices.Tilemaps"/>.</param>
    public TilemapRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer!;
        var store = _host.Tilemaps;
        if (store is null)
            return;

        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                var id = chunk.Entities[i];
                ref readonly var tm = ref chunk.Column<Tilemap>()[i];
                ref readonly var transform = ref chunk.Column<Transform>()[i];
                if (!store.TryGet(id, out var mem, out var cols, out var rows))
                    return;

                var span = mem.Span;
                var tw = tm.TileWidth;
                var th = tm.TileHeight;
                var tid = tm.AtlasAlbedoTextureId;
                var minI = tm.NonEmptyTileMinIndex;
                var defaultNormal = r.DefaultNormalTextureId;

                // WorldMatrix is already the composed TRS; avoid the old decompose-then-rebuild round trip and pull
                // scale / rotation for per-tile extents and orientation from one decomposition.
                var worldM = transform.WorldMatrix;
                TransformMath.DecomposeToPRS(worldM, out _, out var worldRad, out var worldScale);

                for (var y = 0; y < rows; y++)
                for (var x = 0; x < cols; x++)
                {
                    var ti = span[y * cols + x];
                    if (ti < minI)
                        continue;

                    var local = new Vector2((x + 0.5f) * tw, -((y + 0.5f) * th));
                    var centerWorldV = Vector2.Transform(local, worldM);
                    var centerWorld = new Vector2D<float>(centerWorldV.X, centerWorldV.Y);

                    var hx = tw * 0.48f * worldScale.X;
                    var hy = th * 0.48f * worldScale.Y;
                    var req = new SpriteDrawRequest
                    {
                        CenterWorld = centerWorld,
                        HalfExtentsWorld = new Vector2D<float>(hx, hy),
                        RotationRadians = worldRad,
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
            });
        }
    }
}
