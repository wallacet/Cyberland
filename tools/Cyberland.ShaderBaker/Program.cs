using System.Buffers.Binary;
using Glslang.NET;

namespace Cyberland.ShaderBaker;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var opts = ParseArgs(args);
            if (opts.Mode == BakerMode.Directory)
            {
                BakeDirectory(opts.InputDirectory!, opts.OutputDirectory!);
                return 0;
            }

            BakeSingle(opts.InputPath!, opts.Stage!.Value, opts.OutputPath!);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Shader baker failed: {ex.Message}");
            return 1;
        }
    }

    private static void BakeDirectory(string inputDirectory, string outputDirectory)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Input directory not found: {inputDirectory}");

        Directory.CreateDirectory(outputDirectory);
        var shaderFiles = Directory.GetFiles(inputDirectory, "*.glsl", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (shaderFiles.Length == 0)
            throw new InvalidOperationException($"No .glsl files found in '{inputDirectory}'.");

        foreach (var shaderPath in shaderFiles)
        {
            var fileName = Path.GetFileName(shaderPath);
            var stage = InferStageFromFileName(fileName);
            var outputPath = Path.Combine(outputDirectory, fileName + ".spv");
            BakeSingle(shaderPath, stage, outputPath);
        }
    }

    private static void BakeSingle(string inputPath, ShaderStage stage, string outputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input shader file not found.", inputPath);

        var glsl = File.ReadAllText(inputPath);
        var words = CompileGlslToSpirv(glsl, stage, inputPath);
        var bytes = new byte[words.Length * sizeof(uint)];
        for (var i = 0; i < words.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint), sizeof(uint)), words[i]);

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);
        File.WriteAllBytes(outputPath, bytes);
        Console.WriteLine($"Baked SPIR-V | stage={stage} source={inputPath} output={outputPath}");
    }

    private static ShaderStage InferStageFromFileName(string fileName)
    {
        if (fileName.Contains(".vert.", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("_vert.", StringComparison.OrdinalIgnoreCase))
            return ShaderStage.Vertex;
        if (fileName.Contains(".frag.", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("_frag.", StringComparison.OrdinalIgnoreCase))
            return ShaderStage.Fragment;
        if (fileName.Contains(".comp.", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("_comp.", StringComparison.OrdinalIgnoreCase))
            return ShaderStage.Compute;

        throw new InvalidOperationException(
            $"Cannot infer shader stage from filename '{fileName}'. Expected '.vert.', '.frag.', or '.comp.' marker.");
    }

    private static uint[] CompileGlslToSpirv(string glsl, ShaderStage stage, string sourcePath)
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
            sourceEntrypoint = "main",
            entrypoint = "main",
            defaultVersion = 450,
            defaultProfile = ShaderProfile.None,
            forceDefaultVersionAndProfile = false,
            forwardCompatible = false,
            messages = MessageType.Default
        };

        using Shader shader = new(input);
        if (!shader.Preprocess())
            throw new InvalidOperationException($"GLSL preprocess failed ({stage}) in {sourcePath}:\n{shader.GetInfoLog()}\n{shader.GetDebugLog()}");
        if (!shader.Parse())
            throw new InvalidOperationException($"GLSL parse failed ({stage}) in {sourcePath}:\n{shader.GetInfoLog()}\n{shader.GetDebugLog()}");

        using Glslang.NET.Program program = new();
        program.AddShader(shader);
        if (!program.Link(MessageType.SpvRules | MessageType.VulkanRules | MessageType.Default))
            throw new InvalidOperationException($"GLSL link failed ({stage}) in {sourcePath}:\n{program.GetInfoLog()}\n{program.GetDebugLog()}");

        program.GenerateSPIRV(out uint[] words, stage, null);
        var spirvMessages = program.GetSPIRVMessages();
        if (!string.IsNullOrWhiteSpace(spirvMessages))
            throw new InvalidOperationException($"SPIR-V generation issues ({stage}) in {sourcePath}:\n{spirvMessages}");
        return words;
    }

    private static ParsedOptions ParseArgs(string[] args)
    {
        var options = new ParsedOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--input-dir", StringComparison.OrdinalIgnoreCase))
            {
                options.Mode = BakerMode.Directory;
                options.InputDirectory = RequireValue(args, ref i, "--input-dir");
            }
            else if (arg.Equals("--output-dir", StringComparison.OrdinalIgnoreCase))
            {
                options.OutputDirectory = RequireValue(args, ref i, "--output-dir");
            }
            else if (arg.Equals("--input", StringComparison.OrdinalIgnoreCase))
            {
                options.Mode = BakerMode.Single;
                options.InputPath = RequireValue(args, ref i, "--input");
            }
            else if (arg.Equals("--stage", StringComparison.OrdinalIgnoreCase))
            {
                var stageRaw = RequireValue(args, ref i, "--stage");
                if (!Enum.TryParse(stageRaw, ignoreCase: true, out ShaderStage parsed))
                    throw new InvalidOperationException($"Unsupported --stage value '{stageRaw}'.");
                options.Stage = parsed;
            }
            else if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase))
            {
                options.OutputPath = RequireValue(args, ref i, "--output");
            }
            else
            {
                throw new InvalidOperationException($"Unknown argument: {arg}");
            }
        }

        if (options.Mode == BakerMode.Directory)
        {
            if (string.IsNullOrWhiteSpace(options.InputDirectory) || string.IsNullOrWhiteSpace(options.OutputDirectory))
                throw new InvalidOperationException("Directory mode requires --input-dir and --output-dir.");
            return options;
        }

        if (string.IsNullOrWhiteSpace(options.InputPath) ||
            string.IsNullOrWhiteSpace(options.OutputPath) ||
            options.Stage is null)
        {
            throw new InvalidOperationException("Single-file mode requires --input, --stage, and --output.");
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        index++;
        if (index >= args.Length)
            throw new InvalidOperationException($"Missing value for {optionName}.");
        return args[index];
    }

    private enum BakerMode
    {
        Single,
        Directory
    }

    private sealed class ParsedOptions
    {
        public BakerMode Mode { get; set; } = BakerMode.Single;
        public string? InputPath { get; set; }
        public ShaderStage? Stage { get; set; }
        public string? OutputPath { get; set; }
        public string? InputDirectory { get; set; }
        public string? OutputDirectory { get; set; }
    }
}
