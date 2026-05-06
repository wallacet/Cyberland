using Cyberland.Engine.Rendering;
using Glslang.NET;
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
            { EngineShaderSources.DeferredBaseFrag, ShaderStage.Fragment },
            { EngineShaderSources.DeferredPointVert, ShaderStage.Vertex },
            { EngineShaderSources.DeferredPointFrag, ShaderStage.Fragment },
            { EngineShaderSources.DeferredEmissiveBleedFrag, ShaderStage.Fragment },
            { EngineShaderSources.SpriteTransparentWboitFrag, ShaderStage.Fragment },
            { EngineShaderSources.TransparentResolveFrag, ShaderStage.Fragment },
            { EngineShaderSources.CompositeVert, ShaderStage.Vertex },
            { EngineShaderSources.CompositeFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomExtractFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomDownsampleFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomGaussianFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomUpsampleFrag, ShaderStage.Fragment },
            { EngineShaderSources.BloomCopyFrag, ShaderStage.Fragment },
            { EngineShaderSources.TextMsdfVert, ShaderStage.Vertex },
            { EngineShaderSources.TextMsdfFrag, ShaderStage.Fragment },
        };

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
            Assert.Contains(shader, covered);
    }

    [Theory]
    [MemberData(nameof(AllBuiltInShaders))]
    public void Load_returns_glsl_that_compiles_to_spirv(string fileName, ShaderStage stage)
    {
        var src = EngineShaderSources.Load(fileName);
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
}
