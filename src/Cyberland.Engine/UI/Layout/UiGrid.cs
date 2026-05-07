using System;
using System.Collections.Generic;
using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Layout;

/// <summary>
/// Fixed column-count grid: uniform column widths, row heights from the tallest cell per row, row-major cells.
/// </summary>
public class UiGrid : UiElement
{
    private readonly List<UiElement> _visibleChildrenScratch = new();
    private float[] _rowHeightsScratch = Array.Empty<float>();

    /// <summary>Number of columns (≥ 1).</summary>
    public int ColumnCount
    {
        get => _columnCount;
        set
        {
            var next = Math.Max(1, value);
            if (_columnCount == next)
                return;
            _columnCount = next;
            InvalidateLayout();
        }
    }

    /// <summary>Gap between columns and between rows (pixels).</summary>
    public float Spacing
    {
        get => _spacing;
        set
        {
            if (MathF.Abs(_spacing - value) <= 1e-4f)
                return;
            _spacing = value;
            InvalidateLayout();
        }
    }

    private int _columnCount = 1;
    private float _spacing;

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        var innerMaxW = constraints.MaxWidth - Padding.Horizontal - Margin.Horizontal;
        var innerMaxH = ClampInnerMaxHeightForBand(this,
            constraints.MaxHeight - Padding.Vertical - Margin.Vertical);

        var cols = ColumnCount;
        var gapTotalW = Spacing * Math.Max(0, cols - 1);
        var colW = cols > 0 ? MathF.Max(0f, (innerMaxW - gapTotalW) / cols) : 0f;

        var visible = BuildVisibleChildren();

        var visibleCount = visible.Count;
        var rows = visibleCount == 0 ? 0 : (visibleCount + cols - 1) / cols;

        float totalH = 0f;
        var firstRow = true;
        for (var r = 0; r < rows; r++)
        {
            if (!firstRow)
                totalH += Spacing;
            firstRow = false;

            float rowH = 0f;
            for (var c = 0; c < cols; c++)
            {
                var i = r * cols + c;
                if (i >= visibleCount)
                    break;

                var child = visible[i];
                var cellInnerW = MathF.Max(0f, colW - child.Margin.Horizontal);
                var cc = UiSizeConstraints.Loose(cellInnerW, innerMaxH);
                child.Measure(cc);
                rowH = MathF.Max(rowH, child.MeasuredSize.Y + child.Margin.Vertical);
            }

            totalH += rowH;
        }

        totalH = MathF.Min(totalH, innerMaxH);

        var dw = innerMaxW + Padding.Horizontal + Margin.Horizontal;
        var dh = totalH + Padding.Vertical + Margin.Vertical;
        return constraints.ClampSize(new Vector2D<float>(dw, dh));
    }

    /// <inheritdoc />
    public override void Arrange(in UiRect allocationMarginBoxAbsolute)
    {
        base.Arrange(allocationMarginBoxAbsolute);
        if (!Visible)
            return;

        var inner = ComputedBounds.Deflate(Padding);
        var cols = ColumnCount;
        var gapTotalW = Spacing * Math.Max(0, cols - 1);
        var colW = cols > 0 ? MathF.Max(0f, (inner.Width - gapTotalW) / cols) : 0f;

        var visible = BuildVisibleChildren();
        var visibleCount = visible.Count;

        var rows = visibleCount == 0 ? 0 : (visibleCount + cols - 1) / cols;
        EnsureRowHeightsCapacity(rows);

        for (var r = 0; r < rows; r++)
        {
            float rowH = 0f;
            for (var c = 0; c < cols; c++)
            {
                var i = r * cols + c;
                if (i >= visibleCount)
                    break;

                var child = visible[i];
                rowH = MathF.Max(rowH, child.MeasuredSize.Y + child.Margin.Vertical);
            }

            _rowHeightsScratch[r] = rowH;
        }

        float y = inner.Y;
        var firstRow = true;
        for (var r = 0; r < rows; r++)
        {
            if (!firstRow)
                y += Spacing;
            firstRow = false;

            var rowH = _rowHeightsScratch[r];
            float x = inner.X;
            for (var c = 0; c < cols; c++)
            {
                if (c > 0)
                    x += Spacing;

                var i = r * cols + c;
                if (i >= visibleCount)
                    break;

                var child = visible[i];
                var slot = new UiRect(x, y, colW, rowH);
                child.Arrange(slot);
                x += colW;
            }

            y += rowH;
        }
    }

    private List<UiElement> BuildVisibleChildren()
    {
        var visible = _visibleChildrenScratch;
        visible.Clear();
        var children = Children;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.Visible)
                visible.Add(child);
        }

        return visible;
    }

    private void EnsureRowHeightsCapacity(int rows)
    {
        if (_rowHeightsScratch.Length >= rows)
            return;

        var replacement = new float[Math.Max(4, rows)];
        if (_rowHeightsScratch.Length > 0)
            Array.Copy(_rowHeightsScratch, replacement, _rowHeightsScratch.Length);
        _rowHeightsScratch = replacement;
    }
}
