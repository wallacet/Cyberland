using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Recomputes arena metrics and brick cell positions when the framebuffer size changes.
/// Uses <see cref="Parallel.For"/> over brick rows within each chunk; the scheduler supplies
/// <see cref="ParallelOptions"/> (single-threaded on <see cref="OnStart"/>, host-capped in early update).
/// </summary>
public sealed class ArenaLayoutSystem : IParallelSystem, IParallelEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    /// <remarks>Brick cells carry <see cref="Cell"/>, <see cref="Transform"/>, and <see cref="Trigger"/>; layout updates both world placement and trigger AABBs from the same chunk columns.</remarks>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Cell, Transform, Trigger>();


    private World _world = null!;
    /// <summary>
    /// <see cref="OnStart"/> runs before the parallel early phase; use one worker so layout matches
    /// the same code path as <see cref="OnParallelEarlyUpdate"/> without racing other startup systems.
    /// </summary>
    private static readonly ParallelOptions StartupParallelOptions = new() { MaxDegreeOfParallelism = 1 };

    private readonly GameHostServices _host;
    private readonly EntityId _stateEntity;
    public ArenaLayoutSystem(GameHostServices host, EntityId stateEntity)
    {
        _host = host;
        _stateEntity = stateEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        EnsureRendererAvailable();
        UpdateLayoutIfNeeded(archetype, StartupParallelOptions);
    }

    public void OnParallelEarlyUpdate(ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        UpdateLayoutIfNeeded(archetype, parallelOptions);
    }

    private void EnsureRendererAvailable()
    {
        if (_host.Renderer is not null)
            return;

        EngineDiagnostics.Report(
            EngineErrorSeverity.Major,
            "Cyberland.Demo.BrickBreaker — ArenaLayout init failed",
            "Host.Renderer was null during ArenaLayoutSystem.OnStart; arena layout requires swapchain size.");
        throw new InvalidOperationException("ArenaLayoutSystem requires Host.Renderer during OnStart.");
    }

    private void UpdateLayoutIfNeeded(ChunkQueryAll cellArchetype, ParallelOptions parallelOptions)
    {
        // Use the mod's fixed virtual canvas, not IRenderer.ActiveCameraViewportSize, in this parallel *early*
        // pass: cameras are submitted in *late*, so the renderer may still report swapchain size here and the
        // arena would be laid out for the wrong extent relative to Camera2D (Constants.CanvasWidth/Height).
        var fb = new Vector2D<int>(Constants.CanvasWidth, Constants.CanvasHeight);

        ref var game = ref _world.Get<GameState>(_stateEntity);
        var layoutChanged = !game.LayoutInitialized || game.LayoutWidth != fb.X || game.LayoutHeight != fb.Y;
        if (!layoutChanged) return;

        var margin = 40f;
        game.ArenaMinX = margin;
        game.ArenaMaxX = fb.X - margin;
        game.ArenaMinY = margin + 80f;
        game.ArenaMaxY = fb.Y - margin;
        game.PaddleY = game.ArenaMinY + 36f;
        game.BrickW = (game.ArenaMaxX - game.ArenaMinX) / Constants.Cols;
        game.BrickH = 22f;
        game.BrickOriginX = game.ArenaMinX;
        game.BrickTopY = game.ArenaMaxY - 40f;
        game.LayoutInitialized = true;
        game.LayoutWidth = fb.X;
        game.LayoutHeight = fb.Y;

        var brickOriginX = game.BrickOriginX;
        var brickTopY = game.BrickTopY;
        var brickW = game.BrickW;
        var brickH = game.BrickH;

        foreach (var chunk in cellArchetype)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref readonly var cell = ref chunk.Column<Cell>()[i];
                var bx = brickOriginX + (cell.X + 0.5f) * brickW;
                var by = brickTopY - (cell.Y + 0.5f) * brickH;
                ref var transform = ref chunk.Column<Transform>()[i];
                transform.LocalPosition = new Vector2D<float>(bx, by);
                ref var trigger = ref chunk.Column<Trigger>()[i];
                trigger.HalfExtents = new Vector2D<float>(brickW * 0.46f, brickH * 0.45f);
            });
        }
    }
}
