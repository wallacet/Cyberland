using System.Numerics;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
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
/// This system is intentionally serial: it coordinates input edges, document ordering, and renderer submission state
/// that currently relies on main-thread-only host services.
/// Candidate parallel work is limited to future per-document layout preparation that proves thread-safe against shared
/// font/glyph services; this orchestrator remains serial to preserve deterministic input consumption and draw ordering.
/// </remarks>
public sealed class UiDocumentFrameSystem : ISystem, ILateUpdate
{
    private const int MaxMeasureArrangesPerFrame = 128;
    private readonly GameHostServices _host;
    private readonly List<(EntityId Id, UiDocumentRoot Root)> _rows = new();
    private readonly List<EntityId> _activeRootIds = new();
    private readonly List<FrameDocument> _frameDocuments = new();
    private bool _prevPrimary;
    private EntityId _armedDocumentId;
    private UiButton? _armedButton;
    private bool _reportedLayoutBudgetBackpressure;
    private int _loggedFrameSpikes;

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
#if DEBUG
        using var frameScope = FrameProfilerScope.Enter("ui.frame");
#endif
        var frameStartTicks = Stopwatch.GetTimestamp();
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

#if DEBUG
        using (FrameProfilerScope.Enter("ui.query.collect_roots"))
#endif
        {
            _rows.Clear();
            _activeRootIds.Clear();
            foreach (var chunk in query)
            {
                var ents = chunk.Entities;
                var cols = chunk.Column<UiDocumentRoot>();
                for (var i = 0; i < chunk.Count; i++)
                {
                    _rows.Add((ents[i], cols[i]));
                    _activeRootIds.Add(ents[i]);
                }
            }

            _host.UiDocuments.PruneToEntities(CollectionsMarshal.AsSpan(_activeRootIds));
        }

#if DEBUG
        using (FrameProfilerScope.Enter("ui.query.sort_roots"))
#endif
        {
            _rows.Sort(static (a, b) =>
            {
                var order = a.Root.SortKeyBase.CompareTo(b.Root.SortKeyBase);
                return order != 0 ? order : a.Id.Raw.CompareTo(b.Id.Raw);
            });
        }

#if DEBUG
        using var docLoopScope = FrameProfilerScope.Enter("ui.documents.loop");
#endif
        _frameDocuments.Clear();
        var measureArrangeCount = 0;
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

            // Font/localization propagation can mark the document dirty; always run the cheap stale check first.
#if DEBUG
            using var docScope = FrameProfilerScope.Enter("ui.document.process");
#endif
            doc.PrepareFontsAndLocalizationIfNeeded(fonts, loc);

            // Submissions are discarded at the start of each render tick (see IRenderer.ResetPendingSubmissionsForNewTick).
            // We may skip MeasureArrange when layout is clean, but we must call DrawVisuals every frame so HUD persists.
            var skipMeasure = incremental && !doc.LayoutDirty && rootStable;
            if (!skipMeasure && measureArrangeCount < MaxMeasureArrangesPerFrame)
            {
#if DEBUG
                using var measureScope = FrameProfilerScope.Enter("ui.document.measure_arrange");
#endif
                doc.MeasureArrange(rootRect);
                measureArrangeCount++;
            }
            else if (!skipMeasure && !_reportedLayoutBudgetBackpressure)
            {
                EngineDiagnostics.Report(
                    EngineErrorSeverity.Warning,
                    "Cyberland.Engine.UiDocumentFrameSystem",
                    $"UI measure/arrange budget reached ({MaxMeasureArrangesPerFrame}) for this frame; remaining dirty documents reuse prior layout until next frame.");
                _reportedLayoutBudgetBackpressure = true;
            }

