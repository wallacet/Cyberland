using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Fixed-step Snake simulation on the <see cref="Session"/> row. Game rules are centralized in <see cref="Session.Step"/>.
/// </summary>
public sealed class TickSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Session>();

    private EntityId _controlEntity;
    private readonly GameHostServices _host;

    /// <summary>Creates the tick driver.</summary>
    public TickSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _controlEntity = sessionRow.World.RequireSingleEntityWith<Control>("Snake control");
    }

    /// <inheritdoc />
    public void OnSingletonFixedUpdate(in SingletonEntity sessionRow, float fixedDeltaSeconds)
    {
        var world = sessionRow.World;
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        if (fb.X <= 0 || fb.Y <= 0) return;
        ref var session = ref sessionRow.Get<Session>();
        // Idempotent layout: same math as tilemap/lights/visuals so each system can run independently.
        session.UpdateLayout(fb.X, fb.Y);
        ref var ctl = ref world.Get<Control>(_controlEntity);
        if (ctl.StartGame) { ctl.StartGame = false; session.StartGame(); }
        if (session.Phase != Phase.Playing) return;
        session.TickAcc += fixedDeltaSeconds;
        while (session.TickAcc >= Constants.TickSeconds)
        {
            session.TickAcc -= Constants.TickSeconds;
            session.DirX = session.NextDirX;
            session.DirY = session.NextDirY;
            session.Step();
        }
    }
}
