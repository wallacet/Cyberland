using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Sequential early input: fills <see cref="Control"/> and maps <c>Q</c> to <see cref="IRenderer.RequestClose"/>.</summary>
public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _stateEntity;
    private readonly EntityId _controlEntity;
    private World _world;

    public InputSystem(GameHostServices host, EntityId stateEntity, EntityId controlEntity)
    {
        _host = host;
        _stateEntity = stateEntity;
        _controlEntity = controlEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
    }

    public void OnEarlyUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var world = _world;
        ref var c = ref world.Components<Control>().Get(_controlEntity);
        c = default;
        var r = _host.Renderer;
        var input = _host.Input;
        if (r is null)
            return;
        var kb = input?.Keyboards.Count > 0 ? input.Keyboards[0] : null;
        if (kb is null)
            return;
        if (kb.IsKeyPressed(Key.Q))
        {
            r.RequestClose?.Invoke();
            return;
        }
        var phase = world.Components<GameState>().Get(_stateEntity).Phase;
        switch (phase)
        {
            case Phase.Title:
                if (kb.IsKeyPressed(Key.Enter))
                    c.StartRound = true;
                break;
            case Phase.GameOver:
                if (kb.IsKeyPressed(Key.Enter) || kb.IsKeyPressed(Key.R))
                    c.StartRound = true;
                break;
            case Phase.Playing:
                if (kb.IsKeyPressed(Key.A) || kb.IsKeyPressed(Key.Left))
                    c.MoveLeft = true;
                if (kb.IsKeyPressed(Key.D) || kb.IsKeyPressed(Key.Right))
                    c.MoveRight = true;
                if (kb.IsKeyPressed(Key.Space))
                    c.LaunchBall = true;
                break;
        }
    }
}
