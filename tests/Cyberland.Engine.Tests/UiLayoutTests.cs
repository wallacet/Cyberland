using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Layout;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class UiLayoutTests
{
    /// <summary>Fixed width, vertical stretch — fills cross-axis slot in <see cref="UiHorizontalStack"/>.</summary>
    private static void StretchHeightFixedWidth(UiElement e, float width)
    {
        e.AnchorMin = new Vector2D<float>(0f, 0f);
        e.AnchorMax = new Vector2D<float>(0f, 1f);
        e.Pivot = default;
        e.AnchoredPosition = default;
        e.SizeDelta = new Vector2D<float>(width, 0f);
        e.StretchLeft = e.StretchRight = e.StretchTop = e.StretchBottom = 0f;
    }

    [Fact]
    public void UiVerticalStack_behaves_like_column_panel()
    {
        var stack = new UiVerticalStack { Spacing = 2f };
        UiLayoutPresets.StretchAll(stack);
        var a = stack.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 5f, 10f);
        var b = stack.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(b, 7f, 8f);

        stack.Measure(UiSizeConstraints.Loose(100f, 200f));
        stack.Arrange(new UiRect(0f, 0f, 100f, 200f));

        Assert.Equal(0f, a.ComputedBounds.Y);
        Assert.Equal(10f + 2f, b.ComputedBounds.Y);
    }

    [Fact]
    public void UiHorizontalStack_TopStretch_nested_column_fills_row_slot_for_text_and_hits()
    {
        var row = new UiHorizontalStack { Spacing = 4f, CrossAlignment = UiCrossAlignment.Stretch };
        UiLayoutPresets.StretchAll(row);
        var stripe = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(stripe, 6f, 40f);
        var titles = row.AddChild(new UiVerticalStack());
        UiLayoutPresets.TopStretch(titles, 40f);
        var name = titles.AddChild(new UiElement());
        UiLayoutPresets.TopStretch(name, 14f);
        var desc = titles.AddChild(new UiElement());
        UiLayoutPresets.TopStretch(desc, 12f);

        row.Measure(UiSizeConstraints.Loose(400f, 50f));
        row.Arrange(new UiRect(0f, 0f, 400f, 50f));

        Assert.True(titles.ComputedBounds.Width > 64f, "nested column should map stack slot width, not 0");
        Assert.True(titles.ComputedBounds.Height > 1f);
    }

    [Fact]
    public void UiHorizontalStack_Start_places_left_to_right()
    {
        var row = new UiHorizontalStack { Spacing = 4f, CrossAlignment = UiCrossAlignment.Start };
        UiLayoutPresets.StretchAll(row);
        var a = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 20f, 10f);
        var b = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(b, 30f, 12f);

        row.Measure(UiSizeConstraints.Loose(500f, 80f));
        row.Arrange(new UiRect(0f, 0f, 500f, 80f));

        Assert.Equal(0f, a.ComputedBounds.X);
        Assert.Equal(20f + 4f, b.ComputedBounds.X);
        Assert.Equal(0f, a.ComputedBounds.Y);
        Assert.Equal(0f, b.ComputedBounds.Y);
    }

    [Fact]
    public void UiHorizontalStack_End_aligns_cross_axis()
    {
        var row = new UiHorizontalStack { CrossAlignment = UiCrossAlignment.End };
        UiLayoutPresets.StretchAll(row);
        var a = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 10f, 20f);

        row.Measure(UiSizeConstraints.Loose(200f, 100f));
        row.Arrange(new UiRect(0f, 0f, 200f, 100f));

        Assert.Equal(80f, a.ComputedBounds.Y);
    }

    [Fact]
    public void UiHorizontalStack_Center_aligns_cross_axis()
    {
        var row = new UiHorizontalStack { CrossAlignment = UiCrossAlignment.Center };
        UiLayoutPresets.StretchAll(row);
        var a = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 10f, 20f);

        row.Measure(UiSizeConstraints.Loose(200f, 100f));
        row.Arrange(new UiRect(0f, 0f, 200f, 100f));

        Assert.Equal(40f, a.ComputedBounds.Y);
    }

    [Fact]
    public void UiHorizontalStack_Stretch_uses_full_track_height()
    {
        var row = new UiHorizontalStack { CrossAlignment = UiCrossAlignment.Stretch };
        UiLayoutPresets.StretchAll(row);
        var a = row.AddChild(new UiElement());
        StretchHeightFixedWidth(a, 10f);

        row.Measure(UiSizeConstraints.Loose(200f, 100f));
        row.Arrange(new UiRect(0f, 0f, 200f, 100f));

        Assert.Equal(0f, a.ComputedBounds.Y);
        Assert.Equal(100f, a.ComputedBounds.Height);
    }

    [Fact]
    public void UiHorizontalStack_unknown_cross_alignment_falls_through_to_stretch_track()
    {
        var row = new UiHorizontalStack { CrossAlignment = (UiCrossAlignment)999 };
        UiLayoutPresets.StretchAll(row);
        var a = row.AddChild(new UiElement());
        StretchHeightFixedWidth(a, 10f);

        row.Measure(UiSizeConstraints.Loose(200f, 100f));
        row.Arrange(new UiRect(0f, 0f, 200f, 100f));

        Assert.Equal(100f, a.ComputedBounds.Height);
    }

    [Fact]
    public void UiGrid_row_major_uniform_columns_and_spacing()
    {
        var grid = new UiGrid { ColumnCount = 2, Spacing = 4f };
        UiLayoutPresets.StretchAll(grid);

        var a = grid.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 10f, 10f);
        var b = grid.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(b, 10f, 15f);
        var c = grid.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(c, 10f, 12f);

        grid.Measure(UiSizeConstraints.Loose(200f, 300f));
        grid.Arrange(new UiRect(0f, 0f, 200f, 300f));

        var colW = (200f - 4f) * 0.5f;
        Assert.Equal(0f, a.ComputedBounds.X);
        Assert.Equal(colW + 4f, b.ComputedBounds.X);
        Assert.Equal(0f, c.ComputedBounds.X);
        Assert.Equal(15f + 4f, c.ComputedBounds.Y);
    }

    [Fact]
    public void UiGrid_ColumnCount_clamps_to_at_least_one()
    {
        var grid = new UiGrid { ColumnCount = 0 };
        Assert.Equal(1, grid.ColumnCount);
    }

    [Fact]
    public void UiGrid_property_changes_invalidate_layout()
    {
        var doc = new UiDocument();
        var grid = doc.Root.AddChild(new UiGrid { ColumnCount = 1, Spacing = 0f });
        UiLayoutPresets.StretchAll(grid);
        var a = grid.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 20f, 10f);
        var b = grid.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(b, 20f, 10f);

        doc.MeasureArrange(new Vector2D<float>(100f, 40f));
        Assert.Equal(0f, b.ComputedBounds.X);

        grid.ColumnCount = 2;
        grid.Spacing = 4f;
        doc.MeasureArrange(new Vector2D<float>(100f, 40f));
        Assert.True(b.ComputedBounds.X > 0f);
    }

    [Fact]
    public void UiGrid_when_invisible_skips_child_arrange_slots()
    {
        var grid = new UiGrid { Visible = false, ColumnCount = 2 };
        UiLayoutPresets.StretchAll(grid);
        grid.AddChild(new UiElement());
        grid.Measure(UiSizeConstraints.Loose(50f, 50f));
        grid.Arrange(new UiRect(0f, 0f, 50f, 50f));
        Assert.Equal(default(UiRect), grid.ComputedBounds);
    }

    [Fact]
    public void UiGrid_row_height_scratch_can_grow_multiple_times()
    {
        var grid = new UiGrid { ColumnCount = 1, Spacing = 1f };
        UiLayoutPresets.StretchAll(grid);

        for (var i = 0; i < 5; i++)
        {
            var cell = grid.AddChild(new UiElement());
            UiLayoutPresets.TopLeftFixed(cell, 20f, 10f + i);
        }

        grid.Measure(UiSizeConstraints.Loose(80f, 400f));
        grid.Arrange(new UiRect(0f, 0f, 80f, 400f));

        for (var i = 0; i < 12; i++)
        {
            var cell = grid.AddChild(new UiElement());
            UiLayoutPresets.TopLeftFixed(cell, 20f, 8f);
        }

        grid.Measure(UiSizeConstraints.Loose(80f, 400f));
        grid.Arrange(new UiRect(0f, 0f, 80f, 400f));
        Assert.True(grid.Children.Count > 10);
    }

    [Fact]
    public void UiGrid_row_height_scratch_reuse_returns_when_capacity_is_sufficient()
    {
        var grid = new UiGrid { ColumnCount = 1 };
        UiLayoutPresets.StretchAll(grid);
        for (var i = 0; i < 6; i++)
        {
            var cell = grid.AddChild(new UiElement());
            UiLayoutPresets.TopLeftFixed(cell, 10f, 10f);
        }

        grid.Measure(UiSizeConstraints.Loose(100f, 300f));
        grid.Arrange(new UiRect(0f, 0f, 100f, 300f));
        // Same row count on the second arrange path should hit the early return capacity branch.
        grid.Measure(UiSizeConstraints.Loose(100f, 300f));
        grid.Arrange(new UiRect(0f, 0f, 100f, 300f));
        Assert.Equal(6, grid.Children.Count);
    }

    [Fact]
    public void Nested_stack_in_stretched_slot_sizes_within_parent()
    {
        var doc = new UiDocument();
        var row = doc.Root.AddChild(new UiHorizontalStack());
        UiLayoutPresets.StretchAll(row);
        var inner = row.AddChild(new UiVerticalStack());
        UiLayoutPresets.TopLeftFixed(inner, 40f, 50f);

        doc.MeasureArrange(new Vector2D<float>(300f, 120f));

        Assert.True(inner.ComputedBounds.Width <= 300f);
        Assert.True(inner.ComputedBounds.Height <= 120f);
    }

    [Fact]
    public void UiHorizontalStack_measure_continue_when_child_invisible()
    {
        var row = new UiHorizontalStack();
        UiLayoutPresets.StretchAll(row);
        row.AddChild(new UiElement { Visible = false });
        var a = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 12f, 8f);

        row.Measure(UiSizeConstraints.Loose(80f, 40f));
        Assert.Equal(12f, row.MeasuredSize.X);
    }

    [Fact]
    public void UiHorizontalStack_Stretch_cross_without_visible_children_leaves_zero_cross()
    {
        var row = new UiHorizontalStack { CrossAlignment = UiCrossAlignment.Stretch };
        UiLayoutPresets.StretchAll(row);
        row.AddChild(new UiElement { Visible = false });

        row.Measure(UiSizeConstraints.Loose(120f, 60f));
        Assert.Equal(0f, row.MeasuredSize.Y);
    }

    [Fact]
    public void UiHorizontalStack_when_invisible_returns_before_child_arrange()
    {
        var row = new UiHorizontalStack { Visible = false };
        UiLayoutPresets.StretchAll(row);
        row.AddChild(new UiElement());
        row.Measure(UiSizeConstraints.Loose(40f, 40f));
        row.Arrange(new UiRect(0f, 0f, 40f, 40f));
        Assert.Equal(default(UiRect), row.ComputedBounds);
    }

    [Fact]
    public void UiHorizontalStack_TopStretch_clamps_cross_axis_when_fixed_button_contains_stretch_all_label()
    {
        var nav = new UiHorizontalStack { Spacing = 8f };
        UiLayoutPresets.TopStretch(nav, 38f);

        var btn = new UiButton();
        UiLayoutPresets.TopLeftFixed(btn, 148f, 34f);
        var lab = new UiLabel();
        UiLayoutPresets.StretchAll(lab);
        lab.Text.Text = "Gather";
        lab.Text.Fonts = Fonts();
        btn.AddChild(lab);
        nav.AddChild(btn);

        nav.Measure(UiSizeConstraints.Loose(800f, 600f));

        Assert.InRange(nav.MeasuredSize.Y, 34f, 42f);
    }

    private static FontLibrary Fonts()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        return lib;
    }

    [Fact]
    public void UiHorizontalStack_arrange_continue_skips_invisible_child_slot()
    {
        var row = new UiHorizontalStack { Spacing = 3f };
        UiLayoutPresets.StretchAll(row);
        var a = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 10f, 10f);
        row.AddChild(new UiElement { Visible = false });
        var b = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(b, 20f, 10f);

        row.Measure(UiSizeConstraints.Loose(200f, 50f));
        row.Arrange(new UiRect(0f, 0f, 200f, 50f));

        Assert.Equal(10f + 3f, b.ComputedBounds.X);
    }

    [Fact]
    public void UiHorizontalStack_spacing_change_invalidates_layout()
    {
        var doc = new UiDocument();
        var row = doc.Root.AddChild(new UiHorizontalStack { Spacing = 0f });
        UiLayoutPresets.StretchAll(row);
        var a = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(a, 10f, 10f);
        var b = row.AddChild(new UiElement());
        UiLayoutPresets.TopLeftFixed(b, 10f, 10f);

        doc.MeasureArrange(new Vector2D<float>(80f, 30f));
        Assert.Equal(10f, b.ComputedBounds.X);

        row.Spacing = 7f;
        doc.MeasureArrange(new Vector2D<float>(80f, 30f));
        Assert.Equal(17f, b.ComputedBounds.X);
    }
}
