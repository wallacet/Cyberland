using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>Places the player once in <see cref="OnStart"/>, then integrates <see cref="Position"/> from <see cref="Velocity"/> and clamps to the playfield.</summary>
public sealed class DemoIntegrateSystem : ISystem, IFixedUpdate
{
    private readonly GameHostServices _host;
    private readonly EntityId _player;

    public DemoIntegrateSystem(GameHostServices host, EntityId player)
    {
        _host = host;
        _player = player;
    }

    public void OnStart(World world)
    {
        var r = _host.Renderer;
        if (r is null)
            return;

        ref var pos = ref world.Components<Position>().Get(_player);
        var fb = r.SwapchainPixelSize;
        var p = WorldScreenSpace.ScreenPixelToWorldCenter(
            new Vector2D<float>(fb.X * 0.55f, fb.Y / 2f),
            fb);
        pos.X = p.X;
        pos.Y = p.Y;
    }

    public void OnFixedUpdate(World world, float fixedDeltaSeconds)
    {
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        ref var pos = ref world.Components<Position>().Get(_player);
        ref var vel = ref world.Components<Velocity>().Get(_player);

        pos.X += vel.X * fixedDeltaSeconds;
        pos.Y += vel.Y * fixedDeltaSeconds;

        var h = DemoPlayerConstants.SpriteHalfExtent;
        pos.X = Math.Clamp(pos.X, h, fb.X - h);
        pos.Y = Math.Clamp(pos.Y, h, fb.Y - h);

        // Keep a bright local point light centered on the player so lighting response
        // is obvious while moving and can drive bloom from lit highlights.
        var playerLight = new PointLight
        {
            PositionWorld = new Vector2D<float>(pos.X, pos.Y),
            Radius = 180f,
            Color = new Vector3D<float>(0.35f, 0.95f, 0.55f),
            Intensity = 1.2f,
            FalloffExponent = 2.25f,
            CastsShadow = false
        };
        r.SubmitPointLight(in playerLight);
    }
}
