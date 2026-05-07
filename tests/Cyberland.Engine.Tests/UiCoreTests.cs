using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Layout;
using Cyberland.Engine.UI.Rendering;
using Cyberland.Engine.UI.Text;
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
    public void UiAnchorLayout_horizontal_stretch_zero_SizeDelta_fills_slot_height()
    {
        var slot = new UiRect(10f, 20f, 400f, 120f);
        var r = UiAnchorLayout.ResolveBounds(
            slot,
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(1f, 0f),
            new Vector2D<float>(0f, 0f),
            default,
            new Vector2D<float>(0f, 0f),
            0f, 0f, 0f, 0f);
        Assert.Equal(10f, r.X);
        Assert.Equal(20f, r.Y);
        Assert.Equal(400f, r.Width);
        Assert.Equal(120f, r.Height);
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
    public void UiPanel_StretchWidthAutoHeight_matches_intrinsic_column_slot_without_fixed_band_gap()
    {
        var col = new UiVerticalStack { Spacing = 8f };
        UiLayoutPresets.StretchAll(col);
        var frame = col.AddChild(new UiPanel { Padding = new UiThickness(1f) });
        UiLayoutPresets.StretchWidthAutoHeight(frame);
        var inner = frame.AddChild(new UiPanel());
        UiLayoutPresets.TopLeftFixed(inner, 40f, 44f);

        col.Measure(UiSizeConstraints.Loose(200f, 400f));
        Assert.True(frame.MeasuredSize.Y < 90f, "intrinsic height must not reserve a huge fixed band");

        var tail = col.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(tail, 10f, 10f);
        col.Measure(UiSizeConstraints.Loose(200f, 400f));
        col.Arrange(new UiRect(0f, 0f, 200f, 400f));

        Assert.True(tail.ComputedBounds.Y >= frame.ComputedBounds.Bottom + 8f - 0.01f);
    }

    [Fact]
    public void UiPanel_TopLeftFixed_horizontal_measure_reserves_SizeDelta_width_over_narrow_children()
    {
        var row = new UiHorizontalStack { Spacing = 12f };
        UiLayoutPresets.StretchAll(row);
        var tile = new UiPanel();
        UiLayoutPresets.TopLeftFixed(tile, 90f, 28f);
        var inner = new UiPanel();
        UiLayoutPresets.TopLeftFixed(inner, 12f, 12f);
        tile.AddChild(inner);
        row.AddChild(tile);

        row.Measure(UiSizeConstraints.Loose(400f, 80f));
        Assert.True(tile.MeasuredSize.X >= 89f, "tile measure must reserve TopLeftFixed width");
        Assert.True(row.MeasuredSize.X >= 90f - 0.01f);
    }

    [Fact]
    public void UiPanel_fixed_height_caps_vertical_measure_for_stretch_labels()
    {
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);

        var row = new UiHorizontalStack { CrossAlignment = UiCrossAlignment.Stretch };
        UiLayoutPresets.StretchAll(row);
        var btn = new UiPanel();
        UiLayoutPresets.TopLeftFixed(btn, 120f, 36f);
        var lab = new UiLabel();
        lab.Text.Fonts = fonts;
        lab.Text.Text = "Caption";
        lab.Text.DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(1f, 1f, 1f, 1f));
        btn.AddChild(lab);

        row.AddChild(btn);
        row.Measure(UiSizeConstraints.Loose(400f, 72f));

        Assert.InRange(btn.MeasuredSize.Y, 35.5f, 37f);
    }

    [Fact]
    public void UiPanel_TopStretch_fixed_band_uses_SizeDelta_height_for_stack_spacing_not_intrinsic_sum()
    {
        var col = new UiVerticalStack { Spacing = 8f };
        UiLayoutPresets.StretchAll(col);
        var frame = col.AddChild(new UiPanel { Padding = new UiThickness(1f) });
        UiLayoutPresets.TopStretch(frame, 100f);
        var inner = frame.AddChild(new UiPanel());
        UiLayoutPresets.TopLeftFixed(inner, 40f, 40f);

        col.Measure(UiSizeConstraints.Loose(200f, 400f));
        Assert.Equal(100f, frame.MeasuredSize.Y, 0.01f);

        var tail = col.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(tail, 10f, 10f);
        col.Measure(UiSizeConstraints.Loose(200f, 400f));
        col.Arrange(new UiRect(0f, 0f, 200f, 400f));

        Assert.True(tail.ComputedBounds.Y >= frame.ComputedBounds.Bottom + 8f - 0.01f);
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
    public void UiPanel_vertical_stack_stretch_child_measures_to_remaining_height_below_fixed_rows()
    {
        var panel = new UiPanel { Spacing = 10f };
        UiLayoutPresets.StretchAll(panel);

        var header = panel.AddChild(new UiElement());
        UiLayoutPresets.TopStretch(header, 30f);
        var body = panel.AddChild(new UiElement());
        UiLayoutPresets.StretchAll(body);

        panel.Measure(UiSizeConstraints.Loose(400f, 200f));
        panel.Arrange(new UiRect(0f, 0f, 400f, 200f));

        Assert.Equal(30f, header.MeasuredSize.Y);
        Assert.Equal(160f, body.MeasuredSize.Y);
        Assert.Equal(0f, header.ComputedBounds.Y);
        Assert.Equal(40f, body.ComputedBounds.Y);
        Assert.Equal(160f, body.ComputedBounds.Height);
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

    [Fact]
    public void UiElement_SortedChildren_orders_by_sort_key_then_insertion()
    {
        var p = new UiPanel();
        var a = new UiPanel { SortKey = 2f };
        var b = new UiPanel { SortKey = 1f };
        p.AddChild(a);
        p.AddChild(b);
        var span = p.SortedChildren();
        Assert.Equal(2, span.Length);
        Assert.Same(b, span[0]);
        Assert.Same(a, span[1]);
    }

    [Fact]
    public void UiElement_Draw_visits_children_in_sorted_order()
    {
        var root = new UiPanel();
        UiLayoutPresets.StretchAll(root);

        var late = new UiElement { SortKey = 5f };
        UiLayoutPresets.TopLeftFixed(late, 10f, 10f);
        late.AnchoredPosition = new Vector2D<float>(40f, 0f);

        var early = new UiElement { SortKey = 1f };
        UiLayoutPresets.TopLeftFixed(early, 10f, 10f);

        root.AddChild(late);
        root.AddChild(early);

        root.Measure(UiSizeConstraints.Loose(100f, 100f));
        root.Arrange(new UiRect(0f, 0f, 100f, 100f));

        var ctx = new UiRenderContext(new UiRect(0f, 0f, 100f, 100f));
        root.Draw(ctx);

        // Root records first, then children in traversal order.
        Assert.True(ctx.DebugRects.Count >= 3);
        Assert.Equal(0f, ctx.DebugRects[1].X);
        Assert.Equal(40f, ctx.DebugRects[2].X);
    }

    [Fact]
    public void UiElement_layout_property_setters_coalesce_noop_assignments()
    {
        var p = new UiPanel();
        var m = new UiThickness(2f, 3f, 4f, 5f);
        p.Margin = m;
        p.Margin = m;
        p.Padding = p.Padding;
        p.AnchorMin = p.AnchorMin;
        p.AnchorMax = p.AnchorMax;
        p.Pivot = p.Pivot;
        p.AnchoredPosition = p.AnchoredPosition;
        p.SizeDelta = p.SizeDelta;
        p.StretchLeft = 1f;
        p.StretchLeft = 1f;
        p.StretchRight = 2f;
        p.StretchRight = 2f;
        p.StretchTop = 3f;
        p.StretchTop = 3f;
        p.StretchBottom = 4f;
        p.StretchBottom = 4f;
        p.ClipMode = p.ClipMode;
        p.Visible = true;
        p.SortKey = p.SortKey;
    }

    [Fact]
    public void UiPanel_background_texture_and_spacing_setters_coalesce()
    {
        var p = new UiPanel();
        var c = new Vector4D<float>(1f, 0f, 0f, 0.5f);
        p.BackgroundColor = c;
        p.BackgroundColor = c;
        p.BackgroundTextureId = 7u;
        p.BackgroundTextureId = 7u;
        p.Spacing = 2f;
        p.Spacing = 2f;
    }

    [Fact]
    public void UiDocument_PrepareFontsAndLocalizationIfNeeded_reruns_after_Invalidate()
    {
        var doc = new UiDocument();
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var tb = new UiTextBlock
        {
            Text = "x",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        doc.Root.AddChild(tb);

        doc.PrepareFontsAndLocalizationIfNeeded(fonts, null);
        Assert.Same(fonts, tb.Fonts);

        tb.Fonts = null;
        doc.InvalidateFontsAndLocalization();
        doc.PrepareFontsAndLocalizationIfNeeded(fonts, null);
        Assert.Same(fonts, tb.Fonts);
    }
}
