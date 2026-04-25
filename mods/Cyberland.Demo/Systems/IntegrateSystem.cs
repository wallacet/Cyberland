using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Places the player once in <see cref="OnStart"/> from the initial framebuffer size, then integrates <see cref="Transform"/>
/// from <see cref="Velocity"/> and clamps to the playfield each fixed tick. Chunk iteration is via
/// <see cref="SystemQuerySpec.All{PlayerTag,Transform,Velocity}()"/> (single player row).
/// </summary>
public sealed class IntegrateSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform, Velocity>();

    private readonly GameHostServices _host;

    /// <summary>False until <see cref="OnStart"/> places the player.</summary>
    private bool _initialized;

    public IntegrateSystem(GameHostServices host)
    {
        _host = host;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = _host.RendererRequired;
        _ = archetype.RequireSingleEntityWith<PlayerTag>("player");
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
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

        _initialized = true;
    }

    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        if (!_initialized)
            return;

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

                var h = Constants.SpriteHalfExtent;
                pos.X = Math.Clamp(pos.X, h, fb.X - h);
                pos.Y = Math.Clamp(pos.Y, h, fb.Y - h);
                transform.LocalPosition = pos;
            }
        }
    }
}
