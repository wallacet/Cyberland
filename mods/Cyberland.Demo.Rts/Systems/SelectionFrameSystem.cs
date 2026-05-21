using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Late: marquee drag rectangle or AABB around selected units using four selection-bar sprites.</summary>
public sealed class SelectionFrameSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SessionState>();

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _ = sessionRow;
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity sessionRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref readonly var session = ref sessionRow.Get<SessionState>();
        var world = sessionRow.World;

        var top = session.SelectionBar0;
        var bottom = session.SelectionBar1;
        var left = session.SelectionBar2;
        var right = session.SelectionBar3;

        if (session.BoxDragActive)
        {
            LayoutSelectionRect(
                world,
                top,
                bottom,
                left,
                right,
                session.BoxDragStartWorld,
                session.BoxDragEndWorld,
                marquee: true);
            return;
        }

        if (!TryComputeSelectedBounds(world, out var minX, out var minY, out var maxX, out var maxY))
        {
            HideBars(world, top, bottom, left, right);
            return;
        }

        var pad = Constants.SelectionPadding;
        var cx = (minX + maxX) * 0.5f;
        var cy = (minY + maxY) * 0.5f;
        var halfW = (maxX - minX) * 0.5f + pad;
        var halfH = (maxY - minY) * 0.5f + pad;
        var ph = pad * 0.5f;

        LayoutBar(world, top, new Vector2D<float>(cx, cy + halfH + ph), new Vector2D<float>(halfW, ph));
        LayoutBar(world, bottom, new Vector2D<float>(cx, cy - halfH - ph), new Vector2D<float>(halfW, ph));
        LayoutBar(world, left, new Vector2D<float>(cx - halfW - ph, cy), new Vector2D<float>(ph, halfH + pad));
        LayoutBar(world, right, new Vector2D<float>(cx + halfW + ph, cy), new Vector2D<float>(ph, halfH + pad));
    }

    private static void LayoutSelectionRect(
        World world,
        EntityId top,
        EntityId bottom,
        EntityId left,
        EntityId right,
        Vector2D<float> a,
        Vector2D<float> b,
        bool marquee)
    {
        var minX = MathF.Min(a.X, b.X);
        var maxX = MathF.Max(a.X, b.X);
        var minY = MathF.Min(a.Y, b.Y);
        var maxY = MathF.Max(a.Y, b.Y);
        var cx = (minX + maxX) * 0.5f;
        var cy = (minY + maxY) * 0.5f;
        var halfW = MathF.Max(1f, (maxX - minX) * 0.5f);
        var halfH = MathF.Max(1f, (maxY - minY) * 0.5f);
        var ph = 2f;

        var color = marquee
            ? new Vector4D<float>(0.35f, 0.75f, 1f, 0.85f)
            : new Vector4D<float>(0.2f, 1f, 0.35f, 0.95f);

        LayoutBar(world, top, new Vector2D<float>(cx, cy + halfH + ph), new Vector2D<float>(halfW, ph), color);
        LayoutBar(world, bottom, new Vector2D<float>(cx, cy - halfH - ph), new Vector2D<float>(halfW, ph), color);
        LayoutBar(world, left, new Vector2D<float>(cx - halfW - ph, cy), new Vector2D<float>(ph, halfH + ph * 2f), color);
        LayoutBar(world, right, new Vector2D<float>(cx + halfW + ph, cy), new Vector2D<float>(ph, halfH + ph * 2f), color);
    }

    private static bool TryComputeSelectedBounds(
        World world,
        out float minX,
        out float minY,
        out float maxX,
        out float maxY)
    {
        minX = float.PositiveInfinity;
        minY = float.PositiveInfinity;
        maxX = float.NegativeInfinity;
        maxY = float.NegativeInfinity;
        var any = false;

        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<UnitTag, Transform, Sprite, UnitState>()))
        {
            var transforms = chunk.Column<Transform>();
            var sprites = chunk.Column<Sprite>();
            var states = chunk.Column<UnitState>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (!states[i].Selected)
                    continue;

                var p = transforms[i].WorldPosition;
                var h = sprites[i].HalfExtents;
                minX = MathF.Min(minX, p.X - h.X);
                minY = MathF.Min(minY, p.Y - h.Y);
                maxX = MathF.Max(maxX, p.X + h.X);
                maxY = MathF.Max(maxY, p.Y + h.Y);
                any = true;
            }
        }

        return any;
    }

    private static void HideBars(World world, EntityId top, EntityId bottom, EntityId left, EntityId right)
    {
        world.Get<Sprite>(top).Visible = false;
        world.Get<Sprite>(bottom).Visible = false;
        world.Get<Sprite>(left).Visible = false;
        world.Get<Sprite>(right).Visible = false;
    }

    private static void LayoutBar(
        World world,
        EntityId bar,
        Vector2D<float> center,
        Vector2D<float> halfExtents,
        Vector4D<float>? color = null)
    {
        ref var t = ref world.Get<Transform>(bar);
        t.WorldPosition = center;
        ref var spr = ref world.Get<Sprite>(bar);
        spr.HalfExtents = halfExtents;
        spr.Visible = true;
        if (color.HasValue)
            spr.ColorMultiply = color.Value;
    }
}
