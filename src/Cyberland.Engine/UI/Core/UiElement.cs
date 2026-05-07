using System;
using System.Collections.Generic;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Base node for retained UI: parent/child links, margins, Unity-style anchors, measure/arrange, and draw traversal.
/// </summary>
public class UiElement
{
    private readonly List<UiElement> _children = new();
    private readonly ChildIndexComparer _childIndexComparer;
    private UiDocument? _hostDocument;
    private UiElement[]? _sortedChildrenScratch;
    private int[]? _sortedChildIndexScratch;
    private int _sortedChildCount;
    private bool _sortedChildrenDirty = true;
    private bool _visible = true;
    private float _sortKey;

    /// <summary>Creates a retained UI element with stable child-sort helpers allocated once.</summary>
    public UiElement() => _childIndexComparer = new ChildIndexComparer(_children);

    /// <summary>Parent in the UI graph (not scene <see cref="Scene.Transform"/>).</summary>
    public UiElement? Parent { get; private set; }

    /// <summary>Children in stable insertion order.</summary>
    public IReadOnlyList<UiElement> Children => _children;

    /// <summary>When false, the element skips measure/arrange/draw and reports zero measured size.</summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
                return;
            _visible = value;
            InvalidateLayout();
        }
    }

    /// <summary>When true, this node may receive pointer hits when it is the deepest matching target.</summary>
    public bool Interactable { get; set; }

    /// <summary>
    /// Sub-sort offset added to the accumulated sort key for this subtree (larger tends to draw later among siblings).
    /// </summary>
    public float SortKey
    {
        get => _sortKey;
        set
        {
            if (_sortKey == value)
                return;
            _sortKey = value;
            _sortedChildrenDirty = true;
            InvalidateVisual();
        }
    }

    private UiClipMode _clipMode;
    private UiThickness _margin;
    private UiThickness _padding;
    private Vector2D<float> _anchorMin;
    private Vector2D<float> _anchorMax;
    private Vector2D<float> _pivot;
    private Vector2D<float> _anchoredPosition;
    private Vector2D<float> _sizeDelta;
    private float _stretchLeft;
    private float _stretchRight;
    private float _stretchTop;
    private float _stretchBottom;

    /// <summary>How descendants are clipped relative to this node's bounds.</summary>
    public UiClipMode ClipMode
    {
        get => _clipMode;
        set
        {
            if (_clipMode == value)
                return;
            _clipMode = value;
            InvalidateLayout();
        }
    }

    /// <summary>Outer inset between this border box and the parent's content slot.</summary>
    public UiThickness Margin
    {
        get => _margin;
        set
        {
            if (_margin.Equals(value))
                return;
            _margin = value;
            InvalidateLayout();
        }
    }

    /// <summary>Inset between border box edges and the content box used for child layout.</summary>
    public UiThickness Padding
    {
        get => _padding;
        set
        {
            if (_padding.Equals(value))
                return;
            _padding = value;
            InvalidateLayout();
        }
    }

    /// <summary>Normalized anchor corners in the parent content slot (after this element's margin).</summary>
    public Vector2D<float> AnchorMin
    {
        get => _anchorMin;
        set
        {
            if (_anchorMin == value)
                return;
            _anchorMin = value;
            InvalidateLayout();
        }
    }

    /// <summary>Normalized anchor corners in the parent content slot (after this element's margin).</summary>
    public Vector2D<float> AnchorMax
    {
        get => _anchorMax;
        set
        {
            if (_anchorMax == value)
                return;
            _anchorMax = value;
            InvalidateLayout();
        }
    }

    /// <summary>Normalized pivot on this element's resolved rect.</summary>
    public Vector2D<float> Pivot
    {
        get => _pivot;
        set
        {
            if (_pivot == value)
                return;
            _pivot = value;
            InvalidateLayout();
        }
    }

    /// <summary>Pixel shift of the pivot vs the anchor point when an axis is collapsed.</summary>
    public Vector2D<float> AnchoredPosition
    {
        get => _anchoredPosition;
        set
        {
            if (_anchoredPosition == value)
                return;
            _anchoredPosition = value;
            InvalidateLayout();
        }
    }

    /// <summary>Pixel width/height when an axis is collapsed; stretch axes use <see cref="StretchLeft"/> instead.</summary>
    public Vector2D<float> SizeDelta
    {
        get => _sizeDelta;
        set
        {
            if (_sizeDelta == value)
                return;
            _sizeDelta = value;
            InvalidateLayout();
        }
    }

    /// <summary>Left inset when the X axis is stretched.</summary>
    public float StretchLeft
    {
        get => _stretchLeft;
        set
        {
            if (_stretchLeft == value)
                return;
            _stretchLeft = value;
            InvalidateLayout();
        }
    }

    /// <summary>Right inset when the X axis is stretched.</summary>
    public float StretchRight
    {
        get => _stretchRight;
        set
        {
            if (_stretchRight == value)
                return;
            _stretchRight = value;
            InvalidateLayout();
        }
    }

    /// <summary>Top inset when the Y axis is stretched.</summary>
    public float StretchTop
    {
        get => _stretchTop;
        set
        {
            if (_stretchTop == value)
                return;
            _stretchTop = value;
            InvalidateLayout();
        }
    }

    /// <summary>Bottom inset when the Y axis is stretched.</summary>
    public float StretchBottom
    {
        get => _stretchBottom;
        set
        {
            if (_stretchBottom == value)
                return;
            _stretchBottom = value;
            InvalidateLayout();
        }
    }

    /// <summary>Latest desired border-box size from <see cref="Measure"/>.</summary>
    public Vector2D<float> MeasuredSize { get; protected set; }

    /// <summary>Resolved border box after <see cref="Arrange"/> (absolute document pixels for the current pass).</summary>
    public UiRect ComputedBounds { get; protected set; }

    /// <summary>Adds <paramref name="child"/> as the last child and reparents it from any prior parent.</summary>
    public T AddChild<T>(T child) where T : UiElement
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent == this)
            return child;

        child.Parent?.RemoveChild(child);
        child.Parent = this;
        _children.Add(child);
        child.AttachDocument(_hostDocument);
        _sortedChildrenDirty = true;
        InvalidateLayout();
        return child;
    }

    /// <summary>Removes <paramref name="child"/> when it is a direct child.</summary>
    public void RemoveChild(UiElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent != this)
            return;

        _children.Remove(child);
        child.Parent = null;
        child.AttachDocument(null);
        _sortedChildrenDirty = true;
        InvalidateLayout();
    }

    internal void AttachDocument(UiDocument? doc)
    {
        if (_hostDocument == doc)
        {
            foreach (var c in _children)
                c.AttachDocument(doc);
            return;
        }

        _hostDocument = doc;
        foreach (var c in _children)
            c.AttachDocument(doc);
    }

    /// <summary>Marks the owning <see cref="UiDocument"/> so the next frame runs layout (and draw).</summary>
    protected void InvalidateLayout() => _hostDocument?.NotifyLayoutDirty();

    /// <summary>Marks the owning document for redraw without forcing a full measure (rare; most callers use <see cref="InvalidateLayout"/>).</summary>
    protected void InvalidateVisual() => _hostDocument?.NotifyVisualDirty();

    /// <summary>Rebuilds the cached child draw/hit order snapshot (<see cref="SortedChildren"/>).</summary>
    internal void RebuildSortedChildrenSnapshot()
    {
        var n = _children.Count;
        if (n == 0)
        {
            _sortedChildrenScratch ??= Array.Empty<UiElement>();
            _sortedChildCount = 0;
            _sortedChildrenDirty = false;
            return;
        }

        if (_sortedChildrenScratch is null || _sortedChildrenScratch.Length < n)
            _sortedChildrenScratch = new UiElement[Math.Max(16, n)];
        if (_sortedChildIndexScratch is null || _sortedChildIndexScratch.Length < n)
            _sortedChildIndexScratch = new int[Math.Max(16, n)];

        for (var i = 0; i < n; i++)
            _sortedChildIndexScratch[i] = i;
        Array.Sort(_sortedChildIndexScratch, 0, n, _childIndexComparer);
        for (var i = 0; i < n; i++)
            _sortedChildrenScratch[i] = _children[_sortedChildIndexScratch[i]];

        _sortedChildCount = n;
        _sortedChildrenDirty = false;
    }

    /// <summary>Visible direct children in draw/hit order (ascending <see cref="SortKey"/>, then insertion order).</summary>
    public ReadOnlySpan<UiElement> SortedChildren()
    {
        if (_sortedChildrenDirty)
            RebuildSortedChildrenSnapshot();
        return (_sortedChildrenScratch ?? Array.Empty<UiElement>()).AsSpan(0, _sortedChildCount);
    }

    private sealed class ChildIndexComparer : IComparer<int>
    {
        private readonly List<UiElement> _children;

        public ChildIndexComparer(List<UiElement> children) => _children = children;

        public int Compare(int x, int y)
        {
            var c = _children[x].SortKey.CompareTo(_children[y].SortKey);
            return c != 0 ? c : x.CompareTo(y);
        }
    }

    /// <summary>Computes <see cref="MeasuredSize"/> for this node under <paramref name="constraints"/>.</summary>
    public Vector2D<float> Measure(in UiSizeConstraints constraints)
    {
        if (!Visible)
        {
            MeasuredSize = default;
            return MeasuredSize;
        }

        MeasuredSize = MeasureCore(in constraints);
        return MeasuredSize;
    }

    /// <summary>
    /// Limits vertical slack passed to children when this node's own border height is band-shaped (collapsed vertical
    /// anchor axis and height from <see cref="SizeDelta"/>), e.g. <see cref="UiLayoutPresets.TopStretch"/>. Otherwise a
    /// <see cref="Controls.UiLabel"/> using <see cref="UiLayoutPresets.StretchAll"/> measures to the full parent height
    /// and inflates cross-axis size in <see cref="Layout.UiHorizontalStack"/> or pushes siblings away in vertical stacks.
    /// </summary>
    protected static float ClampInnerMaxHeightForBand(UiElement self, float innerMaxH)
    {
        var stretchY = self.AnchorMax.Y - self.AnchorMin.Y > UiLayoutConstants.AxisEpsilon;
        if (stretchY)
            return innerMaxH;

        var band = self.SizeDelta.Y - self.Padding.Vertical;
        if (band <= UiLayoutConstants.AxisEpsilon)
            return innerMaxH;

        return MathF.Min(innerMaxH, band);
    }

    /// <summary>Arranges this element into an absolute margin-box slot from the parent layout.</summary>
    public virtual void Arrange(in UiRect allocationMarginBoxAbsolute)
    {
        if (!Visible)
        {
            ComputedBounds = default;
            return;
        }

        var anchorSlot = allocationMarginBoxAbsolute.Deflate(Margin);
        ComputedBounds = UiAnchorLayout.ResolveBounds(
            anchorSlot,
            AnchorMin,
            AnchorMax,
            Pivot,
            AnchoredPosition,
            SizeDelta,
            StretchLeft,
            StretchRight,
            StretchTop,
            StretchBottom);
    }

    /// <summary>
    /// Draw hook; default walks children in <see cref="SortedChildren"/> order so debug traversal matches visual/hit ordering.
    /// </summary>
    public virtual void Draw(UiRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Visible)
            return;

        var inherited = context.CurrentClip;
        var selfClip = ComputedBounds.Intersect(inherited);
        context.RecordDebugRect(selfClip);

        var childClip = ClipMode == UiClipMode.IntersectParent ? selfClip : inherited;
        context.PushClip(childClip);
        var span = SortedChildren();
        for (var i = 0; i < span.Length; i++)
            span[i].Draw(context);
        context.PopClip();
    }

    /// <summary>
    /// Depth-first visual submission for HUD passes: combines <see cref="SortKey"/> with the caller’s accumulated key.
    /// Submits visuals clipped to <paramref name="inheritedClip"/> per <see cref="ClipMode"/> (matches <see cref="HitTest"/>).
    /// </summary>
    public virtual void DrawVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float accumulatedSortKey,
        in UiRect inheritedClip)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(cache);
        if (!Visible)
            return;

        var selfClip = ComputedBounds.Intersect(inheritedClip);
        var mine = accumulatedSortKey + SortKey;
        DrawSelfVisuals(renderer, fonts, cache, space, mine, selfClip, inheritedClip);

        var span = SortedChildren();
        for (var i = 0; i < span.Length; i++)
        {
            var rank = i + 1;
            var child = span[i];
            var childInherited = ClipMode == UiClipMode.IntersectParent ? selfClip : inheritedClip;
            child.DrawVisuals(renderer, fonts, cache, space, mine + rank * 1e-4f, childInherited);
        }
    }

    /// <summary>
    /// Override to submit quads/glyphs for this node before children.
    /// <paramref name="viewportClip"/> is <see cref="ComputedBounds"/> ∩ ancestor clip; <paramref name="inheritedClip"/>
    /// is the clip inherited from this node's parent (cap for inflating text past layout bounds).
    /// </summary>
    protected virtual void DrawSelfVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey,
        in UiRect viewportClip,
        in UiRect inheritedClip)
    {
        _ = renderer;
        _ = fonts;
        _ = cache;
        _ = space;
        _ = sortKey;
        _ = viewportClip;
        _ = inheritedClip;
    }

    /// <summary>Deepest top-most interactable node under <paramref name="point"/> (document +Y down space).</summary>
    public UiElement? HitTest(Vector2D<float> point, in UiRect documentClip) =>
        HitTestRecursive(this, point, documentClip);

    private static UiElement? HitTestRecursive(UiElement e, Vector2D<float> p, UiRect clip)
    {
        if (!e.Visible)
            return null;

        var bounds = e.ComputedBounds;
        if (!bounds.Contains(p))
            return null;

        var selfClip = bounds.Intersect(clip);
        if (selfClip.Width <= 0f || selfClip.Height <= 0f || !selfClip.Contains(p))
            return null;

        var childClip = e.ClipMode == UiClipMode.IntersectParent ? selfClip : clip;
        var span = e.SortedChildren();
        for (var i = span.Length - 1; i >= 0; i--)
        {
            var hit = HitTestRecursive(span[i], p, childClip);
            if (hit is not null)
                return hit;
        }

        return e.Interactable ? e : null;
    }

    /// <summary>Leaf/default sizing: collapsed axes honor <see cref="SizeDelta"/>; stretched axes expand to constraint maxima.</summary>
    protected virtual Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        var stretchX = AnchorMax.X - AnchorMin.X > UiLayoutConstants.AxisEpsilon;
        var stretchY = AnchorMax.Y - AnchorMin.Y > UiLayoutConstants.AxisEpsilon;

        var dw = stretchX ? constraints.MaxWidth : SizeDelta.X + Margin.Horizontal;
        var dh = stretchY ? constraints.MaxHeight : SizeDelta.Y + Margin.Vertical;
        return constraints.ClampSize(new Vector2D<float>(dw, dh));
    }
}
