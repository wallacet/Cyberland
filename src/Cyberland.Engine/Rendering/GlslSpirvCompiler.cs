using System.Diagnostics.CodeAnalysis;
using Glslang.NET;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Compiles GLSL source to SPIR-V using Glslang.NET (Vulkan 1.2 target, SPIR-V 1.5).
/// </summary>
public static class GlslSpirvCompiler
{
    [ExcludeFromCodeCoverage(Justification = "SPIR-V diagnostics from glslang vary by toolchain; success path is covered in tests.")]
    private static void ThrowIfSpirvIssues(string? spirvMessages, ShaderStage stage)
    {
        if (!string.IsNullOrWhiteSpace(spirvMessages))
            throw new InvalidOperationException($"SPIR-V generation reported issues ({stage}):\n{spirvMessages}");
    }

    [ExcludeFromCodeCoverage(Justification = "Single-stage link failure is version-sensitive; success path calls Link with valid shaders.")]
    private static void LinkProgramOrThrow(Program program, MessageType linkFlags, ShaderStage stage)
    {
        if (!program.Link(linkFlags))
            throw new InvalidOperationException($"GLSL link failed ({stage}):\n{program.GetInfoLog()}\n{program.GetDebugLog()}");
    }

    public static uint[] CompileGlslToSpirv(string glsl, ShaderStage stage, string entryPoint = "main")
    {
        CompilationInput input = new()
        {
            language = SourceType.GLSL,
            stage = stage,
            client = ClientType.Vulkan,
            clientVersion = TargetClientVersion.Vulkan_1_2,
            targetLanguage = TargetLanguage.SPV,
            targetLanguageVersion = TargetLanguageVersion.SPV_1_5,
            code = glsl,
            sourceEntrypoint = entryPoint,
            entrypoint = entryPoint,
            defaultVersion = 450,
            defaultProfile = ShaderProfile.None,
            forceDefaultVersionAndProfile = false,
            forwardCompatible = false,
            messages = MessageType.Default
        };

        using Shader shader = new(input);

        if (!shader.Preprocess())
            throw new InvalidOperationException($"GLSL preprocess failed ({stage}):\n{shader.GetInfoLog()}\n{shader.GetDebugLog()}");

        if (!shader.Parse())
            throw new InvalidOperationException($"GLSL parse failed ({stage}):\n{shader.GetInfoLog()}\n{shader.GetDebugLog()}");

        using Program program = new();
        program.AddShader(shader);

        var linkFlags = MessageType.SpvRules | MessageType.VulkanRules | MessageType.Default;
        LinkProgramOrThrow(program, linkFlags, stage);

        program.GenerateSPIRV(out uint[] words, stage, null);

        ThrowIfSpirvIssues(program.GetSPIRVMessages(), stage);

        return words;
    }
}
