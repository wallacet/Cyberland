using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Rasterizes one grapheme into a contour-based RGB MSDF atlas tile.
/// </summary>
internal static class GlyphRasterizer
{
    internal const int MsdfBorderPx = TextMsdfDefaults.BorderPixels;
    internal const float MsdfPixelRange = TextMsdfDefaults.PixelRange;

    /// <summary>Bump when MSDF generation or metrics change so glyph caches miss stale tiles.</summary>
    internal const int RasterRevision = 7;


    /// <summary>
    /// Builds a small RGBA bitmap for one glyph cluster and horizontal advance in pixels.
    /// </summary>
    /// <param name="font">Resolved font face.</param>
    /// <param name="glyph">One grapheme as a string (may be surrogate pair).</param>
    /// <param name="rgba">Packed RGBA8, length <c>width*height*4</c>.</param>
    /// <param name="atlasWidth">Atlas bitmap width in texture pixels.</param>
    /// <param name="atlasHeight">Atlas bitmap height in texture pixels.</param>
    /// <param name="drawWidthPx">Glyph draw width in logical text pixels.</param>
    /// <param name="drawHeightPx">Glyph draw height in logical text pixels.</param>
    /// <param name="advancePx">Horizontal advance to the next pen position.</param>
    /// <param name="offsetPenToCenterX">From current pen X to sprite center X (same coord system as SixLabors bounds).</param>
    /// <param name="offsetPenToCenterYWorld">
    /// From baseline world Y to sprite center Y in **world** space (+Y up). Computed as
    /// <c>-(bounds.Top + bounds.Height/2)</c> when bounds use +Y down from the layout origin on the baseline.
    /// </param>
    /// <param name="msdfPixelRange">Signed distance normalization range in source atlas pixels.</param>
    public static bool TryCreateGlyphMsdf(
        Font font,
        string glyph,
        [NotNullWhen(true)] out byte[]? rgba,
        out int atlasWidth,
        out int atlasHeight,
        out float drawWidthPx,
        out float drawHeightPx,
        out float advancePx,
        out float offsetPenToCenterX,
        out float offsetPenToCenterYWorld,
        out float msdfPixelRange) =>
        TryCreateGlyphMsdf(font, glyph.AsSpan(), out rgba, out atlasWidth, out atlasHeight, out drawWidthPx, out drawHeightPx, out advancePx,
            out offsetPenToCenterX, out offsetPenToCenterYWorld, out msdfPixelRange);

    /// <summary>Rasterizes one grapheme cluster (1–2 UTF-16 code units) to premultiplied RGBA.</summary>
    public static bool TryCreateGlyphMsdf(
        Font font,
        ReadOnlySpan<char> glyph,
        [NotNullWhen(true)] out byte[]? rgba,
        out int atlasWidth,
        out int atlasHeight,
        out float drawWidthPx,
        out float drawHeightPx,
        out float advancePx,
        out float offsetPenToCenterX,
        out float offsetPenToCenterYWorld,
        out float msdfPixelRange)
    {
        rgba = null;
        atlasWidth = 1;
        atlasHeight = 1;
        drawWidthPx = 1f;
        drawHeightPx = 1f;
        advancePx = 0f;
        offsetPenToCenterX = 0f;
        offsetPenToCenterYWorld = 0f;
        msdfPixelRange = MsdfPixelRange * TextMsdfDefaults.AtlasSupersample;

        var opts = new TextOptions(font) { Dpi = 96f };
        if (glyph.IsEmpty)
            return false;

        var glyphStr = new string(glyph);

        // Non-throwing measurers (single codepoint); union advance box with ink bounds for placement.
        var ink = TextMeasurer.MeasureBounds(glyphStr, opts);
        var advRect = TextMeasurer.MeasureAdvance(glyphStr, opts);
        var b = FontRectangle.Union(in advRect, in ink);

        advancePx = advRect.Width > 0f ? advRect.Width : MathF.Max(1f, font.Size * 0.25f);

        var inkWidth = Math.Max(1, (int)MathF.Ceiling(b.Width));
        var inkHeight = Math.Max(1, (int)MathF.Ceiling(b.Height));
        drawWidthPx = inkWidth + MsdfBorderPx * 2f;
        drawHeightPx = inkHeight + MsdfBorderPx * 2f;
        var supersample = MathF.Max(drawWidthPx, drawHeightPx) <= TextMsdfDefaults.AtlasSupersampleSmallGlyphMaxDrawPixels
            ? TextMsdfDefaults.AtlasSupersampleSmallGlyph
            : TextMsdfDefaults.AtlasSupersample;
        msdfPixelRange = MsdfPixelRange * supersample;
        atlasWidth = Math.Max(1, (int)MathF.Ceiling(drawWidthPx * supersample));
        atlasHeight = Math.Max(1, (int)MathF.Ceiling(drawHeightPx * supersample));

        rgba = new byte[atlasWidth * atlasHeight * 4];
        var renderOpts = new TextOptions(font)
        {
            Dpi = 96f,
            Origin = new PointF(-b.Left + MsdfBorderPx, -b.Top + MsdfBorderPx)
        };
        BuildRgbMsdf(glyphStr, renderOpts, rgba, atlasWidth, atlasHeight, msdfPixelRange, supersample);

        var bitmapLeft = b.Left - MsdfBorderPx;
        var bitmapTop = b.Top - MsdfBorderPx;
        offsetPenToCenterX = bitmapLeft + drawWidthPx * 0.5f;
        offsetPenToCenterYWorld = -(bitmapTop + drawHeightPx * 0.5f);
        return true;
    }

