using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Fixed update: integrates the singleton player’s <see cref="Velocity"/> into <see cref="Transform.LocalPosition"/> and clamps
/// to the virtual canvas.
/// </summary>
/// <remarks>
/// Uses <see cref="ChunkQueryAllExtensions.GetFirst{T}"/> for the singleton player row (same <see cref="SystemQuerySpec"/> in
/// both phases)—see **cyberland-mod-patterns-hdr**. Do not cache component-store facades.
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
        ref var transform = ref archetype.GetFirst<Transform>();
        transform.LocalPosition = p;
    }

    /// <inheritdoc />
    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        ref var transform = ref archetype.GetFirst<Transform>();
        ref var vel = ref archetype.GetFirst<Velocity>();
        var pos = transform.LocalPosition;
        pos.X += vel.X * fixedDeltaSeconds;
        pos.Y += vel.Y * fixedDeltaSeconds;

        // Clamp using sprite half-extent so the quad’s AABB stays inside the virtual canvas.
        var h = Constants.SpriteHalfExtent;
        pos.X = Math.Clamp(pos.X, h, fb.X - h);
        pos.Y = Math.Clamp(pos.Y, h, fb.Y - h);
        transform.LocalPosition = pos;
    }
}
