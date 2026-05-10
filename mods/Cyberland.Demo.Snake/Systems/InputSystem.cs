using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Early phase: maps keyboard actions to <see cref="Control"/> and updates direction intent on <see cref="Session"/>.
/// </summary>
/// <remarks>
/// <see cref="Control"/> is its own row; <see cref="Session"/> is resolved once—two singleton archetypes, wired by entity id in <see cref="OnSingletonStart"/>.
/// </remarks>
public sealed class InputSystem : ISingletonSystem, ISingletonEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Control>();

    private EntityId _sessionEntity;
    private readonly GameHostServices _host;

    /// <summary>Creates the input driver.</summary>
    public InputSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity controlRow)
    {
        _sessionEntity = controlRow.World.RequireSingleEntityWith<Session>("Snake session");
    }

    /// <inheritdoc />
    public void OnSingletonEarlyUpdate(in SingletonEntity controlRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var world = controlRow.World;
        ref var ctl = ref controlRow.Get<Control>();
        // Do not clear the whole component: StartGame is latched for TickSystem (fixed). High refresh can deliver many
        // Render ticks before the next fixed substep; ctl = default would erase an unconsumed StartGame before Tick runs.
        // Frame gameplay commands gate one-shot edges without relying on ConsumePressed here — same BeginFrame snapshot in every early pass.
        var r = _host.Renderer;
        var input = _host.Input;
        if (input.IsDown("cyberland.common/quit")) { r.RequestClose?.Invoke(); return; }
        ref var session = ref world.Get<Session>(_sessionEntity);
        switch (session.Phase)
        {
            case Phase.Title:
                if (input.HasAnyActionPressedThisFrame("cyberland.demo.snake/start_game", "cyberland.common/start"))
                    ctl.StartGame = true;
                break;
            case Phase.Playing:
                if (input.HasActionPressedThisFrame("cyberland.demo.snake/up") && session.DirY == 0) { session.NextDirX = 0; session.NextDirY = 1; }
                else if (input.HasActionPressedThisFrame("cyberland.demo.snake/down") && session.DirY == 0) { session.NextDirX = 0; session.NextDirY = -1; }
                else if (input.HasActionPressedThisFrame("cyberland.demo.snake/left") && session.DirX == 0) { session.NextDirX = -1; session.NextDirY = 0; }
                else if (input.HasActionPressedThisFrame("cyberland.demo.snake/right") && session.DirX == 0) { session.NextDirX = 1; session.NextDirY = 0; }
                break;
            case Phase.GameOver:
            case Phase.Won:
                if (input.HasAnyActionPressedThisFrame("cyberland.demo.snake/start_game", "cyberland.common/start"))
                    ctl.StartGame = true;
                break;
        }
    }
}
