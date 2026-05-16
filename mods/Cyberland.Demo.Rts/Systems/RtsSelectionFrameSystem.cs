using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Late: green selection frame around the unit when <see cref="RtsSessionState.UnitSelected"/> is true.</summary>
public sealed class RtsSelectionFrameSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<RtsSessionState>();

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _ = sessionRow;
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity sessionRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var session = ref sessionRow.Get<RtsSessionState>();
        var world = sessionRow.World;

        ref readonly var unitSpr = ref world.Get<Sprite>(session.UnitEntity);
        ref readonly var unitTf = ref world.Get<Transform>(session.UnitEntity);
        var ux = unitTf.WorldPosition.X;
        var uy = unitTf.WorldPosition.Y;
        var hu = unitSpr.HalfExtents.X;
        var hv = unitSpr.HalfExtents.Y;
        var pad = RtsConstants.SelectionPadding;
        var ph = pad * 0.5f;

        var top = session.SelectionBar0;
        var bottom = session.SelectionBar1;
        var left = session.SelectionBar2;
        var right = session.SelectionBar3;

        if (!session.UnitSelected)
        {
            world.Get<Sprite>(top).Visible = false;
            world.Get<Sprite>(bottom).Visible = false;
            world.Get<Sprite>(left).Visible = false;
            world.Get<Sprite>(right).Visible = false;
            return;
        }

        LayoutBar(world, top, new Vector2D<float>(ux, uy + hv + ph), new Vector2D<float>(hu + pad, ph));
        LayoutBar(world, bottom, new Vector2D<float>(ux, uy - hv - ph), new Vector2D<float>(hu + pad, ph));
        LayoutBar(world, left, new Vector2D<float>(ux - hu - ph, uy), new Vector2D<float>(ph, hv + pad));
        LayoutBar(world, right, new Vector2D<float>(ux + hu + ph, uy), new Vector2D<float>(ph, hv + pad));
    }

    private static void LayoutBar(World world, EntityId bar, Vector2D<float> center, Vector2D<float> halfExtents)
    {
        ref var t = ref world.Get<Transform>(bar);
        t.WorldPosition = center;
        ref var spr = ref world.Get<Sprite>(bar);
        spr.HalfExtents = halfExtents;
        spr.Visible = true;
    }
}
