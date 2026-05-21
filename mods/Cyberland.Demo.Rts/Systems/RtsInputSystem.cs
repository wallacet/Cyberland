using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Scene;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Early: quit, control groups, marquee/click select (Shift/Ctrl), formation move orders.</summary>
public sealed class RtsInputSystem : ISingletonSystem, ISingletonEarlyUpdate
{
    private enum RtsSelectionMode
    {
        Replace,
        Add,
        Remove
    }

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<RtsSessionState, RtsControlGroups>();

    private readonly GameHostServices _host;
    private EntityId[] _selectedScratch = [];
    private Vector2D<float> _dragStartScreen;
    private readonly bool[] _prevDigitDown = new bool[RtsControlGroups.GroupCount];
    private static readonly Key[] GroupKeys =
    [
        Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5,
        Key.Number6, Key.Number7, Key.Number8, Key.Number9, Key.Number0
    ];

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
        var input = _host.Input;
        var renderer = _host.Renderer;

        if (input.IsDown("cyberland.common/quit"))
        {
            renderer.RequestClose?.Invoke();
            return;
        }

        ref var session = ref sessionRow.Get<RtsSessionState>();
        ref var groups = ref sessionRow.Get<RtsControlGroups>();
        var world = sessionRow.World;
        session.GroupKeyClock += deltaSeconds;

        ProcessControlGroupHotkeys(ref session, ref groups, world, input);

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

            var mode = ResolveSelectionMode(input);
            if (dragPx < RtsConstants.BoxSelectMinDragScreenPx)
                ApplyClickSelect(world, session.BoxDragEndWorld, mode);
            else
                ApplyBoxSelect(world, session.BoxDragStartWorld, session.BoxDragEndWorld, mode);
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

