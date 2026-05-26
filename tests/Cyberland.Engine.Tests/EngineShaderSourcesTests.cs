using Cyberland.Engine.Rendering;
using Glslang.NET;
using System.Buffers.Binary;
using System.Reflection;

namespace Cyberland.Engine.Tests;

public sealed class EngineShaderSourcesTests
{
    public static TheoryData<string, ShaderStage> AllBuiltInShaders =>
        new()
        {
            { EngineShaderSources.SpriteVert, ShaderStage.Vertex },
            { EngineShaderSources.SpriteEmissiveFrag, ShaderStage.Fragment },
            { EngineShaderSources.SpriteGbufferFrag, ShaderStage.Fragment },
            { EngineShaderSources.SpriteSwapchainUiFrag, ShaderStage.Fragment },
            { EngineShaderSources.DeferredEmissiveBleedFrag, ShaderStage.Fragment },
            { EngineShaderSources.ShadowOccluderVert, ShaderStage.Vertex },
            { EngineShaderSources.ShadowOccluderFrag, ShaderStage.Fragment },
            { EngineShaderSources.FullscreenTriangleVert, ShaderStage.Vertex },
            { EngineShaderSources.JfaInitFrag, ShaderStage.Fragment },
            { EngineShaderSources.JfaStepFrag, ShaderStage.Fragment },
            { EngineShaderSources.JfaToSdfFrag, ShaderStage.Fragment },
            { EngineShaderSources.SpriteTransparentWboitFrag, ShaderStage.Fragment },
            { EngineShaderSources.TransparentResolveFrag, ShaderStage.Fragment },
            { EngineShaderSources.CompositeFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomExtractFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomDownsampleFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomGaussianFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomUpsampleFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomCopyFrag, ShaderStage.Fragment },
            { EngineShaderSources.TiledDeferredLightingFrag, ShaderStage.Fragment },
            { EngineShaderSources.TextMsdfVert, ShaderStage.Vertex },
            { EngineShaderSources.TextMsdfFrag, ShaderStage.Fragment },
        };

    [Fact]
    public void MergeFragmentWithShadowInclude_supports_single_line_body()
    {
        var merged = EngineShaderSources.MergeFragmentWithShadowInclude("// include", "#version 450");
        Assert.Contains("// include", merged, StringComparison.Ordinal);
        Assert.Contains("#version 450", merged, StringComparison.Ordinal);
    }

    [Fact]
    public void AllBuiltInShaders_includes_all_EngineShaderSources_constants()
    {
        var covered = AllBuiltInShaders.Select(row => (string)row[0]).ToHashSet(StringComparer.Ordinal);
        var declared = typeof(EngineShaderSources)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToArray();

        foreach (var shader in declared)
        {
            if (shader == EngineShaderSources.ShadowSdfSamplingInclude)
                continue;
            Assert.Contains(shader, covered);
        }
    }

    [Theory]
    [MemberData(nameof(AllBuiltInShaders))]
    public void Load_returns_glsl_that_compiles_to_spirv(string fileName, ShaderStage stage)
    {
        var src = fileName is EngineShaderSources.TiledDeferredLightingFrag
            ? EngineShaderSources.LoadFragmentWithShadowInclude(fileName)
            : EngineShaderSources.Load(fileName);
        Assert.Contains("#version 450", src, StringComparison.Ordinal);
        _ = GlslSpirvCompiler.CompileGlslToSpirv(src, stage);
    }

