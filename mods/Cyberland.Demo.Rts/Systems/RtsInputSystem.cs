using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Early: quit, marquee box select, click select, right-click formation move orders.</summary>
public sealed class RtsInputSystem : ISingletonSystem, ISingletonEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<RtsSessionState>();

    private readonly GameHostServices _host;
    private EntityId[] _selectedScratch = [];
    private Vector2D<float> _dragStartScreen;

    public RtsInputSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _ = sessionRow;
        _selectedScratch = new EntityId[RtsConstants.UnitCount];
    }

    /// <inheritdoc />
    public void OnSingletonEarlyUpdate(in SingletonEntity sessionRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var input = _host.Input;
        var renderer = _host.Renderer;

        if (input.IsDown("cyberland.common/quit"))
        {
            renderer.RequestClose?.Invoke();
            return;
        }

        ref var session = ref sessionRow.Get<RtsSessionState>();
        var world = sessionRow.World;

        if (input.ConsumeMouseButtonPressed(MouseButton.Left))
        {
            var mouse = input.GetMousePosition(CoordinateSpace.WorldSpace);
            session.BoxDragActive = true;
            session.BoxDragStartWorld = new Vector2D<float>(mouse.X, mouse.Y);
            session.BoxDragEndWorld = session.BoxDragStartWorld;
            var screen = input.MousePositionScreen;
            _dragStartScreen = new Vector2D<float>(screen.X, screen.Y);
        }

        if (session.BoxDragActive && input.MouseButton(MouseButton.Left))
        {
            var mouse = input.GetMousePosition(CoordinateSpace.WorldSpace);
            session.BoxDragEndWorld = new Vector2D<float>(mouse.X, mouse.Y);
        }

        if (input.ConsumeMouseButtonReleased(MouseButton.Left) && session.BoxDragActive)
        {
            session.BoxDragActive = false;
            var endScreen = input.MousePositionScreen;
            var end = new Vector2D<float>(endScreen.X, endScreen.Y);
            var dragPx = MathF.Sqrt(
                (end.X - _dragStartScreen.X) * (end.X - _dragStartScreen.X) +
                (end.Y - _dragStartScreen.Y) * (end.Y - _dragStartScreen.Y));

            if (dragPx < RtsConstants.BoxSelectMinDragScreenPx)
                ApplyClickSelect(world, session.BoxDragEndWorld);
            else
                ApplyBoxSelect(world, session.BoxDragStartWorld, session.BoxDragEndWorld);
        }

        if (input.ConsumeMouseButtonPressed(MouseButton.Right))
        {
            var selectedCount = CollectSelected(world, _selectedScratch);
            if (selectedCount > 0)
            {
                var mouse = input.GetMousePosition(CoordinateSpace.WorldSpace);
                var anchor = ClampToPlayfield(new Vector2D<float>(mouse.X, mouse.Y));
                RtsFormation.AssignGridTargets(anchor, _selectedScratch.AsSpan(0, selectedCount), world);
                for (var i = 0; i < selectedCount; i++)
                {
                    ref var state = ref world.Get<RtsUnitState>(_selectedScratch[i]);
                    state.HasMoveOrder = true;
                }
            }
        }
    }

    private static void ApplyClickSelect(World world, Vector2D<float> worldPoint)
    {
        EntityId? hit = null;
        var bestSort = float.NegativeInfinity;
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<RtsUnitTag, Transform, Sprite>()))
        {
            var transforms = chunk.Column<Transform>();
            var sprites = chunk.Column<Sprite>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                if (!HitUnit(transforms[i].WorldPosition, sprites[i].HalfExtents, worldPoint))
                    continue;
                if (sprites[i].SortKey <= bestSort)
                    continue;
                bestSort = sprites[i].SortKey;
                hit = entities[i];
            }
        }

        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<RtsUnitTag, RtsUnitState>()))
        {
            var states = chunk.Column<RtsUnitState>();
            var entities = chunk.Entities;
            for (var i = 0; i < entities.Length; i++)
                states[i].Selected = hit.HasValue && entities[i] == hit.Value;
        }
    }

    private static void ApplyBoxSelect(World world, Vector2D<float> a, Vector2D<float> b)
    {
        var minX = MathF.Min(a.X, b.X);
        var maxX = MathF.Max(a.X, b.X);
        var minY = MathF.Min(a.Y, b.Y);
        var maxY = MathF.Max(a.Y, b.Y);

        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<RtsUnitTag, Transform, Sprite, RtsUnitState>()))
        {
            var transforms = chunk.Column<Transform>();
            var sprites = chunk.Column<Sprite>();
            var states = chunk.Column<RtsUnitState>();
            var entities = chunk.Entities;
            for (var i = 0; i < entities.Length; i++)
            {
                var p = transforms[i].WorldPosition;
                var h = sprites[i].HalfExtents;
                states[i].Selected = AabbIntersectsRect(p, h, minX, minY, maxX, maxY);
            }
        }
    }

    private static int CollectSelected(World world, EntityId[] scratch)
    {
        var count = 0;
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<RtsUnitTag, RtsUnitState>()))
        {
            var states = chunk.Column<RtsUnitState>();
            var entities = chunk.Entities;
            for (var i = 0; i < entities.Length; i++)
            {
                if (!states[i].Selected)
                    continue;
                if (count < scratch.Length)
                    scratch[count++] = entities[i];
            }
        }

        return count;
    }

    private static bool HitUnit(Vector2D<float> center, Vector2D<float> halfExtents, Vector2D<float> worldPoint) =>
        MathF.Abs(worldPoint.X - center.X) <= halfExtents.X &&
        MathF.Abs(worldPoint.Y - center.Y) <= halfExtents.Y;

    private static bool AabbIntersectsRect(
        Vector2D<float> center,
        Vector2D<float> halfExtents,
        float minX,
        float minY,
        float maxX,
        float maxY) =>
        center.X + halfExtents.X >= minX &&
        center.X - halfExtents.X <= maxX &&
        center.Y + halfExtents.Y >= minY &&
        center.Y - halfExtents.Y <= maxY;

    private static Vector2D<float> ClampToPlayfield(Vector2D<float> p)
    {
        var s = RtsConstants.PlaySize;
        return new Vector2D<float>(Math.Clamp(p.X, 0f, s), Math.Clamp(p.Y, 0f, s));
    }
}