        UpdateDigitEdgeLatch(input);
    }

    private void ProcessControlGroupHotkeys(
        ref RtsSessionState session,
        ref RtsControlGroups groups,
        World world,
        IInputService input)
    {
        var ctrl = IsCtrlHeld(input);
        var shift = IsShiftHeld(input);

        for (var g = 0; g < RtsControlGroups.GroupCount; g++)
        {
            if (!ConsumeDigitPressed(g, input))
                continue;

            if (ctrl)
            {
                var count = CollectSelected(world, _selectedScratch);
                if (count > 0)
                    RtsControlGroupLogic.AssignFromSelection(ref groups, g, world, _selectedScratch.AsSpan(0, count));
                RecordGroupKeyTap(ref session, (byte)g);
                continue;
            }

            if (shift)
            {
                RtsControlGroupLogic.ApplyRecallAdditive(ref groups, g, world);
                session.ActiveGroupIndex = (byte)g;
                RecordGroupKeyTap(ref session, (byte)g);
                continue;
            }

            // Plain digit: already-selected → camera center; else double-tap → visible select; else recall.
            if (RtsControlGroupLogic.SelectionMatchesGroup(ref groups, g, world))
            {
                RequestCameraFocusOnGroup(ref session, ref groups, g, world);
                RecordGroupKeyTap(ref session, (byte)g);
                continue;
            }

            if (session.LastGroupKeyIndex == g &&
                session.GroupKeyClock - session.LastGroupKeyTime <= RtsConstants.GroupDoubleTapSeconds)
            {
                ApplyVisibleGroupSelect(ref session, ref groups, g, world);
                session.ActiveGroupIndex = (byte)g;
                RecordGroupKeyTap(ref session, (byte)g);
                continue;
            }

            RtsControlGroupLogic.ApplyRecallReplace(ref groups, g, world);
            session.ActiveGroupIndex = (byte)g;
            RecordGroupKeyTap(ref session, (byte)g);
        }
    }

    private void RequestCameraFocusOnGroup(
        ref RtsSessionState session,
        ref RtsControlGroups groups,
        int groupIndex,
        World world)
    {
        if (!RtsControlGroupLogic.TryComputeCentroidWorld(ref groups, groupIndex, world, out var centroid))
            return;

        ref readonly var cam2 = ref world.Get<Camera2D>(session.CameraEntity);
        var clamped = RtsCameraBounds.ClampCenterPosition(
            centroid,
            cam2.ViewportSizeWorld.X,
            cam2.ViewportSizeWorld.Y);
        session.PendingCameraFocusWorld = clamped;
        session.PendingCameraFocus = true;
    }

    private void ApplyVisibleGroupSelect(
        ref RtsSessionState session,
        ref RtsControlGroups groups,
        int groupIndex,
        World world)
    {
        ref readonly var camTf = ref world.Get<Transform>(session.CameraEntity);
        ref readonly var cam2 = ref world.Get<Camera2D>(session.CameraEntity);
        RtsControlGroupLogic.SelectVisibleMembersInViewport(
            ref groups,
            groupIndex,
            world,
            camTf.WorldPosition,
            camTf.WorldRotationRadians,
            cam2.ViewportSizeWorld);
    }

    private static void RecordGroupKeyTap(ref RtsSessionState session, byte groupIndex)
    {
        session.LastGroupKeyIndex = groupIndex;
        session.LastGroupKeyTime = session.GroupKeyClock;
    }

    private bool ConsumeDigitPressed(int groupIndex, IInputService input)
    {
        var key = GroupKeys[groupIndex];
        var down = input.IsControlDown(InputControl.Keyboard(key));
        var pressed = down && !_prevDigitDown[groupIndex];
        return pressed;
    }

    private void UpdateDigitEdgeLatch(IInputService input)
    {
        for (var g = 0; g < RtsControlGroups.GroupCount; g++)
            _prevDigitDown[g] = input.IsControlDown(InputControl.Keyboard(GroupKeys[g]));
    }

    private static RtsSelectionMode ResolveSelectionMode(IInputService input)
    {
        if (IsCtrlHeld(input))
            return RtsSelectionMode.Remove;
        if (IsShiftHeld(input))
            return RtsSelectionMode.Add;
        return RtsSelectionMode.Replace;
    }

    private static bool IsShiftHeld(IInputService input) =>
        input.IsControlDown(InputControl.Keyboard(Key.ShiftLeft)) ||
        input.IsControlDown(InputControl.Keyboard(Key.ShiftRight));

    private static bool IsCtrlHeld(IInputService input) =>
        input.IsControlDown(InputControl.Keyboard(Key.ControlLeft)) ||
        input.IsControlDown(InputControl.Keyboard(Key.ControlRight));

    private static void ApplyClickSelect(World world, Vector2D<float> worldPoint, RtsSelectionMode mode)
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

        if (mode == RtsSelectionMode.Replace)
        {
            foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<RtsUnitTag, RtsUnitState>()))
            {
                var states = chunk.Column<RtsUnitState>();
                var entities = chunk.Entities;
                for (var i = 0; i < chunk.Count; i++)
                    states[i].Selected = hit.HasValue && entities[i] == hit.Value;
            }

            return;
        }

        if (!hit.HasValue)
            return;

        ref var hitState = ref world.Get<RtsUnitState>(hit.Value);
        if (mode == RtsSelectionMode.Add)
            hitState.Selected = true;
        else if (mode == RtsSelectionMode.Remove)
            hitState.Selected = false;
    }

    private static void ApplyBoxSelect(
        World world,
        Vector2D<float> a,
        Vector2D<float> b,
        RtsSelectionMode mode)
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
            for (var i = 0; i < chunk.Count; i++)
            {
                var p = transforms[i].WorldPosition;
                var h = sprites[i].HalfExtents;
                var inBox = AabbIntersectsRect(p, h, minX, minY, maxX, maxY);

                switch (mode)
                {
                    case RtsSelectionMode.Replace:
                        states[i].Selected = inBox;
                        break;
                    case RtsSelectionMode.Add:
                        if (inBox)
                            states[i].Selected = true;
                        break;
                    case RtsSelectionMode.Remove:
                        if (inBox)
                            states[i].Selected = false;
                        break;
                }
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
            for (var i = 0; i < chunk.Count; i++)
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
