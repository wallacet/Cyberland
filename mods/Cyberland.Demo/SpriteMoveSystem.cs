using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// WASD / arrows move the Vulkan sprite in <see cref="WorldScreenSpace"/>; the host renderer converts to pixels.
/// </summary>
public sealed class SpriteMoveSystem : ISystem
{
    public const float SpriteHalfExtent = 48f;
    private const float MoveSpeed = 320f;

    private readonly GameHostServices _host;
    private Vector2D<float> _spriteWorld;
    private bool _spriteInitialized;

    public SpriteMoveSystem(GameHostServices host) =>
        _host = host;

    public void OnUpdate(World world, float deltaSeconds)
    {
        var renderer = _host.Renderer;
        if (renderer is null)
            return;

        if (!_spriteInitialized)
        {
            var fb = renderer.SwapchainPixelSize;
            _spriteWorld = WorldScreenSpace.ScreenPixelToWorldCenter(
                new Vector2D<float>(fb.X / 2f, fb.Y / 2f),
                fb);
            _spriteInitialized = true;
        }

        var input = _host.Input;
        if (input is null || input.Keyboards.Count == 0)
        {
            renderer.SetSpriteWorld(_spriteWorld.X, _spriteWorld.Y, SpriteHalfExtent);
            return;
        }

        var kb = input.Keyboards[0];
        var bindings = _host.KeyBindings;

        float dx = 0f, dy = 0f;
        if (bindings.IsDown(kb, "move_left") || kb.IsKeyPressed(Key.Left))
            dx -= 1f;
        if (bindings.IsDown(kb, "move_right") || kb.IsKeyPressed(Key.Right))
            dx += 1f;
        if (bindings.IsDown(kb, "move_up") || kb.IsKeyPressed(Key.Up))
            dy += 1f;
        if (bindings.IsDown(kb, "move_down") || kb.IsKeyPressed(Key.Down))
            dy -= 1f;

        if (dx != 0f || dy != 0f)
        {
            var len = MathF.Sqrt(dx * dx + dy * dy);
            dx /= len;
            dy /= len;
        }

        _spriteWorld += new Vector2D<float>(dx * MoveSpeed * deltaSeconds, dy * MoveSpeed * deltaSeconds);

        var size = renderer.SwapchainPixelSize;
        var h = SpriteHalfExtent;
        _spriteWorld.X = Math.Clamp(_spriteWorld.X, h, size.X - h);
        _spriteWorld.Y = Math.Clamp(_spriteWorld.Y, h, size.Y - h);

        renderer.SetSpriteWorld(_spriteWorld.X, _spriteWorld.Y, SpriteHalfExtent);
    }
}
