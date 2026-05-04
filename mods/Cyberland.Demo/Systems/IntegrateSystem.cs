using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Fixed update integration for tagged player entities: applies <see cref="Velocity"/> to <see cref="Transform.LocalPosition"/>
/// and clamps within the virtual canvas so sprites stay on-screen after camera framing.
/// </summary>
/// <remarks>
/// <see cref="OnStart"/> seeds an initial spawn derived from <see cref="ModLayoutViewport.VirtualSizeForSimulation"/> so windowed
/// sessions still place the hero predictably. Physics stepping happens only in <see cref="OnFixedUpdate"/>.
/// </remarks>
public sealed class IntegrateSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform, Velocity>();

    private readonly GameHostServices _host;

    /// <summary>Uses host layout helpers so world-space placement stays aligned with the renderer’s virtual canvas.</summary>
    public IntegrateSystem(GameHostServices host)
    {
        _host = host;
    }

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = _host.RendererRequired;
        _ = archetype.RequireSingleEntityWith<PlayerTag>("player");

        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        // Slightly right of center—room to showcase HDR bloom shifting as you walk toward the neon side.
        var p = WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(fb.X * 0.55f, fb.Y / 2f), fb);
        foreach (var chunk in archetype)
        {
            var transforms = chunk.Column<Transform>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var transform = ref transforms[i];
                transform.LocalPosition = p;
            }
        }
    }

    /// <inheritdoc />
    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        foreach (var chunk in archetype)
        {
            var transforms = chunk.Column<Transform>();
            var vels = chunk.Column<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var transform = ref transforms[i];
                ref var vel = ref vels[i];
                var pos = transform.LocalPosition;
                pos.X += vel.X * fixedDeltaSeconds;
                pos.Y += vel.Y * fixedDeltaSeconds;

                // Clamp using sprite half-extent so the quad’s AABB never crosses the playfield border.
                var h = Constants.SpriteHalfExtent;
                pos.X = Math.Clamp(pos.X, h, fb.X - h);
                pos.Y = Math.Clamp(pos.Y, h, fb.Y - h);
                transform.LocalPosition = pos;
            }
        }
    }
}
