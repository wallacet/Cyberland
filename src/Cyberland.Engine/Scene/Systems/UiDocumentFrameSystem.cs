using System.Numerics;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Ecs;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Serial late pass: resolves retained <see cref="UiDocument"/> widgets registered on the host, runs layout, routes pointer
/// input for viewport HUD documents, then submits draw calls above prior HUD/text passes (registration order).
/// </summary>
/// <remarks>
/// World-space documents still layout and draw, but pointer routing is implemented only for <see cref="CoordinateSpace.ViewportSpace"/> in v1.
/// </remarks>
public sealed class UiDocumentFrameSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private readonly List<(EntityId Id, UiDocumentRoot Root)> _rows = new();
    private bool _prevPrimary;
    private UiButton? _armedButton;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<UiDocumentRoot>();

    /// <summary>Creates the UI frame driver.</summary>
    public UiDocumentFrameSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = deltaSeconds;

        var renderer = _host.Renderer;
        var fonts = _host.Fonts;
        var cache = _host.TextGlyphCache;
        var loc = _host.LocalizedContent?.Strings;

        var input = _host.Input;
        var primaryDown = input?.IsControlDown(InputControl.MouseButtonControl(MouseButton.Left)) ?? false;
        var primaryPressed = primaryDown && !_prevPrimary;
        var primaryReleased = !primaryDown && _prevPrimary;
        _prevPrimary = primaryDown;

        Vector2D<float>? viewportPointer = null;
        if (input is not null)
        {
            var mp = input.GetMousePosition(CoordinateSpace.ViewportSpace);
            viewportPointer = new Vector2D<float>(mp.X, mp.Y);
        }

        var wheel = input?.MouseWheelDelta ?? Vector2.Zero;

        _rows.Clear();
        foreach (var chunk in query)
        {
            var ents = chunk.Entities;
            var cols = chunk.Column<UiDocumentRoot>();
            for (var i = 0; i < chunk.Count; i++)
                _rows.Add((ents[i], cols[i]));
        }

        _rows.Sort(static (a, b) =>
        {
            var order = a.Root.SortKeyBase.CompareTo(b.Root.SortKeyBase);
            return order != 0 ? order : a.Id.Raw.CompareTo(b.Id.Raw);
        });

        foreach (var row in _rows)
        {
            ref readonly var cfg = ref row.Root;
            if (!cfg.Visible)
                continue;

            if (!_host.UiDocuments.TryGet(row.Id, out var doc))
                continue;

            // Use the same-tick ECS camera snapshot when available so retained UI layout, viewport anchors, and
            // bitmap text all resolve against one viewport contract. Fallback to renderer state for early startup.
            var vpRuntime = _host.CameraRuntimeState.ViewportSizeWorld;
            var vp = vpRuntime.X > 0 && vpRuntime.Y > 0
                ? vpRuntime
                : renderer.ActiveCameraViewportSize;
            var rootRect = ResolveRootRect(cfg.RootPreset, vp);

            var incremental = UiLayoutGating.UseIncrementalDocumentFrames;
            var rootStable = RootRectNearlyEquals(rootRect, doc.LastArrangedRootRect);
            var hasViewportPointer =
                cfg.CoordinateSpace == CoordinateSpace.ViewportSpace && viewportPointer.HasValue;
            var ptVp = viewportPointer.GetValueOrDefault();
            var wheelY = hasViewportPointer ? wheel.Y : 0f;
            var pointerNeedsFrame = hasViewportPointer &&
                (wheelY != 0f || primaryPressed || primaryReleased);

            // Font/localization propagation can mark the document dirty; always run the cheap stale check first.
            doc.PrepareFontsAndLocalizationIfNeeded(fonts, loc);

            // Submissions are discarded at the start of each render tick (see IRenderer.ResetPendingSubmissionsForNewTick).
            // We may skip MeasureArrange when layout is clean, but we must call DrawVisuals every frame so HUD persists.
            var skipMeasure = incremental && !doc.LayoutDirty && rootStable;
            if (!skipMeasure)
                doc.MeasureArrange(rootRect);

            if (pointerNeedsFrame && hasViewportPointer)
            {
                if (wheelY != 0f)
                {
                    TryApplyWheel(doc, ptVp, wheelY, rootRect);
                    doc.MeasureArrange(rootRect);
                }

                if (primaryPressed)
                    PointerPress(doc, ptVp, rootRect);
                else if (primaryReleased)
                    PointerRelease(doc, ptVp, rootRect);
            }

            doc.DrawVisuals(renderer, fonts, cache, cfg.CoordinateSpace, cfg.SortKeyBase, rootRect);
            doc.ClearVisualDirty();
        }
    }

    private static bool RootRectNearlyEquals(in UiRect a, in UiRect b) =>
        MathF.Abs(a.X - b.X) < 0.25f &&
        MathF.Abs(a.Y - b.Y) < 0.25f &&
        MathF.Abs(a.Width - b.Width) < 0.25f &&
        MathF.Abs(a.Height - b.Height) < 0.25f;

    private static UiRect ResolveRootRect(UiDocumentRootPreset preset, Vector2D<int> viewportPx) =>
        preset switch
        {
            UiDocumentRootPreset.FullViewport => new UiRect(0f, 0f, viewportPx.X, viewportPx.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported UiDocumentRoot preset."),
        };

    private void PointerPress(UiDocument doc, Vector2D<float> pointer, UiRect clip)
    {
        var hit = doc.HitTest(pointer, clip);
        var btn = FindAncestorButton(hit);
        _armedButton?.NotifyCancelPress();
        _armedButton = btn;
        btn?.NotifyPressStarted();
    }

    private void PointerRelease(UiDocument doc, Vector2D<float> pointer, UiRect clip)
    {
        var hit = doc.HitTest(pointer, clip);
        MaybeSelectRadio(hit);

        var btnHit = FindAncestorButton(hit);

        if (_armedButton is { } armed)
        {
            var releasedOnSelf = ReferenceEquals(btnHit, armed) && armed.Interactable;
            armed.NotifyPressEnded(releasedOnSelf);
        }

        _armedButton = null;
    }

    private static void MaybeSelectRadio(UiElement? hit)
    {
        for (var e = hit; e != null; e = e.Parent)
        {
            if (e is UiRadioButton rb)
            {
                rb.SelectFromUiSystem();
                return;
            }
        }
    }

    private static UiButton? FindAncestorButton(UiElement? hit)
    {
        for (var e = hit; e != null; e = e.Parent)
        {
            if (e is UiButton b)
                return b;
        }

        return null;
    }

    private static void TryApplyWheel(UiDocument doc, Vector2D<float> pointer, float wheelY, UiRect clip)
    {
        var sv = FindDeepestScrollView(doc.Root, pointer, clip);
        sv?.ApplyWheel(wheelY);
    }

    /// <summary>
    /// Scroll routing ignores <see cref="UiElement.Interactable"/> so wheels reach clipped viewports even though panels default to non-interactive.
    /// </summary>
    internal static UiScrollView? FindDeepestScrollView(UiElement e, Vector2D<float> p, UiRect clip)
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
            var nested = FindDeepestScrollView(span[i], p, childClip);
            if (nested is not null)
                return nested;
        }

        return e is UiScrollView sv ? sv : null;
    }
}
