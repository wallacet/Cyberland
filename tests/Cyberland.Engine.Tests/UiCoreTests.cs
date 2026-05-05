using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class UiCoreTests
{
    [Fact]
    public void UiRect_Deflate_shrinks_by_thickness()
    {
        var r = new UiRect(10f, 20f, 100f, 50f);
        var d = r.Deflate(new UiThickness(5f, 7f, 5f, 8f));
        Assert.Equal(15f, d.X);
        Assert.Equal(27f, d.Y);
        Assert.Equal(90f, d.Width);
        Assert.Equal(35f, d.Height);
    }

    [Fact]
    public void UiAnchorLayout_horizontal_stretch_vertical_collapsed_sizes_height_from_SizeDelta()
    {
        var slot = new UiRect(0f, 0f, 400f, 300f);
        var r = UiAnchorLayout.ResolveBounds(
            slot,
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1f, 0f),
            new Vector2D<float>(0f, 0f),
            default,
            new Vector2D<float>(0f, 40f),
            0f, 0f, 0f, 0f);
        Assert.Equal(0f, r.X);
        Assert.Equal(0f, r.Y);
        Assert.Equal(400f, r.Width);
        Assert.Equal(40f, r.Height);
    }

    [Fact]
    public void UiLayoutPresets_TopRightFixed_places_panel_with_margin()
    {
        var root = new UiPanel();
        UiLayoutPresets.StretchAll(root);
        UiLayoutPresets.TopRightFixed(root, width: 280f, height: 120f, margin: 16f);
        root.Measure(UiSizeConstraints.Loose(800f, 600f));
        root.Arrange(new UiRect(0f, 0f, 800f, 600f));

        Assert.Equal(504f, root.ComputedBounds.X);
        Assert.Equal(16f, root.ComputedBounds.Y);
        Assert.Equal(280f, root.ComputedBounds.Width);
        Assert.Equal(120f, root.ComputedBounds.Height);
    }

    [Fact]
    public void UiLayoutPresets_CenterFixed_centers_in_parent()
    {
        var root = new UiPanel();
        UiLayoutPresets.StretchAll(root);
        UiLayoutPresets.CenterFixed(root, width: 100f, height: 50f);
        root.Measure(UiSizeConstraints.Loose(400f, 300f));
        root.Arrange(new UiRect(0f, 0f, 400f, 300f));

        Assert.Equal(150f, root.ComputedBounds.X);
        Assert.Equal(125f, root.ComputedBounds.Y);
        Assert.Equal(100f, root.ComputedBounds.Width);
        Assert.Equal(50f, root.ComputedBounds.Height);
    }

    [Fact]
    public void UiPanel_vertical_stack_positions_children_with_spacing_and_padding()
    {
        var panel = new UiPanel { Padding = new UiThickness(4f), Spacing = 6f };
        UiLayoutPresets.StretchAll(panel);

        var a = panel.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 10f, 20f);
        var b = panel.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(b, 30f, 15f);

        panel.Measure(UiSizeConstraints.Loose(200f, 500f));
        panel.Arrange(new UiRect(0f, 0f, 200f, 500f));

        Assert.Equal(10f, a.ComputedBounds.Width);
        Assert.Equal(20f, a.ComputedBounds.Height);
        Assert.Equal(4f, a.ComputedBounds.X);
        Assert.Equal(4f, a.ComputedBounds.Y);

        Assert.Equal(30f, b.ComputedBounds.Width);
        Assert.Equal(15f, b.ComputedBounds.Height);
        Assert.Equal(4f, b.ComputedBounds.X);
        Assert.Equal(4f + 20f + 6f, b.ComputedBounds.Y);
    }

    [Fact]
    public void UiDocument_MeasureArrange_and_Draw_collects_debug_rects()
    {
        var doc = new UiDocument();
        var child = doc.Root.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(child, 32f, 18f);

        doc.MeasureArrange(new Vector2D<float>(640f, 480f));
        var ctx = new UiRenderContext(new UiRect(0f, 0f, 640f, 480f));
        doc.Draw(ctx);

        Assert.Contains(ctx.DebugRects, r => r.Width > 600f && r.Height > 400f);
        Assert.Contains(ctx.DebugRects, r => Math.Abs(r.Width - 32f) < 0.01f && Math.Abs(r.Height - 18f) < 0.01f);
    }

    [Fact]
    public void UiClipMode_IntersectParent_clips_child_recorded_rect()
    {
        var doc = new UiDocument();
        doc.Root.ClipMode = UiClipMode.IntersectParent;
        var child = doc.Root.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(child, 400f, 400f);

        doc.MeasureArrange(new Vector2D<float>(100f, 100f));
        var ctx = new UiRenderContext(new UiRect(0f, 0f, 100f, 100f));
        doc.Draw(ctx);

        var nonRoot = ctx.DebugRects.Where(r => r.Width < 150f && r.Height < 150f).ToList();
        Assert.All(nonRoot, r => Assert.True(r.Width <= 100f && r.Height <= 100f));
    }

    [Fact]
    public void UiRenderContext_PopClip_without_push_throws()
    {
        var ctx = new UiRenderContext(new UiRect(0f, 0f, 10f, 10f));
        Assert.Throws<InvalidOperationException>(() => ctx.PopClip());
    }

    [Fact]
    public void UiElement_Visible_false_skips_layout_and_draw()
    {
        var panel = new UiPanel();
        UiLayoutPresets.StretchAll(panel);
        var hidden = panel.AddChild(new UiElement { Visible = false });
        UiLayoutPresets.TopLeftFixed(hidden, 50f, 50f);

        panel.Measure(UiSizeConstraints.Loose(100f, 100f));
        panel.Arrange(new UiRect(0f, 0f, 100f, 100f));

        Assert.Equal(0f, hidden.MeasuredSize.X);
        Assert.Equal(default(UiRect), hidden.ComputedBounds);

        var ctx = new UiRenderContext(new UiRect(0f, 0f, 100f, 100f));
        panel.Draw(ctx);
        Assert.DoesNotContain(ctx.DebugRects, r => Math.Abs(r.Width - 50f) < 0.01f);
    }

    [Fact]
    public void UiElement_AddChild_reparents_and_RemoveChild_clears_parent()
    {
        var a = new UiPanel();
        var b = new UiPanel();
        var leaf = new UiElement();
        a.AddChild(leaf);
        Assert.Same(a, leaf.Parent);
        b.AddChild(leaf);
        Assert.Same(b, leaf.Parent);
        Assert.DoesNotContain(a.Children, c => ReferenceEquals(c, leaf));

        b.RemoveChild(leaf);
        Assert.Null(leaf.Parent);
        b.RemoveChild(leaf);
    }

    [Fact]
    public void UiElement_RemoveChild_wrong_parent_is_noop()
    {
        var a = new UiPanel();
        var b = new UiPanel();
        var leaf = new UiElement();
        a.AddChild(leaf);
        b.RemoveChild(leaf);
        Assert.Same(a, leaf.Parent);
    }

    [Fact]
    public void UiElement_Measure_stretched_axes_follow_constraints()
    {
        var e = new UiElement();
        UiLayoutPresets.StretchAll(e);
        var s = e.Measure(UiSizeConstraints.Loose(222f, 333f));
        Assert.Equal(222f, s.X);
        Assert.Equal(333f, s.Y);
    }

    [Fact]
    public void UiLayoutPresets_BottomRightFixed_offsets_from_corner()
    {
        var root = new UiPanel();
        UiLayoutPresets.StretchAll(root);
        UiLayoutPresets.BottomRightFixed(root, width: 40f, height: 20f, margin: 8f);
        root.Measure(UiSizeConstraints.Loose(100f, 100f));
        root.Arrange(new UiRect(0f, 0f, 100f, 100f));

        Assert.Equal(52f, root.ComputedBounds.X);
        Assert.Equal(72f, root.ComputedBounds.Y);
        Assert.Equal(40f, root.ComputedBounds.Width);
        Assert.Equal(20f, root.ComputedBounds.Height);
    }

    [Fact]
    public void UiLayoutPresets_TopStretch_sets_full_width_band()
    {
        var root = new UiPanel();
        UiLayoutPresets.StretchAll(root);
        UiLayoutPresets.TopStretch(root, height: 22f);
        root.Measure(UiSizeConstraints.Loose(300f, 400f));
        root.Arrange(new UiRect(0f, 0f, 300f, 400f));
        Assert.Equal(300f, root.ComputedBounds.Width);
        Assert.Equal(22f, root.ComputedBounds.Height);
    }

    [Fact]
    public void UiPanel_AddChild_same_parent_no_duplicate()
    {
        var p = new UiPanel();
        var c = new UiElement();
        p.AddChild(c);
        p.AddChild(c);
        Assert.Single(p.Children);
    }

    [Fact]
    public void UiElement_Measure_and_Arrange_when_invisible_sets_zeros()
    {
        var e = new UiElement { Visible = false };
        UiLayoutPresets.TopLeftFixed(e, 10f, 10f);
        e.Measure(UiSizeConstraints.Loose(100f, 100f));
        e.Arrange(new UiRect(0f, 0f, 100f, 100f));
        Assert.Equal(0f, e.MeasuredSize.X);
        Assert.Equal(default(UiRect), e.ComputedBounds);
    }

    [Fact]
    public void UiPanel_Arrange_when_invisible_returns_early_after_base()
    {
        var p = new UiPanel { Visible = false };
        UiLayoutPresets.StretchAll(p);
        p.AddChild(new UiElement());
        p.Measure(UiSizeConstraints.Loose(50f, 50f));
        p.Arrange(new UiRect(0f, 0f, 50f, 50f));
        Assert.Equal(default(UiRect), p.ComputedBounds);
    }

    [Fact]
    public void UiRect_Center_Contains_and_equality_surface()
    {
        var r = new UiRect(10f, 20f, 100f, 40f);
        Assert.Equal(new Vector2D<float>(60f, 40f), r.Center);
        Assert.True(r.Contains(new Vector2D<float>(10f, 20f)));
        Assert.True(r.Contains(new Vector2D<float>(110f, 60f)));
        Assert.False(r.Contains(new Vector2D<float>(9f, 30f)));
        Assert.False(r.Contains(new Vector2D<float>(111f, 30f)));
        Assert.False(r.Contains(new Vector2D<float>(50f, 19f)));
        Assert.False(r.Contains(new Vector2D<float>(50f, 61f)));

        var same = new UiRect(10f, 20f, 100f, 40f);
        Assert.True(r.Equals(same));
        Assert.False(r.Equals(new UiRect(11f, 20f, 100f, 40f)));
        Assert.False(r.Equals(new UiRect(10f, 21f, 100f, 40f)));
        Assert.False(r.Equals(new UiRect(10f, 20f, 101f, 40f)));
        Assert.False(r.Equals(new UiRect(10f, 20f, 100f, 41f)));

        Assert.True(r.Equals((object)same));
        Assert.False(r.Equals((object)"nope"));
        Assert.False(r.Equals(null));

        Assert.Equal(same.GetHashCode(), r.GetHashCode());
        Assert.True(r == same);
        Assert.False(r != same);
    }

    [Fact]
    public void UiThickness_constructors_and_equality_surface()
    {
        var u = new UiThickness(2f);
        var hv = new UiThickness(3f, 4f);
        Assert.Equal(3f, hv.Left);
        Assert.Equal(4f, hv.Top);

        var full = new UiThickness(1f, 2f, 3f, 4f);
        Assert.True(full.Equals(full));
        Assert.False(full.Equals(new UiThickness(9f, 2f, 3f, 4f)));
        Assert.True(full.Equals((object)new UiThickness(1f, 2f, 3f, 4f)));
        Assert.False(full.Equals((object)u));
        Assert.False(full.Equals(null));

        _ = full.GetHashCode();
        Assert.True(full == new UiThickness(1f, 2f, 3f, 4f));
        Assert.False(full != new UiThickness(1f, 2f, 3f, 4f));
        Assert.Equal(2f, u.Left);
        Assert.Equal(6f, hv.Horizontal);
        Assert.Equal(8f, hv.Vertical);
    }

    [Fact]
    public void UiElement_Draw_null_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UiElement().Draw(null!));
    }

    [Fact]
    public void UiDocument_Draw_null_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UiDocument().Draw(null!));
    }

    [Fact]
    public void UiElement_AddChild_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UiPanel().AddChild<UiElement>(null!));
    }

    [Fact]
    public void UiElement_RemoveChild_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UiPanel().RemoveChild(null!));
    }
}
