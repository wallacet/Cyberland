using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Layout;

/// <summary>
/// Fixed column-count grid: uniform column widths, row heights from the tallest cell per row, row-major cells.
/// </summary>
public class UiGrid : UiElement
{
    /// <summary>Number of columns (≥ 1).</summary>
    public int ColumnCount
    {
        get => _columnCount;
        set => _columnCount = Math.Max(1, value);
    }

    /// <summary>Gap between columns and between rows (pixels).</summary>
    public float Spacing { get; set; }

    private int _columnCount = 1;

    /// <inheritdoc />
    protected override Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        var innerMaxW = constraints.MaxWidth - Padding.Horizontal - Margin.Horizontal;
        var innerMaxH = constraints.MaxHeight - Padding.Vertical - Margin.Vertical;

        var cols = ColumnCount;
        var gapTotalW = Spacing * Math.Max(0, cols - 1);
        var colW = cols > 0 ? MathF.Max(0f, (innerMaxW - gapTotalW) / cols) : 0f;

        var visible = new List<UiElement>();
        foreach (var c in Children)
        {
            if (c.Visible)
                visible.Add(c);
        }

        var rows = visible.Count == 0 ? 0 : (visible.Count + cols - 1) / cols;

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
                if (i >= visible.Count)
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

        var visible = new List<UiElement>();
        foreach (var c in Children)
        {
            if (c.Visible)
                visible.Add(c);
        }

        var rows = visible.Count == 0 ? 0 : (visible.Count + cols - 1) / cols;

        var rowHeights = new float[rows];
        for (var r = 0; r < rows; r++)
        {
            float rowH = 0f;
            for (var c = 0; c < cols; c++)
            {
                var i = r * cols + c;
                if (i >= visible.Count)
                    break;

                var child = visible[i];
                rowH = MathF.Max(rowH, child.MeasuredSize.Y + child.Margin.Vertical);
            }

            rowHeights[r] = rowH;
        }

        float y = inner.Y;
        var firstRow = true;
        for (var r = 0; r < rows; r++)
        {
            if (!firstRow)
                y += Spacing;
            firstRow = false;

            var rowH = rowHeights[r];
            float x = inner.X;
            for (var c = 0; c < cols; c++)
            {
                if (c > 0)
                    x += Spacing;

                var i = r * cols + c;
                if (i >= visible.Count)
                    break;

                var child = visible[i];
                var slot = new UiRect(x, y, colW, rowH);
                child.Arrange(slot);
                x += colW;
            }

            y += rowH;
        }
    }
}
