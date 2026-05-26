namespace Cyberland.Engine.Rendering;

/// <summary>
/// Loads built-in GLSL sources shipped as embedded resources under <c>Rendering/Shaders/*.glsl</c>.
/// </summary>
/// <remarks>
/// <para>
/// The lighting / shadow path uses the SDF cone-trace include (<see cref="ShadowSdfSamplingInclude"/>). The production
/// lighting shader (<see cref="TiledDeferredLightingFrag"/>) gets the include prepended at bake time and at runtime via
/// <see cref="LoadFragmentWithShadowInclude"/>.
/// </para>
/// </remarks>
internal static class EngineShaderSources
{
    public const string SpriteVert = "sprite_vert.glsl";
    public const string SpriteEmissiveFrag = "sprite_emissive.frag.glsl";
    public const string SpriteGbufferFrag = "sprite_gbuffer.frag.glsl";
    public const string SpriteSwapchainUiFrag = "sprite_swapchain_ui.frag.glsl";
    public const string DeferredEmissiveBleedFrag = "deferred_emissive_bleed.frag.glsl";
    public const string ShadowOccluderVert = "shadow_occluder.vert.glsl";
    public const string ShadowOccluderFrag = "shadow_occluder.frag.glsl";
    public const string FullscreenTriangleVert = "fullscreen_triangle.vert.glsl";
    public const string JfaInitFrag = "jfa_init.frag.glsl";
    public const string JfaStepFrag = "jfa_step.frag.glsl";
    public const string JfaToSdfFrag = "jfa_to_sdf.frag.glsl";
    public const string ShadowSdfSamplingInclude = "shadow_sdf_sampling.glsl";
    public const string SpriteTransparentWboitFrag = "sprite_transparent_wboit.frag.glsl";
    public const string TransparentResolveFrag = "transparent_resolve.frag.glsl";
    public const string CompositeFrag = "composite.frag.glsl";
    public const string BloomExtractFrag = "bloom_extract.frag.glsl";
    public const string BloomDownsampleFrag = "bloom_downsample.frag.glsl";
    public const string BloomGaussianFrag = "bloom_gaussian.frag.glsl";
    public const string BloomUpsampleFrag = "bloom_upsample.frag.glsl";
    public const string BloomCopyFrag = "bloom_copy.frag.glsl";
    public const string TiledDeferredLightingFrag = "tiled_deferred_lighting.frag.glsl";
    public const string TextMsdfVert = "text_msdf.vert.glsl";
    public const string TextMsdfFrag = "text_msdf.frag.glsl";

    /// <summary>
    /// Shaders that compile from GLSL at runtime so push-constant / include changes cannot drift from embedded SPIR-V.
    /// The shadow SDF cone-trace include and the deferred lighting shaders ship with both SPIR-V (baked) and GLSL
    /// (loaded fresh at startup); the runtime GLSL path defends against include-merge mismatches between bake-time and
    /// run-time.
    /// </summary>
    internal static bool PreferRuntimeGlslCompile(string sourceFileName) =>
        sourceFileName is TiledDeferredLightingFrag
            or ShadowOccluderVert
            or ShadowOccluderFrag;

    /// <summary>Reads UTF-8 text from an embedded shader file.</summary>
    public static string Load(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var asm = typeof(EngineShaderSources).Assembly;
        var resourceName = $"{asm.GetName().Name}.Rendering.Shaders.{fileName}";
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            var available = string.Join(", ", asm.GetManifestResourceNames() ?? Array.Empty<string>());
            throw new InvalidOperationException($"Shader resource '{resourceName}' not found. Available: {available}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Loads a fragment shader plus the shared shadow-SDF sampling include after the #version line. Mirrors the
    /// merge rule the shader baker performs at compile time so runtime GLSL fallback produces identical SPIR-V.
    /// </summary>
    public static string LoadFragmentWithShadowInclude(string fragmentFileName)
    {
        var include = Load(ShadowSdfSamplingInclude);
        var body = Load(fragmentFileName);
        return MergeFragmentWithShadowInclude(include, body);
    }

    /// <summary>Merges include text into a fragment body after its first line (testable helper).</summary>
    internal static string MergeFragmentWithShadowInclude(string include, string body)
    {
        var nl = body.IndexOf('\n');
        if (nl < 0)
            return include + Environment.NewLine + body;
        return body[..(nl + 1)] + include + Environment.NewLine + body[(nl + 1)..];
    }

    /// <summary>
    /// Tries to read a precompiled SPIR-V blob for a built-in shader.
    /// </summary>
    /// <param name="fileName">The GLSL source filename (for example <c>sprite_vert.glsl</c>).</param>
    /// <param name="spirvBytes">Raw SPIR-V bytes on success.</param>
    /// <param name="failureReason">Human-readable reason when not available.</param>
    /// <returns><c>true</c> when the embedded SPIR-V payload exists and is readable.</returns>
    public static bool TryLoadPrecompiledSpirv(string fileName, out byte[] spirvBytes, out string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var asm = typeof(EngineShaderSources).Assembly;
        var resourceName = $"{asm.GetName().Name}.Rendering.Shaders.Spirv.{fileName}.spv";
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            spirvBytes = Array.Empty<byte>();
            failureReason = $"missing embedded resource '{resourceName}'";
            return false;
        }

        using var ms = new MemoryStream((int)Math.Min(stream.Length, int.MaxValue));
        stream.CopyTo(ms);
        spirvBytes = ms.ToArray();
        failureReason = string.Empty;
        return true;
    }
}
