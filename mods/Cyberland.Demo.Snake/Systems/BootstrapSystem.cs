using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.Snake;

/// <summary>
/// One-shot entity creation for Snake. Registered as <see cref="ISystem"/> (sequential): only <see cref="OnStart"/> runs.
/// </summary>
public sealed class BootstrapSystem : ISystem
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Session>();

    private readonly GameHostServices _host;
    private readonly EntityId _sessionEntity;
    private readonly EntityId _arenaEntity;
    private readonly EntityId _visualsEntity;

    public BootstrapSystem(GameHostServices host, EntityId sessionEntity, EntityId arenaEntity, EntityId visualsEntity)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _arenaEntity = arenaEntity;
        _visualsEntity = visualsEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = archetype;
        if (_host.Tilemaps is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake.BootstrapSystem", "GameHostServices.Tilemaps was null during OnStart.");
            throw new InvalidOperationException("Tilemap store is required by Snake bootstrap.");
        }

        ref var session = ref world.Components<Session>().Get(_sessionEntity);
        session.EnsureInitialized();
        session.Phase = Phase.Title;
        session.TickAcc = 0f;
        session.DirX = 1;
        session.DirY = 0;
        session.NextDirX = 1;
        session.NextDirY = 0;
        session.Snake.Clear();
        session.Food = (Constants.GridW / 2, Constants.GridH / 2);
        session.FoodsEaten = 0;

        var grid = new int[Constants.GridW * Constants.GridH];
        for (var i = 0; i < grid.Length; i++)
            grid[i] = 1;

        _host.Tilemaps.Register(_arenaEntity, grid, Constants.GridW, Constants.GridH);

        ref var visuals = ref world.Components<VisualBundle>().Get(_visualsEntity);
        visuals.Segments ??= new EntityId[Constants.GridW * Constants.GridH];
        for (var i = 0; i < visuals.Segments.Length; i++)
            visuals.Segments[i] = world.CreateEntity();

        visuals.Food = world.CreateEntity();
        visuals.TitleBar = world.CreateEntity();
        visuals.GoPanel = world.CreateEntity();
        visuals.ScoreBar = world.CreateEntity();
        visuals.TxtTitle = world.CreateEntity();
        visuals.TxtHintTitle = world.CreateEntity();
        visuals.TxtGameOver = world.CreateEntity();
        visuals.TxtHintGo = world.CreateEntity();
        visuals.TxtPlaying = world.CreateEntity();
        visuals.TxtScore = world.CreateEntity();
    }
}
