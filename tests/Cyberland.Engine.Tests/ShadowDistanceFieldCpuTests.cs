using System.Threading.Tasks;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class ShadowDistanceFieldCpuTests
{
    private static ShadowSdfCamera SmallCamera() => ShadowSdfCamera.SyntheticCamera(
        cameraPosWorld: new Vector2D<float>(64f, 36f),
        cameraRotRadians: 0f,
        viewportSizeWorld: new Vector2D<int>(128, 72),
        swapchainSizePx: new Vector2D<int>(128, 72),
        sdfScale: 1f);

    [Fact]
    public void Build_empty_occluders_yields_large_positive_distance()
    {
        var cam = SmallCamera();
        var sdf = ShadowDistanceFieldCpu.Build(Array.Empty<ShadowOccluder2D>(), in cam);
        Assert.Equal(128 * 72, sdf.Length);
        Assert.True(sdf[0] >= 65500f);
    }

    [Fact]
    public void Build_central_occluder_is_negative_inside_and_positive_outside()
    {
        var cam = SmallCamera();
        // 8×8 box at the camera center (world Y = 36).
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(64f, 36f),
            new Vector2D<float>(8f, 8f),
            0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);
        // World (64, 36) maps to swapchain (64, 36) which is texel (64, 36).
        // The SDF stores +Y down, so the center of the box is at SDF (64, 36) — texel index 36*128 + 64 = 4672.
        Assert.True(sdf[36 * 128 + 64] < 0f, "interior should be negative");
        // Far corner: world (0, 72) → swapchain (0, 0) (since world +Y up → viewport +Y down).
        Assert.True(sdf[0] > 0f, "exterior should be positive");
    }

    [Fact]
    public void Sample_returns_bilinear_interpolated_value_inside_grid()
    {
        var sdf = new float[]
        {
            1f, 2f, 3f, 4f,
            5f, 6f, 7f, 8f,
            9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f,
        };
        var sdfSize = new Vector2D<int>(4, 4);
        // Center texel (1, 1) = value 6. Sample at the texel center should give 6.
        var center = ShadowDistanceFieldCpu.Sample(sdf, sdfSize, new Vector2D<float>(1.5f, 1.5f));
        Assert.Equal(6f, center, 3);

        // Sample at corner clamps inside.
        var corner = ShadowDistanceFieldCpu.Sample(sdf, sdfSize, new Vector2D<float>(-10f, -10f));
        Assert.Equal(1f, corner, 3);
    }

    [Fact]
    public void Sample_returns_zero_for_zero_sized_grid()
    {
        var sdf = Array.Empty<float>();
        var size = new Vector2D<int>(0, 0);
        Assert.Equal(0f, ShadowDistanceFieldCpu.Sample(sdf, size, new Vector2D<float>(10f, 10f)));
    }

    [Fact]
    public void SignedDistanceToObbWorld_returns_negative_inside()
    {
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(10f, 10f),
            0f);
        var d = ShadowDistanceFieldCpu.SignedDistanceToObbWorld(new Vector2D<float>(0f, 0f), in occluder);
        Assert.True(d < 0f);
    }

    [Fact]
    public void SignedDistanceToObbWorld_returns_distance_outside()
    {
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(10f, 10f),
            0f);
        // 30 pixels right of the right edge.
        var d = ShadowDistanceFieldCpu.SignedDistanceToObbWorld(new Vector2D<float>(40f, 0f), in occluder);
        Assert.Equal(30f, d, 3);
    }

    [Fact]
    public void SignedDistanceToObbWorld_outside_corner_is_euclidean()
    {
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(10f, 10f),
            0f);
        var d = ShadowDistanceFieldCpu.SignedDistanceToObbWorld(new Vector2D<float>(13f, 14f), in occluder);
        Assert.Equal(5f, d, 3);
    }

    [Fact]
    public void Build_handles_inside_occluder_by_taking_deepest_penetration()
    {
        var cam = SmallCamera();
        // Two overlapping occluders at the center; ensure the inner-branch path (and its bestEdgeWorld continue branch)
        // executes without leaving NaN/positive distances at the center.
        var a = new ShadowOccluder2D(new Vector2D<float>(64f, 36f), new Vector2D<float>(20f, 20f), 0f);
        var b = new ShadowOccluder2D(new Vector2D<float>(64f, 36f), new Vector2D<float>(5f, 5f), 0f);
        var sdf = ShadowDistanceFieldCpu.Build(new[] { a, b }, in cam);
        Assert.True(sdf[36 * 128 + 64] < 0f);
    }

    [Fact]
    public void Build_rotated_occluder_reports_negative_inside_and_positive_outside()
    {
        var cam = SmallCamera();
        // 10×10 box at the camera center, rotated 45° CCW.
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(64f, 36f),
            new Vector2D<float>(10f, 10f),
            MathF.PI / 4f);

        // Sanity-check the OBB containment helper agrees with expectations.
        Assert.True(occluder.ContainsPointWorld(new Vector2D<float>(64f, 36f)), "center should be inside rotated OBB");
        Assert.False(occluder.ContainsPointWorld(new Vector2D<float>(64f + 20f, 36f)), "20 px right should be outside");

        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        // Center texel: SDF should be negative (inside the rotated box).
        Assert.True(sdf[36 * 128 + 64] < 0f, "center of rotated OBB should be negative in SDF");

        // A point well outside the rotated box should be positive.
        Assert.True(sdf[0] > 0f, "far corner should be positive");
    }

    [Fact]
    public void Rotated_Occluder_Pi_Over_3_Negative_Inside_Positive_Outside()
    {
        var cam = SmallCamera();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(64f, 36f),
            new Vector2D<float>(10f, 10f),
            MathF.PI / 3f);

        Assert.True(occluder.ContainsPointWorld(new Vector2D<float>(64f, 36f)), "center should be inside rotated OBB");

        var sdf = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        Assert.True(sdf[36 * 128 + 64] < 0f, "center of π/3-rotated OBB should be negative in SDF");
        Assert.True(sdf[0] > 0f, "far corner should be positive");

        var dCenter = ShadowDistanceFieldCpu.SignedDistanceToObbWorld(new Vector2D<float>(64f, 36f), in occluder);
        Assert.True(dCenter < 0f, "SignedDistance at center should be negative (inside)");

        var dFar = ShadowDistanceFieldCpu.SignedDistanceToObbWorld(new Vector2D<float>(0f, 0f), in occluder);
        Assert.True(dFar > 0f, "SignedDistance far away should be positive (outside)");
    }

    [Fact]
    public void Build_pooled_overload_reuses_scratch_and_matches_nonpooled()
    {
        var cam = SmallCamera();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(64f, 36f),
            new Vector2D<float>(8f, 8f),
            0f);
        var expected = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam);

        float[]? scratch = null;
        var opts = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 1 };
        var result1 = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam, ref scratch, opts);
        Assert.NotNull(scratch);
        Assert.Equal(expected.Length, result1.Length);
        for (var i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], result1[i], 3);

        // Second call reuses the same scratch array (pooled path).
        var prevScratch = scratch;
        var result2 = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam, ref scratch, opts);
        Assert.Same(prevScratch, scratch);
        Assert.Equal(expected.Length, result2.Length);
    }

    [Fact]
    public void SignedDistanceToObbWorld_handles_rotation()
    {
        // 10×10 half-extents, rotated pi/4.
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(10f, 10f),
            MathF.PI / 4f);

        // Center is inside regardless of rotation.
        var dCenter = ShadowDistanceFieldCpu.SignedDistanceToObbWorld(new Vector2D<float>(0f, 0f), in occluder);
        Assert.True(dCenter < 0f, "center should be inside");

        // Along the diagonal (world +X+Y), the rotated box extends farther than an AABB's edge at 10.
        // The diagonal of the rotated box in world space reaches ~14.14 along (1,1).
        // A point at (12, 0) should be inside the rotated box (the rotated local-X axis aligns with world (1,1)).
        var dInsideDiag = ShadowDistanceFieldCpu.SignedDistanceToObbWorld(new Vector2D<float>(12f, 0f), in occluder);
        Assert.True(dInsideDiag < 0f, "point along rotated axis should be inside");

        // A point far away should be positive.
        var dFar = ShadowDistanceFieldCpu.SignedDistanceToObbWorld(new Vector2D<float>(30f, 30f), in occluder);
        Assert.True(dFar > 0f, "point far from rotated OBB should be positive");
    }

    /// <summary>
    /// P2-10: Verifies that a CPU JFA (mirroring the GPU jfa_init/jfa_step/jfa_to_sdf pipeline) produces an SDF
    /// that agrees with the brute-force <see cref="ShadowDistanceFieldCpu.Build"/> oracle within sub-texel tolerance
    /// for an axis-aligned box at SdfScale=1.
    /// </summary>
    [Fact]
    public void CpuJfa_matches_bruteforce_sdf_within_subtexel_tolerance()
    {
        var cam = SmallCamera();
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(64f, 36f),
            new Vector2D<float>(12f, 8f),
            0f);
        var opts = new ParallelOptions { MaxDegreeOfParallelism = 1 };
        var bruteForce = ShadowDistanceFieldCpu.Build(new[] { occluder }, in cam, opts);

        var w = cam.SdfSizePx.X;
        var h = cam.SdfSizePx.Y;

        // Step 1: Build the occluder mask. For each texel, check if its world-space center is inside the OBB.
        var mask = new bool[w * h];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var swPx = new Vector2D<float>((x + 0.5f) / cam.SdfScale, (y + 0.5f) / cam.SdfScale);
            var world = cam.SwapchainPxToWorld(swPx);
            if (ShadowDistanceFieldCpu.SignedDistanceToObbWorld(world, in occluder) <= 0f)
                mask[y * w + x] = true;
        }

        // Step 2: JFA Init — mirrors jfa_init.frag.glsl with floor(vUv * size) texel-center alignment.
        var seedX = new float[w * h];
        var seedY = new float[w * h];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var idx = y * w + x;
            if (mask[idx])
            {
                seedX[idx] = x;
                seedY[idx] = y;
            }
            else
            {
                seedX[idx] = -1e30f;
                seedY[idx] = -1e30f;
            }
        }

        // Step 3: JFA Steps — mirrors jfa_step.frag.glsl. Step sizes halve from max(w,h)/2 down to 1.
        var maxDim = Math.Max(w, h);
        var step = 1;
        while (step < maxDim) step <<= 1;
        step >>= 1;

        var tmpX = new float[w * h];
        var tmpY = new float[w * h];
        while (step >= 1)
        {
            Array.Copy(seedX, tmpX, seedX.Length);
            Array.Copy(seedY, tmpY, seedY.Length);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var idx = y * w + x;
                var bestDist2 = float.MaxValue;
                var bestX = seedX[idx];
                var bestY = seedY[idx];
                if (bestX > -1e20f)
                    bestDist2 = (bestX - x) * (bestX - x) + (bestY - y) * (bestY - y);

                for (var oy = -1; oy <= 1; oy++)
                for (var ox = -1; ox <= 1; ox++)
                {
                    var nx = x + ox * step;
                    var ny = y + oy * step;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    var ni = ny * w + nx;
                    var sx = seedX[ni];
                    var sy = seedY[ni];
                    if (sx < -1e20f) continue;
                    var dist2 = (sx - x) * (sx - x) + (sy - y) * (sy - y);
                    if (dist2 < bestDist2)
                    {
                        bestDist2 = dist2;
                        bestX = sx;
                        bestY = sy;
                    }
                }

                tmpX[idx] = bestX;
                tmpY[idx] = bestY;
            }

            Array.Copy(tmpX, seedX, seedX.Length);
            Array.Copy(tmpY, seedY, seedY.Length);
            step >>= 1;
        }

        // Step 4: Convert to signed distance — mirrors jfa_to_sdf.frag.glsl.
        var jfaSdf = new float[w * h];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var idx = y * w + x;
            float dist;
            if (seedX[idx] < -1e20f)
                dist = MathF.Max(w, h);
            else
                dist = MathF.Sqrt((seedX[idx] - x) * (seedX[idx] - x) + (seedY[idx] - y) * (seedY[idx] - y));
            // Sign from mask: negative inside, positive outside.
            jfaSdf[idx] = mask[idx] ? -dist : dist;
        }

        // Sign agreement: every texel should agree on inside vs outside. The JFA gives 0 inside
        // (distance to nearest seed = self) while the brute-force gives negative distance to edge,
        // but both have the same sign (≤ 0 = inside, > 0 = outside).
        var signDisagree = 0;
        for (var i = 0; i < w * h; i++)
        {
            var bfInside = bruteForce[i] <= 0f;
            var jfaInside = jfaSdf[i] <= 0f;
            if (bfInside != jfaInside) signDisagree++;
        }
        Assert.True(signDisagree == 0,
            $"JFA vs brute-force sign disagreement in {signDisagree} texels; expected 0 for axis-aligned box at SdfScale=1");

        // Exterior (positive) distance agreement: the JFA gives distance to nearest covered texel;
        // brute-force gives distance to continuous OBB edge. These differ by at most ~1 texel due to
        // discretization.
        var maxExteriorError = 0f;
        for (var i = 0; i < w * h; i++)
        {
            if (bruteForce[i] <= 0f) continue;
            var error = MathF.Abs(bruteForce[i] - jfaSdf[i]);
            if (error > maxExteriorError) maxExteriorError = error;
        }
        Assert.True(maxExteriorError < 1.5f,
            $"JFA vs brute-force exterior max error = {maxExteriorError:F4} SDF texels; expected < 1.5");
    }
}
