using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.Snake;

/// <summary>
/// One-shot bootstrap on the singleton <see cref="Session"/> row: registers the tilemap grid and allocates segment/HUD entities referenced by <see cref="VisualBundle"/>.
/// </summary>
/// <remarks>
/// Registered as <see cref="ISingletonSystem"/> so resolution happens once via <see cref="OnSingletonStart"/>—no empty <see cref="SystemQuerySpec"/> spawn-only system.
/// </remarks>
public sealed class SegmentPoolBootstrapSystem : ISingletonSystem
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Session>();

    private readonly GameHostServices _host;

    /// <summary>Creates the bootstrap entry (cold start already ran in <see cref="SetupSceneAsync"/>).</summary>
    public SegmentPoolBootstrapSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        var world = sessionRow.World;
        if (_host.Tilemaps is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake.SegmentPoolBootstrapSystem", "GameHostServices.Tilemaps was null during OnSingletonStart.");
            throw new InvalidOperationException("Tilemap store is required by Snake bootstrap.");
        }

        var arena = world.RequireSingleEntityWith<Tilemap>("Snake arena tilemap");
        var visualsEntity = world.RequireSingleEntityWith<VisualBundle>("Snake visuals bundle");

        ref var session = ref sessionRow.Get<Session>();
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

        _host.Tilemaps.Register(arena, grid, Constants.GridW, Constants.GridH);

        ref var visuals = ref world.Get<VisualBundle>(visualsEntity);
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
        visuals.TxtFps = world.CreateEntity();
    }
}
