using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Components;

/// <summary>Ten control-group slots (keys 1–9, 0) stored on the session singleton.</summary>
public struct ControlGroups : IComponent
{
    public const int GroupCount = 10;

    public ControlGroupslot Slot0;
    public ControlGroupslot Slot1;
    public ControlGroupslot Slot2;
    public ControlGroupslot Slot3;
    public ControlGroupslot Slot4;
    public ControlGroupslot Slot5;
    public ControlGroupslot Slot6;
    public ControlGroupslot Slot7;
    public ControlGroupslot Slot8;
    public ControlGroupslot Slot9;
}

/// <summary>Entity ids assigned to one control group (up to <see cref="Constants.UnitCount"/>).</summary>
public struct ControlGroupslot
{
    public byte Count;
    public EntityId Unit0;
    public EntityId Unit1;
    public EntityId Unit2;
    public EntityId Unit3;
    public EntityId Unit4;
    public EntityId Unit5;
    public EntityId Unit6;
    public EntityId Unit7;
    public EntityId Unit8;
    public EntityId Unit9;
}

/// <summary>Assign, recall, match, visible-select, and centroid helpers for <see cref="ControlGroups"/>.</summary>
public static class RtsControlGroupLogic
{
    public static ref ControlGroupslot Slot(ref ControlGroups groups, int groupIndex)
    {
        switch (groupIndex)
        {
            case 0: return ref groups.Slot0;
            case 1: return ref groups.Slot1;
            case 2: return ref groups.Slot2;
            case 3: return ref groups.Slot3;
            case 4: return ref groups.Slot4;
            case 5: return ref groups.Slot5;
            case 6: return ref groups.Slot6;
            case 7: return ref groups.Slot7;
            case 8: return ref groups.Slot8;
            case 9: return ref groups.Slot9;
            default: throw new ArgumentOutOfRangeException(nameof(groupIndex));
        }
    }

    public static void AssignFromSelection(
        ref ControlGroups groups,
        int groupIndex,
        World world,
        ReadOnlySpan<EntityId> selected)
    {
        ref var slot = ref Slot(ref groups, groupIndex);
        slot.Count = 0;
        for (var i = 0; i < selected.Length && slot.Count < Constants.UnitCount; i++)
        {
            if (!world.IsAlive(selected[i]))
                continue;
            SetSlotUnit(ref slot, slot.Count, selected[i]);
            slot.Count++;
        }
    }

    public static void ApplyRecallReplace(ref ControlGroups groups, int groupIndex, World world)
    {
        ClearAllSelection(world);
        ref readonly var slot = ref Slot(ref groups, groupIndex);
        if (slot.Count == 0)
            return;

        for (var i = 0; i < slot.Count; i++)
        {
            var id = GetSlotUnit(in slot, i);
            if (!world.IsAlive(id))
                continue;
            world.Get<UnitState>(id).Selected = true;
        }
    }

    public static void ApplyRecallAdditive(ref ControlGroups groups, int groupIndex, World world)
    {
        ref readonly var slot = ref Slot(ref groups, groupIndex);
        for (var i = 0; i < slot.Count; i++)
        {
            var id = GetSlotUnit(in slot, i);
            if (!world.IsAlive(id))
                continue;
            world.Get<UnitState>(id).Selected = true;
        }
    }

    public static void ClearAllSelection(World world)
    {
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<UnitTag, UnitState>()))
        {
            var states = chunk.Column<UnitState>();
            for (var i = 0; i < chunk.Count; i++)
                states[i].Selected = false;
        }
    }

    public static bool SelectionMatchesGroup(ref ControlGroups groups, int groupIndex, World world)
    {
        ref readonly var slot = ref Slot(ref groups, groupIndex);
        var aliveInGroup = 0;
        var selectedInGroup = 0;
        var selectedTotal = 0;

        for (var i = 0; i < slot.Count; i++)
        {
            var id = GetSlotUnit(in slot, i);
            if (!world.IsAlive(id))
                continue;
            aliveInGroup++;
            if (world.Get<UnitState>(id).Selected)
                selectedInGroup++;
        }

        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<UnitTag, UnitState>()))
        {
            var states = chunk.Column<UnitState>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (states[i].Selected)
                    selectedTotal++;
            }
        }

        return aliveInGroup > 0 && selectedInGroup == aliveInGroup && selectedTotal == aliveInGroup;
    }

    public static void SelectVisibleMembersInViewport(
        ref ControlGroups groups,
        int groupIndex,
        World world,
        Vector2D<float> cameraPositionWorld,
        float cameraRotationRadians,
        Vector2D<int> viewportSizeWorld)
    {
        ClearAllSelection(world);
        ref readonly var slot = ref Slot(ref groups, groupIndex);
        var vp = new Vector2D<float>(viewportSizeWorld.X, viewportSizeWorld.Y);

        for (var i = 0; i < slot.Count; i++)
        {
            var id = GetSlotUnit(in slot, i);
            if (!world.IsAlive(id))
                continue;

            ref readonly var tf = ref world.Get<Transform>(id);
            var vpPixel = CameraProjection.WorldToViewportPixel(
                tf.WorldPosition,
                cameraPositionWorld,
                cameraRotationRadians,
                vp);

            if (vpPixel.X < 0f || vpPixel.X > vp.X || vpPixel.Y < 0f || vpPixel.Y > vp.Y)
                continue;

            world.Get<UnitState>(id).Selected = true;
        }
    }

    public static bool TryComputeCentroidWorld(
        ref ControlGroups groups,
        int groupIndex,
        World world,
        out Vector2D<float> centroid)
    {
        centroid = default;
        ref readonly var slot = ref Slot(ref groups, groupIndex);
        var sumX = 0f;
        var sumY = 0f;
        var count = 0;

        for (var i = 0; i < slot.Count; i++)
        {
            var id = GetSlotUnit(in slot, i);
            if (!world.IsAlive(id))
                continue;
            var p = world.Get<Transform>(id).WorldPosition;
            sumX += p.X;
            sumY += p.Y;
            count++;
        }

        if (count == 0)
            return false;

        centroid = new Vector2D<float>(sumX / count, sumY / count);
        return true;
    }

    private static EntityId GetSlotUnit(in ControlGroupslot slot, int index) => index switch
    {
        0 => slot.Unit0,
        1 => slot.Unit1,
        2 => slot.Unit2,
        3 => slot.Unit3,
        4 => slot.Unit4,
        5 => slot.Unit5,
        6 => slot.Unit6,
        7 => slot.Unit7,
        8 => slot.Unit8,
        9 => slot.Unit9,
        _ => default
    };

    private static void SetSlotUnit(ref ControlGroupslot slot, int index, EntityId id)
    {
        switch (index)
        {
            case 0: slot.Unit0 = id; break;
            case 1: slot.Unit1 = id; break;
            case 2: slot.Unit2 = id; break;
            case 3: slot.Unit3 = id; break;
            case 4: slot.Unit4 = id; break;
            case 5: slot.Unit5 = id; break;
            case 6: slot.Unit6 = id; break;
            case 7: slot.Unit7 = id; break;
            case 8: slot.Unit8 = id; break;
            case 9: slot.Unit9 = id; break;
        }
    }
}
