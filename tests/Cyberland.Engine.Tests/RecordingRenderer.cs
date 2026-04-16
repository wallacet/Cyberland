using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

internal sealed class RecordingRenderer : IRenderer
{
    private readonly object _lock = new();

    public Vector2D<int> SwapchainPixelSize { get; set; } = new(800, 600);
    public Action? RequestClose { get; set; }
    public FramePacing FramePacing { get; set; } = FramePacing.VSync;
    public TextureId DefaultNormalTextureId => 1;
    public TextureId WhiteTextureId => 2;

    public List<SpriteDrawRequest> Sprites { get; } = new();
    public List<PointLight> PointLights { get; } = new();
    public List<SpotLight> SpotLights { get; } = new();
    public List<DirectionalLight> DirectionalLights { get; } = new();
    public List<AmbientLight> AmbientLights { get; } = new();
    public List<PostProcessVolume> Volumes { get; } = new();
    public GlobalPostProcessSettings? LastGlobal { get; private set; }

    /// <summary>When set, returned from <see cref="RegisterTextureRgba"/> instead of the default id (tests upload failures).</summary>
    public TextureId? RegisterTextureRgbaOverride { get; set; }

    private TextureId _nextTextureId = 3;

    /// <summary>Increments for each <see cref="RegisterTextureRgba"/> call (atlas page creation).</summary>
    public int RegisterTextureRgbaCallCount { get; private set; }

    public TextureId RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height)
    {
        RegisterTextureRgbaCallCount++;
        return RegisterTextureRgbaOverride ?? _nextTextureId++;
    }

    /// <summary>Counts successful subregion uploads (atlas updates).</summary>
    public int UploadSubregionCount { get; private set; }

    /// <summary>When true, <see cref="TryUploadTextureRgbaSubregion"/> returns false (atlas update failure path).</summary>
    public bool FailSubregionUpload { get; set; }

    /// <summary>
    /// When set to a positive N, the Nth call to <see cref="TryUploadTextureRgbaSubregion"/> returns false (1-based).
    /// Other calls succeed unless <see cref="FailSubregionUpload"/> is true. Resets when this property is reassigned.
    /// </summary>
    public int FailSubregionUploadOnAttempt { get; set; } = -1;

    private int _subregionUploadAttempt;

    public bool TryUploadTextureRgbaSubregion(TextureId textureId, int dstX, int dstY, int width, int height,
        ReadOnlySpan<byte> rgba)
    {
        if (textureId == TextureId.MaxValue || width <= 0 || height <= 0 || rgba.Length < width * height * 4)
            return false;
        if (FailSubregionUpload)
            return false;
        _subregionUploadAttempt++;
        if (FailSubregionUploadOnAttempt > 0 && _subregionUploadAttempt == FailSubregionUploadOnAttempt)
            return false;
        UploadSubregionCount++;
        return true;
    }

    public void SubmitSprite(in SpriteDrawRequest draw)
    {
        lock (_lock)
            Sprites.Add(draw);
    }

    public void SubmitSprites(ReadOnlySpan<SpriteDrawRequest> draws)
    {
        if (draws.Length == 0)
            return;
        lock (_lock)
        {
            foreach (ref readonly var d in draws)
                Sprites.Add(d);
        }
    }

    public void SubmitPointLight(in PointLight light)
    {
        lock (_lock)
            PointLights.Add(light);
    }

    public void SubmitSpotLight(in SpotLight light)
    {
        lock (_lock)
            SpotLights.Add(light);
    }

    public void SubmitDirectionalLight(in DirectionalLight light)
    {
        lock (_lock)
            DirectionalLights.Add(light);
    }

    public void SubmitAmbientLight(in AmbientLight light)
    {
        lock (_lock)
            AmbientLights.Add(light);
    }

    public void SubmitPostProcessVolume(in PostProcessVolume volume)
    {
        lock (_lock)
            Volumes.Add(volume);
    }

    public void SetGlobalPostProcess(in GlobalPostProcessSettings settings)
    {
        lock (_lock)
            LastGlobal = settings;
    }
}
