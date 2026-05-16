using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Early: quit, left-click select/deselect, right-click move order only while selected; an in-progress move is not cancelled by deselecting.</summary>
public sealed class RtsInputSystem : ISingletonSystem, ISingletonEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<RtsSessionState>();

    private readonly GameHostServices _host;

    public RtsInputSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _ = sessionRow;
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
            var mw = new Vector2D<float>(mouse.X, mouse.Y);
            if (HitUnit(world, session.UnitEntity, mw))
                session.UnitSelected = !session.UnitSelected;
            else
                session.UnitSelected = false;
        }

        if (input.ConsumeMouseButtonPressed(MouseButton.Right))
        {
            if (session.UnitSelected)
            {
                var mouse = input.GetMousePosition(CoordinateSpace.WorldSpace);
                var mw = new Vector2D<float>(mouse.X, mouse.Y);
                session.MoveTargetWorld = ClampToPlayfield(mw);
                session.HasMoveTarget = true;
            }
        }
    }

    private static bool HitUnit(World world, EntityId unit, Vector2D<float> worldPoint)
    {
        ref readonly var t = ref world.Get<Transform>(unit);
        ref readonly var spr = ref world.Get<Sprite>(unit);
        var p = t.WorldPosition;
        var h = spr.HalfExtents;
        return MathF.Abs(worldPoint.X - p.X) <= h.X && MathF.Abs(worldPoint.Y - p.Y) <= h.Y;
    }

    private static Vector2D<float> ClampToPlayfield(Vector2D<float> p)
    {
        var s = RtsConstants.PlaySize;
        return new Vector2D<float>(Math.Clamp(p.X, 0f, s), Math.Clamp(p.Y, 0f, s));
    }
}
