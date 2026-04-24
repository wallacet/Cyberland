using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Places the player once in <see cref="OnStart"/> from the initial framebuffer size, then integrates <see cref="Transform"/>
/// from <see cref="Velocity"/> and clamps to the playfield each fixed tick. Sequential fixed update (single player entity).
/// </summary>
public sealed class IntegrateSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag>();

    private readonly GameHostServices _host;
    private World _world;
    private EntityId _player;

    /// <summary>False until <see cref="OnStart"/> places the player.</summary>
    private bool _initialized;

    public IntegrateSystem(GameHostServices host)
    {
        _host = host;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        var r = _host.Renderer
                ?? throw new InvalidOperationException("cyberland.demo/integrate requires Host.Renderer during OnStart.");
        _player = archetype.RequireSingleEntityWith<PlayerTag>("player");

        ref var transform = ref world.Components<Transform>().Get(_player);
        var fb = r.SwapchainPixelSize;
        var p = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(fb.X * 0.55f, fb.Y / 2f), fb);
        transform.LocalPosition = p;
        transform.WorldPosition = p;
        _initialized = true;
    }

    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        if (!_initialized)
            return;

        var world = _world;
        var r = _host.Renderer!;
        var fb = r.SwapchainPixelSize;
        ref var transform = ref world.Components<Transform>().Get(_player);
        ref var vel = ref world.Components<Velocity>().Get(_player);

        var pos = transform.LocalPosition;
        pos.X += vel.X * fixedDeltaSeconds;
        pos.Y += vel.Y * fixedDeltaSeconds;

        var h = Constants.SpriteHalfExtent;
        pos.X = Math.Clamp(pos.X, h, fb.X - h);
        pos.Y = Math.Clamp(pos.Y, h, fb.Y - h);
        transform.LocalPosition = pos;
        transform.WorldPosition = pos;
    }
}
