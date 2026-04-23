using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Recomputes arena metrics and brick cell positions when the framebuffer size changes.
/// Uses <see cref="Parallel.ForEach"/> over ECS chunks so brick transforms update in parallel.
/// </summary>
public sealed class ArenaLayoutSystem : IParallelSystem, IParallelEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Cell>();

    private readonly GameHostServices _host;
    private readonly EntityId _stateEntity;
    private readonly List<MultiComponentChunkView> _chunks = new();

    public ArenaLayoutSystem(GameHostServices host, EntityId stateEntity)
    {
        _host = host;
        _stateEntity = stateEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        EnsureRendererAvailable();
        UpdateLayoutIfNeeded(world, archetype, parallelOptions: null);
    }

    public void OnParallelEarlyUpdate(World world, ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        UpdateLayoutIfNeeded(world, archetype, parallelOptions);
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

    private void UpdateLayoutIfNeeded(World world, ChunkQueryAll cellArchetype, ParallelOptions? parallelOptions)
    {
        var fb = _host.Renderer!.SwapchainPixelSize;
        if (fb.X <= 0 || fb.Y <= 0)
            return;

        ref var game = ref world.Components<GameState>().Get(_stateEntity);
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
        var transforms = world.Components<Transform>();
        var trigger = world.Components<Trigger>();

        void ApplyChunk(MultiComponentChunkView chunk)
        {
            var entities = chunk.Entities;
            var cells = chunk.Column<Cell>(0);
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                ref readonly var cell = ref cells[i];
                var bx = brickOriginX + (cell.X + 0.5f) * brickW;
                var by = brickTopY - (cell.Y + 0.5f) * brickH;
                ref var transform = ref transforms.Get(entity);
                transform.LocalPosition = new Vector2D<float>(bx, by);
                transform.WorldPosition = transform.LocalPosition;
                ref var t = ref trigger.Get(entity);
                t.HalfExtents = new Vector2D<float>(brickW * 0.46f, brickH * 0.45f);
            }
        }

        if (parallelOptions is null)
        {
            foreach (var chunk in cellArchetype)
                ApplyChunk(chunk);
        }
        else
        {
            _chunks.Clear();
            foreach (var chunk in cellArchetype)
                _chunks.Add(chunk);
            Parallel.ForEach(_chunks, parallelOptions!, ApplyChunk);
        }
    }
}
