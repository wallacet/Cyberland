using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;

namespace Cyberland.Demo.Snake;

public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _sessionEntity;
    private readonly EntityId _controlEntity;
    public InputSystem(GameHostServices host, EntityId sessionEntity, EntityId controlEntity)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _controlEntity = controlEntity;
    }
    public void OnEarlyUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        ref var ctl = ref world.Components<Control>().Get(_controlEntity);
        ctl = default;
        var r = _host.Renderer;
        var input = _host.Input;
        if (r is null) return;
        var kb = input?.Keyboards.Count > 0 ? input.Keyboards[0] : null;
        if (kb is null) return;
        if (kb.IsKeyPressed(Key.Q)) { r.RequestClose?.Invoke(); return; }
        ref var session = ref world.Components<Session>().Get(_sessionEntity);
        switch (session.Phase)
        {
            case Phase.Title: if (kb.IsKeyPressed(Key.Enter) || kb.IsKeyPressed(Key.R)) ctl.StartGame = true; break;
            case Phase.Playing:
                if (kb.IsKeyPressed(Key.Up) && session.DirY == 0) { session.NextDirX = 0; session.NextDirY = 1; }
                else if (kb.IsKeyPressed(Key.Down) && session.DirY == 0) { session.NextDirX = 0; session.NextDirY = -1; }
                else if (kb.IsKeyPressed(Key.Left) && session.DirX == 0) { session.NextDirX = -1; session.NextDirY = 0; }
                else if (kb.IsKeyPressed(Key.Right) && session.DirX == 0) { session.NextDirX = 1; session.NextDirY = 0; }
                break;
            case Phase.GameOver: if (kb.IsKeyPressed(Key.Enter) || kb.IsKeyPressed(Key.R)) ctl.StartGame = true; break;
        }
    }
}
