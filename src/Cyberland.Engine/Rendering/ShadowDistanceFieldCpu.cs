using System.Threading.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// CPU reference signed-distance-field builder for shadow occluders. Used by tests as the ground-truth oracle for the
/// GPU JFA pipeline. Not invoked per-frame at runtime; the production path runs entirely on the GPU.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate space.</b> Inputs are <see cref="ShadowOccluder2D"/> in <b>world</b> (+Y up) and a <see cref="ShadowSdfCamera"/>
/// that holds every transform constant. Output is a 2D float array indexed by <b>SDF texel</b> coordinates (+Y down),
/// with distances stored in <b>SDF texels</b> (negative inside an occluder, positive outside). To convert to swapchain
/// pixels, call <see cref="ShadowSdfCamera.SdfPxDistanceToSwapchainPx(float)"/>.
/// </para>
/// <para>
/// <b>Why brute-force.</b> O(width × height × N occluders) is fine for tests and the SDF is small; this exists so unit
/// tests can prove the algorithm without invoking Vulkan. The GPU path uses JFA to produce the same result in O(log N)
/// passes.
/// </para>
/// <para>Pure function; safe to invoke from <see cref="Core.Ecs.IParallelSystem"/> workers.</para>
/// </remarks>
internal static class ShadowDistanceFieldCpu
{
    private static readonly ParallelOptions _defaultParallelOptions = new();
    /// <summary>
    /// Builds a signed distance field over the camera's SDF texel grid. Returned span has length
    /// <c>cam.SdfSizePx.X * cam.SdfSizePx.Y</c>; index <c>y * width + x</c>.
    /// </summary>
    /// <param name="occluders">World-space OBBs.</param>
    /// <param name="cam">Per-frame camera + SDF mapping snapshot.</param>
    /// <param name="options">
    /// Optional parallelism control. Rows are processed via
    /// <see cref="Parallel.For(int, int, ParallelOptions, System.Action{int})"/>.
    /// Tests pass <c>MaxDegreeOfParallelism = 1</c> for determinism.
    /// </param>
    public static float[] Build(
        ReadOnlySpan<ShadowOccluder2D> occluders,
        in ShadowSdfCamera cam,
        ParallelOptions? options = null)
    {
        float[]? scratch = null;
        return Build(occluders, in cam, ref scratch, options);
    }

    /// <summary>
    /// Pooled overload: reuses <paramref name="scratch"/> when large enough, replacing it with a new (or same)
    /// array. Callers that invoke <c>Build</c> repeatedly (e.g. per-frame CPU validation) should keep a
    /// <c>float[]?</c> field and pass it here to avoid per-call allocations.
    /// </summary>
    public static float[] Build(
        ReadOnlySpan<ShadowOccluder2D> occluders,
        in ShadowSdfCamera cam,
        ref float[]? scratch,
        ParallelOptions? options = null)
    {
        var sdfSize = cam.SdfSizePx;
        var width = sdfSize.X;
        var height = sdfSize.Y;
        var required = width * height;
        if (scratch is null || scratch.Length < required)
            scratch = new float[required];
        var sdf = scratch;

        // Spans can't cross thread boundaries — snapshot to an array for the parallel closure.
        var occluderArray = occluders.ToArray();

        // When occluders are present the GPU's jfa_to_sdf.frag.glsl uses max(sdfW, sdfH) for
        // seedless texels; when the SDF is entirely empty EnsureShadowSdfFullyLit clears to 65500.
        var emptyRegionSentinelSdfPx = occluderArray.Length > 0
            ? (float)System.Math.Max(width, height)
            : DeferredRenderingConstants.ShadowSdfFullyLitSentinelTexels;

        var positiveInfinity = float.PositiveInfinity;
        var sdfScale = cam.SdfScale;
        var physicalScale = cam.PhysicalScale;
        var camSnapshot = cam;

        // Small SDFs (< 32 rows) are cheaper to process serially than to dispatch across the thread pool.
        var effectiveOptions = height < 32
            ? new ParallelOptions { MaxDegreeOfParallelism = 1 }
            : options ?? _defaultParallelOptions;
        Parallel.For(0, height, effectiveOptions, ySdfPx =>
        {
            for (var xSdfPx = 0; xSdfPx < width; xSdfPx++)
            {
                // (xSdfPx, ySdfPx) is in SDF-texel space (+Y down). Convert to swapchain px, then to world.
                var sampleSwapchainPx = new Vector2D<float>(
                    (xSdfPx + 0.5f) / sdfScale,
                    (ySdfPx + 0.5f) / sdfScale);
                var sampleWorld = camSnapshot.SwapchainPxToWorld(sampleSwapchainPx);

                // Track the smallest signed distance across all occluders. Inside-an-OBB SDFs are negative; the
                // minimum across boxes naturally picks the deepest penetration (most negative) when overlapping,
                // and the nearest edge when outside all boxes.
                var bestSignedWorld = positiveInfinity;
                for (var oi = 0; oi < occluderArray.Length; oi++)
                {
                    var occluder = occluderArray[oi];
                    var edgeDistanceWorld = SignedDistanceToObbWorld(sampleWorld, in occluder);
                    if (edgeDistanceWorld < bestSignedWorld)
                        bestSignedWorld = edgeDistanceWorld;
                }

                // Store as SDF texels (matches the GPU R16F output that the shader samples).
                // Seedless texels get the sentinel directly — no world-to-sdfPx conversion needed.
                if (bestSignedWorld == positiveInfinity)
                {
                    sdf[ySdfPx * width + xSdfPx] = emptyRegionSentinelSdfPx;
                }
                else
                {
                    sdf[ySdfPx * width + xSdfPx] = bestSignedWorld * physicalScale * sdfScale;
                }
            }
        });

        return sdf;
    }