            _frameDocuments.Add(new FrameDocument(row.Id, cfg, doc, rootRect));
        }

        if (viewportPointer.HasValue && (wheel.Y != 0f || primaryPressed || primaryReleased))
        {
#if DEBUG
            using var pointerScope = FrameProfilerScope.Enter("ui.pointer.route");
#endif
            var ptVp = viewportPointer.GetValueOrDefault();
            var routed = TryGetTopmostViewportDocumentAtPoint(ptVp, out var target);

            if (wheel.Y != 0f && routed)
            {
#if DEBUG
                using var wheelScope = FrameProfilerScope.Enter("ui.document.wheel");
#endif
                if (TryApplyWheel(target.Document, ptVp, wheel.Y, target.RootRect))
                    target.Document.MeasureArrange(target.RootRect);
            }

            if (primaryPressed)
            {
                if (routed)
                    PointerPress(target.Id, target.Document, ptVp, target.RootRect);
                else
                    CancelArmedButton();
            }
            else if (primaryReleased)
            {
                if (routed)
                    PointerRelease(target.Id, target.Document, ptVp, target.RootRect);
                else
                    PointerReleaseWithoutHit();
            }
        }

        foreach (var frameDoc in _frameDocuments)
        {
            var cfg = frameDoc.Root;
            var doc = frameDoc.Document;
            var rootRect = frameDoc.RootRect;

#if DEBUG
            using var drawScope = FrameProfilerScope.Enter("ui.document.draw_visuals");
#endif
            doc.DrawVisuals(renderer, fonts, cache, cfg.CoordinateSpace, cfg.SortKeyBase, rootRect);
            doc.ClearVisualDirty();
        }

        var frameElapsedMs = (Stopwatch.GetTimestamp() - frameStartTicks) * 1000d / Stopwatch.Frequency;
        if (frameElapsedMs >= 16d && _loggedFrameSpikes < 12)
        {
            _loggedFrameSpikes++;
            EngineDiagnostics.Report(
                EngineErrorSeverity.Warning,
                "Cyberland.Engine.UiDocumentFrameSystem",
                $"UI frame spike | frame_ms={frameElapsedMs:0.###} docs={_frameDocuments.Count} measure_arranges={measureArrangeCount}");
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

    private bool TryGetTopmostViewportDocumentAtPoint(Vector2D<float> pointer, out FrameDocument target)
    {
        for (var i = _frameDocuments.Count - 1; i >= 0; i--)
        {
            var doc = _frameDocuments[i];
            if (doc.Root.CoordinateSpace != CoordinateSpace.ViewportSpace)
                continue;
            if (!doc.RootRect.Contains(pointer))
                continue;

            target = doc;
            return true;
        }

        target = default;
        return false;
    }

    private void PointerPress(EntityId documentId, UiDocument doc, Vector2D<float> pointer, UiRect clip)
    {
        var hit = doc.HitTest(pointer, clip);
        var btn = FindAncestorButton(hit);
        CancelArmedButton();
        _armedDocumentId = documentId;
        _armedButton = btn;
        btn?.NotifyPressStarted();
    }

    private void PointerRelease(EntityId documentId, UiDocument doc, Vector2D<float> pointer, UiRect clip)
    {
        var hit = doc.HitTest(pointer, clip);
        MaybeSelectRadio(hit);

        var btnHit = FindAncestorButton(hit);

        if (_armedButton is { } armed)
        {
            var releasedOnSelf =
                _armedDocumentId.Equals(documentId) &&
                ReferenceEquals(btnHit, armed) &&
                armed.Interactable;
            armed.NotifyPressEnded(releasedOnSelf);
        }

        _armedDocumentId = default;
        _armedButton = null;
    }

    private void PointerReleaseWithoutHit()
    {
        if (_armedButton is { } armed)
            armed.NotifyPressEnded(false);
        _armedDocumentId = default;
        _armedButton = null;
    }

    private void CancelArmedButton()
    {
        _armedButton?.NotifyCancelPress();
        _armedDocumentId = default;
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

    private static bool TryApplyWheel(UiDocument doc, Vector2D<float> pointer, float wheelY, UiRect clip)
    {
        var sv = FindDeepestScrollView(doc.Root, pointer, clip);
        if (sv is null)
            return false;

        var before = sv.VerticalOffset;
        sv.ApplyWheel(wheelY);
        return before != sv.VerticalOffset;
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

    private readonly record struct FrameDocument(EntityId Id, UiDocumentRoot Root, UiDocument Document, UiRect RootRect);
}
