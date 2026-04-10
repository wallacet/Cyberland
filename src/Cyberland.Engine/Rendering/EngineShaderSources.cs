namespace Cyberland.Engine.Rendering;

/// <summary>
/// Loads built-in GLSL sources shipped as embedded resources under <c>Rendering/Shaders/*.glsl</c>.
/// </summary>
internal static class EngineShaderSources
{
    public const string SpriteVert = "sprite_vert.glsl";
    public const string SpriteEmissiveFrag = "sprite_emissive.frag.glsl";
    public const string SpriteGbufferFrag = "sprite_gbuffer.frag.glsl";
    public const string DeferredBaseFrag = "deferred_base.frag.glsl";
    public const string DeferredPointVert = "deferred_point.vert.glsl";
    public const string DeferredPointFrag = "deferred_point.frag.glsl";
    public const string DeferredEmissiveBleedFrag = "deferred_emissive_bleed.frag.glsl";
    public const string SpriteTransparentWboitFrag = "sprite_transparent_wboit.frag.glsl";
    public const string TransparentResolveFrag = "transparent_resolve.frag.glsl";
    public const string CompositeVert = "composite_vert.glsl";
    public const string CompositeFrag = "composite.frag.glsl";
    public const string BloomExtractFrag = "bloom_extract.frag.glsl";
    public const string BloomDownsampleFrag = "bloom_downsample.frag.glsl";
    public const string BloomGaussianFrag = "bloom_gaussian.frag.glsl";
    public const string BloomUpsampleFrag = "bloom_upsample.frag.glsl";
    public const string BloomCopyFrag = "bloom_copy.frag.glsl";

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
}