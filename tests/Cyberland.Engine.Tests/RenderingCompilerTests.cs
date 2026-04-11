using Cyberland.Engine.Rendering;
using Glslang.NET;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class RenderingCompilerTests
{
    [Fact]
    public void GlslSpirvCompiler_compiles_minimal_vertex_shader()
    {
        const string glsl =
            """
            #version 450
            void main() {
                gl_Position = vec4(0.0, 0.0, 0.0, 1.0);
            }
            """;
        var words = GlslSpirvCompiler.CompileGlslToSpirv(glsl, ShaderStage.Vertex);
        Assert.NotNull(words);
        Assert.True(words.Length > 10);
    }

    [Fact]
    public void GlslSpirvCompiler_respects_custom_entry_point()
    {
        const string glsl =
            """
            #version 450
            void notMain() {
                gl_Position = vec4(0.0, 0.0, 0.0, 1.0);
            }
            """;
        var words = GlslSpirvCompiler.CompileGlslToSpirv(glsl, ShaderStage.Vertex, "notMain");
        Assert.NotEmpty(words);
    }

    [Fact]
    public void GlslSpirvCompiler_invalid_source_throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GlslSpirvCompiler.CompileGlslToSpirv("totally not glsl", ShaderStage.Vertex));
    }

    [Fact]
    public void GlslSpirvCompiler_preprocess_failure_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GlslSpirvCompiler.CompileGlslToSpirv("#version 450\n#error blocking", ShaderStage.Vertex));
        Assert.Contains("preprocess", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GlslSpirvCompiler_parse_failure_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GlslSpirvCompiler.CompileGlslToSpirv("#version 450\nvoid main(", ShaderStage.Vertex));
        Assert.Contains("parse", ex.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void GraphicsInitializationException_UserMessage_contains_detail_and_tips()
    {
        var inner = new InvalidOperationException("inner_msg");
        var ex = new GraphicsInitializationException("gpu", inner);
        Assert.Contains("gpu", ex.UserMessage);
        Assert.Contains("inner_msg", ex.UserMessage);
        Assert.Contains("Technical detail", ex.UserMessage);
        Assert.Contains("Vulkan", ex.UserMessage);
    }

    [Fact]
    public void UserMessageDialog_WriteErrorToStderr_writes_formatted_block()
    {
        lock (ConsoleTestSync.ErrorRedirectLock)
        {
            var prev = Console.Error;
            try
            {
                using var sw = new StringWriter();
                Console.SetError(sw);
                UserMessageDialog.WriteErrorToStderr("Title", "Body");
                var s = sw.ToString();
                Assert.Contains("Title", s);
                Assert.Contains("Body", s);
            }
            finally
            {
                Console.SetError(prev);
            }
        }
    }

    [Fact]
    public void UserMessageDialog_WriteDiagnosticToStderr_writes_severity_block()
    {
        lock (ConsoleTestSync.ErrorRedirectLock)
        {
            var prev = Console.Error;
            try
            {
                using var sw = new StringWriter();
                Console.SetError(sw);
                UserMessageDialog.WriteDiagnosticToStderr("LEVEL", "Cap", "Body");
                var s = sw.ToString();
                Assert.Contains("[LEVEL]", s, StringComparison.Ordinal);
                Assert.Contains("Cap", s);
                Assert.Contains("Body", s);
            }
            finally
            {
                Console.SetError(prev);
            }
        }
    }

    [Fact]
    public void UserMessageDialog_WriteWarningToStderr_delegates_to_diagnostic()
    {
        lock (ConsoleTestSync.ErrorRedirectLock)
        {
            var prev = Console.Error;
            try
            {
                using var sw = new StringWriter();
                Console.SetError(sw);
                UserMessageDialog.WriteWarningToStderr("T", "B");
                var s = sw.ToString();
                Assert.Contains("[WARNING]", s, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetError(prev);
            }
        }
    }
}
