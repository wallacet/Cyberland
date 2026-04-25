using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Fixed-step Snake simulation on the <see cref="Session"/> component. Sequential: game logic lives in <see cref="Session.Step"/>.
/// </summary>
public sealed class TickSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;


    private World _world = null!;
    private readonly GameHostServices _host;
    private readonly EntityId _sessionEntity;
    private readonly EntityId _controlEntity;
    public TickSystem(GameHostServices host, EntityId sessionEntity, EntityId controlEntity)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _controlEntity = controlEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
    }

    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        if (fb.X <= 0 || fb.Y <= 0) return;
        ref var session = ref _world.Get<Session>(_sessionEntity);
        // Idempotent layout: same math as tilemap/lights/visuals so each system can run independently.
        session.UpdateLayout(fb.X, fb.Y);
        ref var ctl = ref _world.Get<Control>(_controlEntity);
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