    [Fact]
    public void Load_throws_when_shader_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => EngineShaderSources.Load("does_not_exist.glsl"));
        Assert.Contains("does_not_exist.glsl", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_throws_on_whitespace_name()
    {
        Assert.Throws<ArgumentException>(() => EngineShaderSources.Load("   "));
    }

    [Fact]
    public void Load_throws_on_null_name()
    {
        Assert.Throws<ArgumentNullException>(() => EngineShaderSources.Load(null!));
    }

    [Fact]
    public void SpriteTransparentWboitFrag_reveal_uses_unweighted_alpha()
    {
        var src = EngineShaderSources.Load(EngineShaderSources.SpriteTransparentWboitFrag);
        Assert.Contains("outReveal = vec4(a, 0.0, 0.0, 1.0);", src, StringComparison.Ordinal);
        Assert.DoesNotContain("outReveal = vec4(a * w", src, StringComparison.Ordinal);
    }

    [Fact]
    public void BloomExtractFrag_applies_emissive_bloom_gain()
    {
        var src = EngineShaderSources.Load(EngineShaderSources.BloomExtractFrag);
        Assert.Contains("pc.bloomSourceGain", src, StringComparison.Ordinal);
        Assert.Contains("prefilteredColor(scene, pc.threshold, pc.knee) * pc.bloomSourceGain", src, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllBuiltInShaders))]
    public void TryLoadPrecompiledSpirv_returns_valid_spirv_for_builtins(string fileName, ShaderStage _)
    {
        var loaded = EngineShaderSources.TryLoadPrecompiledSpirv(fileName, out var spirvBytes, out var failureReason);
        Assert.True(loaded, failureReason);
        Assert.True(spirvBytes.Length > 0);
        Assert.True(SpirvBinary.TryDecodeWords(spirvBytes, out var words, out var decodeReason), decodeReason);
        Assert.True(words.Length > 4);
    }

    [Fact]
    public void TryLoadPrecompiledSpirv_returns_false_when_missing()
    {
        var loaded = EngineShaderSources.TryLoadPrecompiledSpirv("missing_shader.glsl", out var spirvBytes, out var failureReason);
        Assert.False(loaded);
        Assert.Empty(spirvBytes);
        Assert.Contains("missing embedded resource", failureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpirvBinary_rejects_non_dword_length()
    {
        var ok = SpirvBinary.TryDecodeWords([0x03, 0x02, 0x23], out var words, out var reason);
        Assert.False(ok);
        Assert.Empty(words);
        Assert.Contains("divisible by 4", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpirvBinary_rejects_empty_payload()
    {
        var ok = SpirvBinary.TryDecodeWords(ReadOnlySpan<byte>.Empty, out var words, out var reason);
        Assert.False(ok);
        Assert.Empty(words);
        Assert.Contains("empty", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpirvBinary_rejects_wrong_magic()
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01020304u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], 0u);
        var ok = SpirvBinary.TryDecodeWords(bytes, out var words, out var reason);
        Assert.False(ok);
        Assert.Empty(words);
        Assert.Contains("magic mismatch", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreferRuntimeGlslCompile_marks_shadow_and_deferred_shaders()
    {
        Assert.True(EngineShaderSources.PreferRuntimeGlslCompile(EngineShaderSources.TiledDeferredLightingFrag));
        Assert.True(EngineShaderSources.PreferRuntimeGlslCompile(EngineShaderSources.ShadowOccluderFrag));
        Assert.False(EngineShaderSources.PreferRuntimeGlslCompile(EngineShaderSources.SpriteVert));
    }

    [Fact]
    public void TiledDeferredLighting_uses_three_vec4_point_stride()
    {
        var src = EngineShaderSources.LoadFragmentWithShadowInclude(EngineShaderSources.TiledDeferredLightingFrag);
        Assert.Contains("lid * 3u", src, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeGlslInclude_covers_all_fragment_shaders_that_reference_sdf_shadow()
    {
        var runtimeGlslShaders = AllBuiltInShaders
            .Where(row => (ShaderStage)row[1] == ShaderStage.Fragment)
            .Select(row => (string)row[0])
            .Where(EngineShaderSources.PreferRuntimeGlslCompile)
            .ToArray();

        foreach (var shader in runtimeGlslShaders)
        {
            var rawSrc = EngineShaderSources.Load(shader);
            if (!rawSrc.Contains("sdfSoftShadow", StringComparison.Ordinal) &&
                !rawSrc.Contains("sdfDirectionalShadow", StringComparison.Ordinal))
                continue;

            var merged = EngineShaderSources.LoadFragmentWithShadowInclude(shader);
            Assert.Contains("worldToSwapchainPx", merged, StringComparison.Ordinal);
            Assert.Contains("void main(", merged, StringComparison.Ordinal);
        }
    }
}
