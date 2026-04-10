using Cyberland.Engine.Rendering;
using Glslang.NET;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class EngineShaderSourcesTests
{
    public static TheoryData<string, ShaderStage> AllBuiltInShaders =>
        new()
        {
            { EngineShaderSources.SpriteVert, ShaderStage.Vertex },
            { EngineShaderSources.SpriteEmissiveFrag, ShaderStage.Fragment },
            { EngineShaderSources.SpriteGbufferFrag, ShaderStage.Fragment },
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
        };

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
}
