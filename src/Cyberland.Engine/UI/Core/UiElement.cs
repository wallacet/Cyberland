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

    /// <summary>Parent in the UI graph (not scene <see cref="Scene.Transform"/>).</summary>
    public UiElement? Parent { get; private set; }

    /// <summary>Children in stable insertion order.</summary>
    public IReadOnlyList<UiElement> Children => _children;

    /// <summary>When false, the element skips measure/arrange/draw and reports zero measured size.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>When true, this node may receive pointer hits when it is the deepest matching target.</summary>
    public bool Interactable { get; set; }

    /// <summary>
    /// Sub-sort offset added to the accumulated sort key for this subtree (larger tends to draw later among siblings).
    /// </summary>
    public float SortKey { get; set; }

    /// <summary>How descendants are clipped relative to this node's bounds.</summary>
    public UiClipMode ClipMode { get; set; }

    /// <summary>Outer inset between this border box and the parent's content slot.</summary>
    public UiThickness Margin { get; set; }

    /// <summary>Inset between border box edges and the content box used for child layout.</summary>
    public UiThickness Padding { get; set; }

    /// <summary>Normalized anchor corners in the parent content slot (after this element's margin).</summary>
    public Vector2D<float> AnchorMin { get; set; }

    /// <summary>Normalized anchor corners in the parent content slot (after this element's margin).</summary>
    public Vector2D<float> AnchorMax { get; set; }

    /// <summary>Normalized pivot on this element's resolved rect.</summary>
    public Vector2D<float> Pivot { get; set; }

    /// <summary>Pixel shift of the pivot vs the anchor point when an axis is collapsed.</summary>
    public Vector2D<float> AnchoredPosition { get; set; }

    /// <summary>Pixel width/height when an axis is collapsed; stretch axes use <see cref="StretchLeft"/> instead.</summary>
    public Vector2D<float> SizeDelta { get; set; }

    /// <summary>Left inset when the X axis is stretched.</summary>
    public float StretchLeft { get; set; }

    /// <summary>Right inset when the X axis is stretched.</summary>
    public float StretchRight { get; set; }

    /// <summary>Top inset when the Y axis is stretched.</summary>
    public float StretchTop { get; set; }

    /// <summary>Bottom inset when the Y axis is stretched.</summary>
    public float StretchBottom { get; set; }

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
    }

    /// <summary>Measures desired border-box size under parent constraints (+Y down).</summary>
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
        const float eps = 1e-4f;
        var stretchY = self.AnchorMax.Y - self.AnchorMin.Y > eps;
        if (stretchY)
            return innerMaxH;

        var band = self.SizeDelta.Y - self.Padding.Vertical;
        if (band <= eps)
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

    /// <summary>Draw hook; default walks children with clipping rules.</summary>
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
        foreach (var child in _children)
            child.Draw(context);
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

        var rank = 0;
        foreach (var child in EnumerateSortedChildren())
        {
            rank++;
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

        foreach (var child in e.EnumerateSortedChildrenDescending())
        {
            var hit = HitTestRecursive(child, p, childClip);
            if (hit is not null)
                return hit;
        }

        return e.Interactable ? e : null;
    }

    /// <summary>Stable ascending <see cref="SortKey"/> then insertion order.</summary>
    protected IEnumerable<UiElement> EnumerateSortedChildren()
    {
        var tmp = new List<(UiElement element, int index)>(_children.Count);
        for (var i = 0; i < _children.Count; i++)
            tmp.Add((_children[i], i));

        tmp.Sort(static (a, b) =>
        {
            var c = a.element.SortKey.CompareTo(b.element.SortKey);
            return c != 0 ? c : a.index.CompareTo(b.index);
        });

        foreach (var t in tmp)
            yield return t.element;
    }

    /// <summary>Descending <see cref="SortKey"/> for hit-testing (top-most first).</summary>
    private IEnumerable<UiElement> EnumerateSortedChildrenDescending()
    {
        var list = new List<UiElement>();
        foreach (var c in EnumerateSortedChildren())
            list.Add(c);

        for (var i = list.Count - 1; i >= 0; i--)
            yield return list[i];
    }

    /// <summary>Leaf/default sizing: collapsed axes honor <see cref="SizeDelta"/>; stretched axes expand to constraint maxima.</summary>
    protected virtual Vector2D<float> MeasureCore(in UiSizeConstraints constraints)
    {
        const float eps = 1e-4f;
        var stretchX = AnchorMax.X - AnchorMin.X > eps;
        var stretchY = AnchorMax.Y - AnchorMin.Y > eps;

        var dw = stretchX ? constraints.MaxWidth : SizeDelta.X + Margin.Horizontal;
        var dh = stretchY ? constraints.MaxHeight : SizeDelta.Y + Margin.Vertical;
        return constraints.ClampSize(new Vector2D<float>(dw, dh));
    }
}