    [ExcludeFromCodeCoverage]
    private static void BuildRgbMsdf(string glyph, TextOptions options, byte[] rgba, int width, int height, float pxRange, int supersample)
    {
        var capture = new OutlineCaptureGlyphRenderer();
        IGlyphRendererExtensions.Render(capture, glyph, options);
        if (capture.EdgeCount == 0)
        {
            for (var i = 0; i < rgba.Length; i += 4)
            {
                // No contours (for example whitespace): encode fully outside so shader coverage is zero.
                rgba[i] = 0;
                rgba[i + 1] = 0;
                rgba[i + 2] = 0;
                rgba[i + 3] = 0;
            }

            return;
        }

        var invRange = 1f / MathF.Max(0.01f, pxRange);
        var stride = width * 4;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = y * stride + x * 4;
                var point = new Vector2((x + 0.5f) / supersample, (y + 0.5f) / supersample);
                var inside = capture.Contains(point);
                capture.ComputeUnsignedDistances(point, out var dr, out var dg, out var db);
                var sign = inside ? 1f : -1f;
                rgba[idx + 0] = EncodeMsdfChannel(sign * dr * supersample, invRange);
                rgba[idx + 1] = EncodeMsdfChannel(sign * dg * supersample, invRange);
                rgba[idx + 2] = EncodeMsdfChannel(sign * db * supersample, invRange);
                var df = MathF.Min(dr, MathF.Min(dg, db));
                rgba[idx + 3] = EncodeMsdfChannel(sign * df * supersample, invRange);
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private static byte EncodeMsdfChannel(float signedDistance, float invRange)
    {
        var normalized = Math.Clamp(0.5f + signedDistance * invRange, 0f, 1f);
        return (byte)Math.Clamp((int)(normalized * 255f), 0, 255);
    }

    [ExcludeFromCodeCoverage]
    private sealed class OutlineCaptureGlyphRenderer : IGlyphRenderer
    {
        private readonly List<ColoredSegment> _edges = new();
        private readonly List<Contour> _contours = new();
        private List<Vector2>? _currentFigure;
        private List<RawSegment>? _currentFigureSegments;
        private Vector2 _currentPoint;
        private bool _hasCurrentPoint;
        private const float CornerAngleThresholdRadians = 0.45f;

        public int EdgeCount => _edges.Count;

        public TextDecorations EnabledDecorations() => TextDecorations.None;

        public void BeginText(in FontRectangle bounds) { }

        public void EndText() { }

        public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters) => true;

        public void EndGlyph() { }

        public void BeginFigure()
        {
            _currentFigure = new List<Vector2>(16);
            _currentFigureSegments = new List<RawSegment>(16);
            _hasCurrentPoint = false;
        }

        public void EndFigure()
        {
            if (_currentFigure is null || _currentFigureSegments is null || _currentFigure.Count < 2)
            {
                _currentFigure = null;
                _currentFigureSegments = null;
                _hasCurrentPoint = false;
                return;
            }

            var first = _currentFigure[0];
            var last = _currentFigure[^1];
            if (!NearlyEqual(first, last))
                AddLineTo(first);

            if (_currentFigure.Count >= 3)
            {
                var contour = new Contour(_currentFigure.ToArray());
                _contours.Add(contour);
                AddContourEdgesWithCornerColoring(_currentFigureSegments);
            }

            _currentFigure = null;
            _currentFigureSegments = null;
            _hasCurrentPoint = false;
        }

        public void MoveTo(Vector2 point)
        {
            if (_currentFigure is null)
                _currentFigure = new List<Vector2>(16);
            if (_currentFigureSegments is null)
                _currentFigureSegments = new List<RawSegment>(16);
            _currentFigure.Add(point);
            _currentPoint = point;
            _hasCurrentPoint = true;
        }

        public void LineTo(Vector2 point)
        {
            if (!_hasCurrentPoint)
            {
                MoveTo(point);
                return;
            }

            AddLineTo(point);
        }

        public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 endPoint)
        {
            if (!_hasCurrentPoint)
            {
                MoveTo(endPoint);
                return;
            }

            var start = _currentPoint;
            _currentFigureSegments?.Add(RawSegment.Quadratic(start, secondControlPoint, endPoint));
            AppendFlattenedQuadraticPoints(_currentFigure, start, secondControlPoint, endPoint);
            _currentPoint = endPoint;
        }

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 endPoint)
        {
            if (!_hasCurrentPoint)
            {
                MoveTo(endPoint);
                return;
            }

            var start = _currentPoint;
            _currentFigureSegments?.Add(RawSegment.Cubic(start, secondControlPoint, thirdControlPoint, endPoint));
            AppendFlattenedCubicPoints(_currentFigure, start, secondControlPoint, thirdControlPoint, endPoint);
            _currentPoint = endPoint;
        }

        public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness) { }

        public bool Contains(Vector2 point)
        {
            var crossings = 0;
            for (var ci = 0; ci < _contours.Count; ci++)
            {
                var points = _contours[ci].Points;
                for (var i = 0; i < points.Length; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Length];
                    var intersects = ((a.Y > point.Y) != (b.Y > point.Y))
                                     && point.X < ((b.X - a.X) * (point.Y - a.Y) / ((b.Y - a.Y) + 1e-6f)) + a.X;
                    if (intersects)
                        crossings++;
                }
            }

            return (crossings & 1) == 1;
        }

        public void ComputeUnsignedDistances(Vector2 point, out float dr, out float dg, out float db)
        {
            dr = float.MaxValue;
            dg = float.MaxValue;
            db = float.MaxValue;
            var any = float.MaxValue;
            for (var i = 0; i < _edges.Count; i++)
            {
                var e = _edges[i];
                var d = e.Segment.DistanceTo(point);
                any = MathF.Min(any, d);
                if ((e.ChannelMask & 1) != 0)
                    dr = MathF.Min(dr, d);
                if ((e.ChannelMask & 2) != 0)
                    dg = MathF.Min(dg, d);
                if ((e.ChannelMask & 4) != 0)
                    db = MathF.Min(db, d);
            }

            if (dr == float.MaxValue)
                dr = any;
            if (dg == float.MaxValue)
                dg = any;
            if (db == float.MaxValue)
                db = any;
        }

        private void AddLineTo(Vector2 point)
        {
            if (!_hasCurrentPoint)
            {
                MoveTo(point);
                return;
            }

            var a = _currentPoint;
            var b = point;
            _currentPoint = point;
            _currentFigure?.Add(point);
            _currentFigureSegments?.Add(RawSegment.Line(a, b));
        }

        private void AddContourEdgesWithCornerColoring(List<RawSegment> segments)
        {
            if (segments.Count == 0)
                return;

            var n = segments.Count;
            var dirs = new Vector2[n];
            var valid = new bool[n];
            for (var i = 0; i < n; i++)
            {
                if (!segments[i].TryStartTangent(out var tangent))
                    continue;
                dirs[i] = tangent;
                valid[i] = true;
            }

            var corners = new bool[n];
            var cornerCount = 0;
            for (var i = 0; i < n; i++)
            {
                if (!valid[i])
                    continue;
                var prev = PreviousValidEdge(i, valid);
                if (prev < 0)
                    continue;
                if (!IsCorner(dirs[prev], dirs[i]))
                    continue;
                corners[i] = true;
                cornerCount++;
            }

            var contourStartEdge = _edges.Count;
            var color = 1;
            var seeded = false;
            var idxSinceLastSwitch = 0;
            var periodicSwitch = Math.Max(1, CountValid(valid) / 3);
            for (var i = 0; i < n; i++)
            {
                if (!valid[i])
                    continue;

                if (!seeded)
                {
                    seeded = true;
                }
                else
                {
                    var shouldSwitch = cornerCount > 0 ? corners[i] : idxSinceLastSwitch >= periodicSwitch;
                    if (shouldSwitch)
                    {
                        color = NextColor(color);
                        idxSinceLastSwitch = 0;
                    }
                }

                _edges.Add(new ColoredSegment(segments[i], color));
                idxSinceLastSwitch++;
            }

            var contourEdgeCount = _edges.Count - contourStartEdge;
            if (contourEdgeCount <= 1)
                return;

            var firstEdge = FindFirstValid(valid);
            var lastEdge = FindLastValid(valid);
            if (firstEdge >= 0 && lastEdge >= 0 &&
                IsCorner(dirs[lastEdge], dirs[firstEdge]) &&
                TryGetContourEdgeRange(contourStartEdge, contourEdgeCount, out var firstIdx, out var lastIdx) &&
                _edges[firstIdx].ChannelMask == _edges[lastIdx].ChannelMask)
            {
                var e = _edges[firstIdx];
                _edges[firstIdx] = e with { ChannelMask = NextColor(e.ChannelMask) };
            }
        }

        private bool TryGetContourEdgeRange(int contourStartEdge, int contourEdgeCount, out int firstIdx, out int lastIdx)
        {
            firstIdx = -1;
            lastIdx = -1;
            if (contourEdgeCount <= 0 || contourStartEdge < 0)
                return false;
            firstIdx = contourStartEdge;
            lastIdx = contourStartEdge + contourEdgeCount - 1;
            return firstIdx >= 0 && lastIdx < _edges.Count && firstIdx <= lastIdx;
        }

        private static int FindFirstValid(bool[] valid)
        {
            for (var i = 0; i < valid.Length; i++)
                if (valid[i])
                    return i;
            return -1;
        }

        private static int CountValid(bool[] valid)
        {
            var c = 0;
            for (var i = 0; i < valid.Length; i++)
            {
                if (valid[i])
                    c++;
            }

            return c;
        }

        private static int FindLastValid(bool[] valid)
        {
            for (var i = valid.Length - 1; i >= 0; i--)
                if (valid[i])
                    return i;
            return -1;
        }

        private static int PreviousValidEdge(int i, bool[] valid)
        {
            for (var p = i - 1; p >= 0; p--)
                if (valid[p])
                    return p;
            for (var p = valid.Length - 1; p > i; p--)
                if (valid[p])
                    return p;
            return -1;
        }

        private static bool IsCorner(Vector2 prevDir, Vector2 nextDir)
        {
            var dot = Math.Clamp(Vector2.Dot(prevDir, nextDir), -1f, 1f);
            var angle = MathF.Acos(dot);
            return angle > CornerAngleThresholdRadians;
        }

        private static int NextColor(int mask) =>
            mask switch
            {
                1 => 2,
                2 => 4,
                _ => 1
            };

        private static void AppendFlattenedQuadraticPoints(List<Vector2>? figure, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            if (figure is null)
                return;
            var chord = Vector2.Distance(p0, p2);
            var ctrlBias = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2);
            var steps = Math.Clamp((int)MathF.Ceiling((chord + ctrlBias) * 0.3f), 20, 96);
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                figure.Add(EvalQuadratic(p0, p1, p2, t));
            }
        }

        private static void AppendFlattenedCubicPoints(List<Vector2>? figure, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            if (figure is null)
                return;
            var chord = Vector2.Distance(p0, p3);
            var ctrlBias = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
            var steps = Math.Clamp((int)MathF.Ceiling((chord + ctrlBias) * 0.3f), 28, 128);
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                figure.Add(EvalCubic(p0, p1, p2, p3, t));
            }
        }

        private static bool NearlyEqual(Vector2 a, Vector2 b) =>
            MathF.Abs(a.X - b.X) < 0.001f && MathF.Abs(a.Y - b.Y) < 0.001f;

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = Vector2.Dot(ab, ab);
            if (lenSq < 1e-8f)
                return Vector2.Distance(p, a);
            var t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            var q = a + ab * t;
            return Vector2.Distance(p, q);
        }

        private static Vector2 EvalQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            var omt = 1f - t;
            return p0 * (omt * omt) + p1 * (2f * omt * t) + p2 * (t * t);
        }

        private static Vector2 EvalQuadraticDeriv(Vector2 p0, Vector2 p1, Vector2 p2, float t) =>
            (p1 - p0) * (2f * (1f - t)) + (p2 - p1) * (2f * t);

        private static Vector2 EvalQuadraticSecondDeriv(Vector2 p0, Vector2 p1, Vector2 p2) =>
            (p2 - 2f * p1 + p0) * 2f;

        private static Vector2 EvalCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var omt = 1f - t;
            return p0 * (omt * omt * omt)
                   + p1 * (3f * omt * omt * t)
                   + p2 * (3f * omt * t * t)
                   + p3 * (t * t * t);
        }

        private static Vector2 EvalCubicDeriv(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var omt = 1f - t;
            return (p1 - p0) * (3f * omt * omt)
                   + (p2 - p1) * (6f * omt * t)
                   + (p3 - p2) * (3f * t * t);
        }

        private static Vector2 EvalCubicSecondDeriv(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var omt = 1f - t;
            return (p2 - 2f * p1 + p0) * (6f * omt)
                   + (p3 - 2f * p2 + p1) * (6f * t);
        }

        private static float DistanceToQuadratic(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            var bestT = 0f;
            var bestDistSq = float.MaxValue;
            const int samples = 24;
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var q = EvalQuadratic(p0, p1, p2, t);
                var d2 = Vector2.DistanceSquared(p, q);
                if (d2 < bestDistSq)
                {
                    bestDistSq = d2;
                    bestT = t;
                }
            }

            var dd = EvalQuadraticSecondDeriv(p0, p1, p2);
            var tRefined = bestT;
            for (var i = 0; i < 8; i++)
            {
                var q = EvalQuadratic(p0, p1, p2, tRefined);
                var d1 = EvalQuadraticDeriv(p0, p1, p2, tRefined);
                var r = q - p;
                var g = Vector2.Dot(r, d1);
                var gp = Vector2.Dot(d1, d1) + Vector2.Dot(r, dd);
                if (MathF.Abs(gp) < 1e-6f)
                    break;
                tRefined = Math.Clamp(tRefined - g / gp, 0f, 1f);
            }

            var qq = EvalQuadratic(p0, p1, p2, tRefined);
            return Vector2.Distance(p, qq);
        }

        private static float DistanceToCubic(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            var bestT = 0f;
            var bestDistSq = float.MaxValue;
            const int samples = 40;
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var q = EvalCubic(p0, p1, p2, p3, t);
                var d2 = Vector2.DistanceSquared(p, q);
                if (d2 < bestDistSq)
                {
                    bestDistSq = d2;
                    bestT = t;
                }
            }

            var tRefined = bestT;
            for (var i = 0; i < 10; i++)
            {
                var q = EvalCubic(p0, p1, p2, p3, tRefined);
                var d1 = EvalCubicDeriv(p0, p1, p2, p3, tRefined);
                var d2 = EvalCubicSecondDeriv(p0, p1, p2, p3, tRefined);
                var r = q - p;
                var g = Vector2.Dot(r, d1);
                var gp = Vector2.Dot(d1, d1) + Vector2.Dot(r, d2);
                if (MathF.Abs(gp) < 1e-6f)
                    break;
                tRefined = Math.Clamp(tRefined - g / gp, 0f, 1f);
            }

            var qq = EvalCubic(p0, p1, p2, p3, tRefined);
            return Vector2.Distance(p, qq);
        }

        private enum RawSegmentKind
        {
            Line,
            Quadratic,
            Cubic
        }

        private readonly record struct ColoredSegment(RawSegment Segment, int ChannelMask);
        private readonly record struct RawSegment(RawSegmentKind SegmentKind, Vector2 P0, Vector2 P1, Vector2 P2, Vector2 P3)
        {
            public static RawSegment Line(Vector2 a, Vector2 b) => new(RawSegmentKind.Line, a, b, default, default);
            public static RawSegment Quadratic(Vector2 a, Vector2 c, Vector2 b) => new(RawSegmentKind.Quadratic, a, c, b, default);
            public static RawSegment Cubic(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b) => new(RawSegmentKind.Cubic, a, c1, c2, b);

            public bool TryStartTangent(out Vector2 tangent)
            {
                tangent = default;
                Vector2 t = SegmentKind switch
                {
                    RawSegmentKind.Line => P1 - P0,
                    RawSegmentKind.Quadratic => (P1 - P0) * 2f,
                    _ => (P1 - P0) * 3f
                };
                if (t.LengthSquared() < 1e-8f)
                    t = SegmentKind switch
                    {
                        RawSegmentKind.Line => P1 - P0,
                        RawSegmentKind.Quadratic => P2 - P0,
                        _ => P3 - P0
                    };
                if (t.LengthSquared() < 1e-8f)
                    return false;
                tangent = Vector2.Normalize(t);
                return true;
            }

            public float DistanceTo(Vector2 p) =>
                SegmentKind switch
                {
                    RawSegmentKind.Line => DistanceToSegment(p, P0, P1),
                    RawSegmentKind.Quadratic => DistanceToQuadratic(p, P0, P1, P2),
                    _ => DistanceToCubic(p, P0, P1, P2, P3)
                };
        }

        private readonly record struct Contour(Vector2[] Points);
    }
}
