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
/// Registered as <see cref="ISingletonSystem"/> so phase hooks receive <see cref="SingletonEntity"/> for the player row—no
/// <see cref="ChunkQueryAll"/> iteration for a single entity (see **cyberland-mod-patterns-hdr**).
/// </remarks>
public sealed class IntegrateSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform, Velocity>();

    private readonly GameHostServices _host;

    /// <summary>Uses host layout helpers so world-space placement stays aligned with the renderer’s virtual canvas.</summary>
    public IntegrateSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity player)
    {
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        // Slightly right of center—room to showcase HDR bloom shifting as you walk toward the neon side.
        var p = WorldViewportSpace.ViewportPixelToWorldCenter(new Vector2D<float>(fb.X * 0.55f, fb.Y / 2f), fb);
        ref var transform = ref player.Get<Transform>();
        transform.LocalPosition = p;
    }

    /// <inheritdoc />
    public void OnSingletonFixedUpdate(in SingletonEntity player, float fixedDeltaSeconds)
    {
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        ref var transform = ref player.Get<Transform>();
        ref var vel = ref player.Get<Velocity>();
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