    /// <summary>
    /// Bilinearly samples the SDF at a fractional SDF-texel coordinate (+Y down). Out-of-bounds samples clamp.
    /// </summary>
    public static float Sample(ReadOnlySpan<float> sdf, Vector2D<int> sdfSizePx, Vector2D<float> sampleSdfPx)
    {
        var width = sdfSizePx.X;
        var height = sdfSizePx.Y;
        if (width <= 0 || height <= 0)
            return 0f;

        var fx = MathF.Max(0f, MathF.Min(width - 1f, sampleSdfPx.X - 0.5f));
        var fy = MathF.Max(0f, MathF.Min(height - 1f, sampleSdfPx.Y - 0.5f));
        var x0 = (int)MathF.Floor(fx);
        var y0 = (int)MathF.Floor(fy);
        var x1 = System.Math.Min(width - 1, x0 + 1);
        var y1 = System.Math.Min(height - 1, y0 + 1);
        var tx = fx - x0;
        var ty = fy - y0;

        var v00 = sdf[y0 * width + x0];
        var v10 = sdf[y0 * width + x1];
        var v01 = sdf[y1 * width + x0];
        var v11 = sdf[y1 * width + x1];
        var top = v00 + (v10 - v00) * tx;
        var bot = v01 + (v11 - v01) * tx;
        return top + (bot - top) * ty;
    }

    /// <summary>
    /// World-space signed distance from <paramref name="pointWorld"/> to the rotated OBB edge. Negative inside,
    /// positive outside. World units = world pixels.
    /// </summary>
    internal static float SignedDistanceToObbWorld(Vector2D<float> pointWorld, in ShadowOccluder2D obb)
    {
        var dx = pointWorld.X - obb.CenterWorld.X;
        var dy = pointWorld.Y - obb.CenterWorld.Y;
        var c = MathF.Cos(-obb.RotationRadians);
        var s = MathF.Sin(-obb.RotationRadians);
        var lx = dx * c - dy * s;
        var ly = dx * s + dy * c;
        var hx = MathF.Max(obb.HalfExtentsWorld.X, 0f);
        var hy = MathF.Max(obb.HalfExtentsWorld.Y, 0f);
        var ax = MathF.Abs(lx) - hx;
        var ay = MathF.Abs(ly) - hy;
        if (ax <= 0f && ay <= 0f)
            return MathF.Max(ax, ay);
        var qx = MathF.Max(ax, 0f);
        var qy = MathF.Max(ay, 0f);
        var outside = MathF.Sqrt(qx * qx + qy * qy);
        var insideEdge = MathF.Min(MathF.Max(ax, ay), 0f);
        return outside + insideEdge;
    }
}
