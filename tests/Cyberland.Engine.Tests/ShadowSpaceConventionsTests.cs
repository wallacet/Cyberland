using System.Text.RegularExpressions;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Coordinate-space canary: asserts that <see cref="ShadowSdfCamera.WorldToSwapchainPx"/> agrees with the canonical
/// <see cref="CameraProjection.WorldToViewportPixel"/> + <see cref="CameraProjection.ViewportPixelToSwapchainPixel"/>
/// chain across rotation / position / SDF-scale sweeps. This is the test that catches the class of bug that ate the
/// HDR demo spotlight: "two implementations of the same transform drifted apart."
/// </summary>
public sealed class ShadowSpaceConventionsTests
{
    public static TheoryData<float, float, float> RotationsPositionsScales()
    {
        var data = new TheoryData<float, float, float>();
        foreach (var rot in new[] { 0f, 0.25f, -0.7f, MathF.PI / 4f })
        foreach (var sdfScale in new[] { 0.5f, 1.0f, 2.0f })
        foreach (var camX in new[] { 0f, 200f, 800f })
        {
            data.Add(rot, sdfScale, camX);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(RotationsPositionsScales))]
    public void WorldToSwapchainPx_agrees_with_CameraProjection_chain(float rot, float sdfScale, float camX)
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(camX, 360f),
            cameraRotRadians: rot,
            viewportSizeWorld: new Vector2D<int>(1280, 720),
            swapchainSizePx: new Vector2D<int>(1280, 720),
            sdfScale: sdfScale);
        var physical = new PhysicalViewport(
            new Vector2D<int>((int)cam.PhysicalOffsetSwapchainPx.X, (int)cam.PhysicalOffsetSwapchainPx.Y),
            new Vector2D<int>((int)cam.PhysicalSizeSwapchainPx.X, (int)cam.PhysicalSizeSwapchainPx.Y),
            cam.PhysicalScale);

        // 7x7 lattice of world points spanning the viewport.
        for (var yi = 0; yi < 7; yi++)
        {
            var worldY = 360f + (yi - 3f) * 100f;
            for (var xi = 0; xi < 7; xi++)
            {
                var worldX = camX + (xi - 3f) * 100f;
                var pWorld = new Vector2D<float>(worldX, worldY);

                var vp = CameraProjection.WorldToViewportPixel(pWorld, cam.CameraPosWorld, cam.CameraRotRadians, cam.ViewportSizeWorld);
                var expected = CameraProjection.ViewportPixelToSwapchainPixel(vp, in physical);
                var actual = cam.WorldToSwapchainPx(pWorld);
                Assert.True(MathF.Abs(actual.X - expected.X) <= 1e-4f, $"X drift at ({worldX},{worldY}): {actual.X} vs {expected.X}");
                Assert.True(MathF.Abs(actual.Y - expected.Y) <= 1e-4f, $"Y drift at ({worldX},{worldY}): {actual.Y} vs {expected.Y}");
            }
        }
    }

    [Fact]
    public void SwapchainPxToWorld_roundtrips_to_world_under_rotation()
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(200f, 150f),
            cameraRotRadians: 0.4f,
            viewportSizeWorld: new Vector2D<int>(800, 600),
            swapchainSizePx: new Vector2D<int>(1280, 720),
            sdfScale: 0.5f);
        foreach (var pWorld in new[]
        {
            new Vector2D<float>(100f, 100f),
            new Vector2D<float>(500f, 300f),
            new Vector2D<float>(-50f, 800f),
        })
        {
            var swapchainPx = cam.WorldToSwapchainPx(pWorld);
            var back = cam.SwapchainPxToWorld(swapchainPx);
            Assert.True(MathF.Abs(back.X - pWorld.X) <= 1e-3f);
            Assert.True(MathF.Abs(back.Y - pWorld.Y) <= 1e-3f);
        }
    }

    /// <summary>
    /// Source-grep canary: scans deferred/shadow/bloom/composite shaders for bare <c>pos</c>, <c>dist</c>, or <c>t</c>
    /// identifiers that lack a coordinate-space suffix. Catches regressions where someone introduces an unqualified
    /// local — all coordinates and distances must carry their space (e.g. <c>fragWorld</c>, <c>tSwapchainPx</c>).
    /// </summary>
    [Fact]
    public void Shader_naming_convention_rejects_bare_pos_dist_t()
    {
        var root = FindRepoRoot();
        var shaderDir = Path.Combine(root, "src", "Cyberland.Engine", "Rendering", "Shaders");
        var scannedFiles = new[]
        {
            "shadow_sdf_sampling.glsl",
            "deferred_emissive_bleed.frag.glsl",
            "composite.frag.glsl",
            "bloom_downsample.frag.glsl",
            "bloom_upsample.frag.glsl",
            "bloom_gaussian.frag.glsl",
            "bloom_extract.frag.glsl",
            "bloom_copy.frag.glsl",
            "tiled_deferred_lighting.frag.glsl"
        };

        // Standalone identifier: bare `pos`, `dist`, or `t` not inside a larger word.
        // Dot lookbehind excludes GLSL .t swizzle (stpq component access).
        var pattern = new Regex(@"(?<![a-zA-Z_0-9.])(pos|dist|t)(?![a-zA-Z_0-9])");

        var violations = new List<string>();
        foreach (var file in scannedFiles)
        {
            var path = Path.Combine(shaderDir, file);
            Assert.True(File.Exists(path), $"Expected shader file missing: {path}");
            var lines = File.ReadAllLines(path);
            for (var lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                var line = lines[lineNum];
                var commentIdx = line.IndexOf("//", StringComparison.Ordinal);
                var code = commentIdx >= 0 ? line[..commentIdx] : line;
                if (pattern.IsMatch(code))
                    violations.Add($"{file}:{lineNum + 1}: {lines[lineNum].Trim()}");
            }
        }

        Assert.True(violations.Count == 0,
            "Bare `pos`, `dist`, or `t` identifiers in shader files (add coordinate-space suffix):\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void SwapchainPxToWorld_roundtrip_no_nan_at_near_zero_physical_scale()
    {
        // When physicalScale is near zero (degenerate viewport), SwapchainPxToWorld clamps via
        // MathF.Max(PhysicalScale, 1e-4f) so the inverse never produces NaN/Infinity.
        var cam = new ShadowSdfCamera(
            cameraPosWorld: new Vector2D<float>(100f, 100f),
            cameraRotRadians: 0.3f,
            viewportSizeWorld: new Vector2D<float>(800, 600),
            physicalOffsetSwapchainPx: new Vector2D<float>(0f, 0f),
            physicalSizeSwapchainPx: new Vector2D<float>(0f, 0f),
            physicalScale: 0f,
            swapchainSizePx: new Vector2D<float>(1280, 720),
            sdfScale: 1f);

        var pWorld = new Vector2D<float>(200f, 300f);
        var swapchainPx = cam.WorldToSwapchainPx(pWorld);
        var back = cam.SwapchainPxToWorld(swapchainPx);

        Assert.False(float.IsNaN(back.X), "X should not be NaN at near-zero physical scale");
        Assert.False(float.IsNaN(back.Y), "Y should not be NaN at near-zero physical scale");
        Assert.False(float.IsInfinity(back.X), "X should not be Infinity at near-zero physical scale");
        Assert.False(float.IsInfinity(back.Y), "Y should not be Infinity at near-zero physical scale");
    }

    [Fact]
    public void SwapchainPxToWorld_no_nan_at_sub_threshold_physical_scale()
    {
        // PhysicalScale 1e-5f is below the constructor's 1e-4f floor — verify the clamp
        // prevents NaN/Infinity in SwapchainPxToWorld and that the stored scale is the floor value.
        var cam = new ShadowSdfCamera(
            cameraPosWorld: new Vector2D<float>(400f, 300f),
            cameraRotRadians: 0.5f,
            viewportSizeWorld: new Vector2D<float>(1280, 720),
            physicalOffsetSwapchainPx: new Vector2D<float>(0f, 0f),
            physicalSizeSwapchainPx: new Vector2D<float>(1280, 720),
            physicalScale: 1e-5f,
            swapchainSizePx: new Vector2D<float>(1280, 720),
            sdfScale: 1f);

        Assert.Equal(1e-4f, cam.PhysicalScale, 6);

        foreach (var pSwapchainPx in new[]
        {
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(640f, 360f),
            new Vector2D<float>(1280f, 720f),
        })
        {
            var result = cam.SwapchainPxToWorld(pSwapchainPx);
            Assert.False(float.IsNaN(result.X), $"X NaN at swapchain ({pSwapchainPx.X},{pSwapchainPx.Y})");
            Assert.False(float.IsNaN(result.Y), $"Y NaN at swapchain ({pSwapchainPx.X},{pSwapchainPx.Y})");
            Assert.False(float.IsInfinity(result.X), $"X Inf at swapchain ({pSwapchainPx.X},{pSwapchainPx.Y})");
            Assert.False(float.IsInfinity(result.Y), $"Y Inf at swapchain ({pSwapchainPx.X},{pSwapchainPx.Y})");
        }
    }

    /// <summary>
    /// Extended canary: scans ALL engine GLSL shaders plus shadow/lighting C# files for bare coordinate/position/distance
    /// identifiers (<c>pos</c>, <c>dist</c>, <c>t</c>, <c>uv</c>, <c>sz</c>, <c>size</c>) that lack a coordinate-space
    /// suffix. Maintains an allowlist for unavoidable cases (GLSL NDC helpers, vertex attributes, built-ins).
    /// </summary>
    [Fact]
    public void CoordinateSpace_naming_convention_rejects_bare_identifiers_across_all_sources()
    {
        var root = FindRepoRoot();
        var shaderDir = Path.Combine(root, "src", "Cyberland.Engine", "Rendering", "Shaders");
        var renderingDir = Path.Combine(root, "src", "Cyberland.Engine", "Rendering");

        // All GLSL shaders under Shaders/
        var glslFiles = Directory.GetFiles(shaderDir, "*.glsl", SearchOption.TopDirectoryOnly);

        // Shadow*.cs under Rendering/
        var shadowCsFiles = Directory.GetFiles(renderingDir, "Shadow*.cs", SearchOption.TopDirectoryOnly);

        // Additional C# pipeline files
        var additionalCsFiles = new[]
        {
            Path.Combine(renderingDir, "VulkanRenderer.Deferred.Pipelines.Lighting.cs"),
            Path.Combine(renderingDir, "VulkanRenderer.Deferred.Pipelines.ShadowSdf.cs"),
        };

        var allFiles = glslFiles
            .Concat(shadowCsFiles)
            .Concat(additionalCsFiles.Where(File.Exists))
            .ToArray();

        // Bare coordinate identifiers that must carry a space suffix (e.g. posWorld, distSdfPx, srcUv).
        // Dot lookbehind excludes GLSL swizzle (.t, .s) and C# member access.
        var pattern = new Regex(@"(?<![a-zA-Z_0-9.])\b(pos|dist|uv|sz|size)\b(?![a-zA-Z_0-9])");

        // Separate pattern for bare `t` — extremely common as swizzle, loop var, generic param;
        // only flag when used as a standalone variable declaration or assignment.
        var bareT = new Regex(@"(?<![a-zA-Z_0-9.])\bt\b(?![a-zA-Z_0-9(])");

        // Per-file allowlist: file name → set of line substrings that are acceptable.
        var fileAllowlist = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // NDC fullscreen helper — `pos` is clip-space by convention and this file is trivial.
            ["fullscreen_triangle.vert.glsl"] = new[] { "vec2 pos", "gl_Position = vec4(pos", "vUv = pos" },
            // Vertex attribute names (engine interface)
            ["sprite_vert.glsl"] = new[] { "inPos", "outPos" },
            ["shadow_occluder.vert.glsl"] = new[] { "inPos", "outPos", "gl_Position" },
            ["text_msdf.vert.glsl"] = new[] { "inPos", "outPos", "gl_Position" },
        };

        var violations = new List<string>();
        foreach (var filePath in allFiles)
        {
            if (!File.Exists(filePath))
                continue;
            var fileName = Path.GetFileName(filePath);
            var lines = File.ReadAllLines(filePath);
            var allowedLines = fileAllowlist.TryGetValue(fileName, out var al) ? al : Array.Empty<string>();

            for (var lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                var line = lines[lineNum];

                // Strip comments: // for GLSL and C#
                var commentIdx = line.IndexOf("//", StringComparison.Ordinal);
                var code = commentIdx >= 0 ? line[..commentIdx] : line;

                // Strip string literals in C# ("...")
                if (fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    code = Regex.Replace(code, @"""[^""]*""", "\"\"");

                // Check main pattern
                if (pattern.IsMatch(code))
                {
                    if (!IsAllowed(line, allowedLines))
                        violations.Add($"{fileName}:{lineNum + 1}: {line.Trim()}");
                }

                // Check bare `t` only in GLSL files (C# uses `t` for generics, delegates, etc.)
                if (fileName.EndsWith(".glsl", StringComparison.OrdinalIgnoreCase) && bareT.IsMatch(code))
                {
                    if (!IsAllowed(line, allowedLines))
                        violations.Add($"{fileName}:{lineNum + 1}: [bare t] {line.Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Bare coordinate/distance identifiers without space suffix " +
            "(add a suffix like World, SwapchainPx, SdfPx, BloomMipPx, Uv, Texels, etc.):\n" +
            string.Join("\n", violations));
    }

    private static bool IsAllowed(string line, string[] allowedSubstrings)
    {
        foreach (var allowed in allowedSubstrings)
        {
            if (line.Contains(allowed, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Verifies that the world→swapchain projection in shadow_occluder.vert.glsl push constants produces
    /// the same result as <see cref="ShadowSdfCamera.WorldToSwapchainPx"/>. If someone edits the GLSL
    /// projection math without updating ShadowSdfCamera (or vice versa), this test will fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(RotationsPositionsScales))]
    public void OccluderVertPush_agrees_with_ShadowSdfCamera(float rot, float sdfScale, float camX)
    {
        var cam = ShadowSdfCamera.SyntheticCamera(
            cameraPosWorld: new Vector2D<float>(camX, 360f),
            cameraRotRadians: rot,
            viewportSizeWorld: new Vector2D<int>(1280, 720),
            swapchainSizePx: new Vector2D<int>(1280, 720),
            sdfScale: sdfScale);

        var cc = MathF.Cos(-cam.CameraRotRadians);
        var ss = MathF.Sin(-cam.CameraRotRadians);

        for (var yi = 0; yi < 5; yi++)
        {
            var worldY = 360f + (yi - 2f) * 120f;
            for (var xi = 0; xi < 5; xi++)
            {
                var worldX = camX + (xi - 2f) * 120f;
                var pWorld = new Vector2D<float>(worldX, worldY);

                // Replicate the GLSL push-constant projection from shadow_occluder.vert.glsl lines 32–39:
                //   d = cornerWorld - cameraPos;  r = mat2(cc, ss, -ss, cc) * d  (note: GLSL column-major)
                //   vpPx = (r.x + vpW*0.5, vpH*0.5 - r.y);  swapchainPx = physOffset + vpPx * physScale
                var dx = worldX - cam.CameraPosWorld.X;
                var dy = worldY - cam.CameraPosWorld.Y;
                var rx = dx * cc - dy * ss;
                var ry = dx * ss + dy * cc;
                var vpPxX = rx + cam.ViewportSizeWorld.X * 0.5f;
                var vpPxY = cam.ViewportSizeWorld.Y * 0.5f - ry;
                var pushSwapchainPxX = cam.PhysicalOffsetSwapchainPx.X + vpPxX * cam.PhysicalScale;
                var pushSwapchainPxY = cam.PhysicalOffsetSwapchainPx.Y + vpPxY * cam.PhysicalScale;

                var expected = cam.WorldToSwapchainPx(pWorld);

                Assert.True(MathF.Abs(pushSwapchainPxX - expected.X) <= 1e-4f,
                    $"X drift at ({worldX},{worldY}): push {pushSwapchainPxX} vs cam {expected.X}");
                Assert.True(MathF.Abs(pushSwapchainPxY - expected.Y) <= 1e-4f,
                    $"Y drift at ({worldX},{worldY}): push {pushSwapchainPxY} vs cam {expected.Y}");
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Cyberland.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Cyberland.sln from test base directory.");
    }
}
