using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>
/// Moves the player toward the mouse (faster when primary is held). Exactly one <see cref="PlayerTag"/> row — registered as a singleton.
/// </summary>
public sealed class PlayerMovementSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform>();

    private EntityId _controlEntity;
    private EntityId _stateEntity;
    private readonly GameHostServices _host;

    public PlayerMovementSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity player)
    {
        var world = player.World;
        _controlEntity = world.RequireSingleEntityWith<ControlState>("control state");
        _stateEntity = world.RequireSingleEntityWith<GameState>("game state");
    }

    /// <inheritdoc />
    public void OnSingletonFixedUpdate(in SingletonEntity player, float fixedDeltaSeconds)
    {
        ref var transform = ref player.Get<Transform>();
        ref readonly var control = ref player.World.Get<ControlState>(_controlEntity);
        ref readonly var state = ref player.World.Get<GameState>(_stateEntity);
        if (state.Phase is RoundPhase.Won or RoundPhase.Lost)
            return;

        var mouseWorld = control.MouseWorld;
        var speed = _host.Input?.IsDown("cyberland.demo.mousechase/primary") == true ? 420f : 300f;

        var toMouse = mouseWorld - transform.WorldPosition;
        var len = MathF.Sqrt(toMouse.X * toMouse.X + toMouse.Y * toMouse.Y);
        if (len <= 1f)
            return;

        var dir = toMouse / len;
        transform.WorldPosition += dir * speed * fixedDeltaSeconds;
    }
}
