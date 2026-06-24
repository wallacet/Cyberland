using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

internal struct RecordedPostProcessVolume
{
    public PostProcessVolume Volume;
    public Vector2D<float> WorldPosition;
    public float WorldRotationRadians;
    public Vector2D<float> WorldScale;
}

internal sealed class RecordingRenderer : IRenderer
{
    private readonly object _lock = new();

    public Vector2D<int> SwapchainPixelSize { get; set; } = new(800, 600);
    /// <summary>
    /// Virtual camera viewport exposed to mod code; initialized to swapchain so pre-camera layout helpers still work.
    /// Tests may override to simulate specific active cameras without submitting a full <see cref="CameraViewRequest"/>.
    /// </summary>
    public Vector2D<int> ActiveCameraViewportSize { get; set; } = new(800, 600);
    public CameraViewRequest ActiveCameraView { get; set; } = CameraSelection.Default(new Vector2D<int>(800, 600));
    public Action? RequestClose { get; set; }
    public FramePacing FramePacing { get; set; } = FramePacing.VSync;
    public TextureId DefaultNormalTextureId => 1;
    public TextureId WhiteTextureId => 2;
    public TextureId MissingTextureId => 4;

    public List<SpriteDrawRequest> Sprites { get; } = new();
    /// <summary>
    /// Synthetic sprite mirror of glyph submissions for legacy tests that assert text via <see cref="Sprites"/>.
    /// Production <see cref="VulkanRenderer"/> keeps text in a dedicated glyph queue.
    /// </summary>
    public List<SpriteDrawRequest> MirroredTextSprites { get; } = new();
    public List<TextGlyphDrawRequest> TextGlyphs { get; } = new();
    public List<PointLight> PointLights { get; } = new();
    public List<SpotLight> SpotLights { get; } = new();
    public List<DirectionalLight> DirectionalLights { get; } = new();
    public List<AmbientLight> AmbientLights { get; } = new();
    public List<RecordedPostProcessVolume> Volumes { get; } = new();
    public List<CameraViewRequest> Cameras { get; } = new();
    public GlobalPostProcessSettings? LastGlobal { get; private set; }

    /// <summary>When set, returned from <see cref="RegisterTextureRgba"/> instead of the default id (tests upload failures).</summary>
    public TextureId? RegisterTextureRgbaOverride { get; set; }

    private TextureId _nextTextureId = 3;

    /// <summary>Increments for each <see cref="RegisterTextureRgba"/> call (atlas page creation).</summary>
    public int RegisterTextureRgbaCallCount { get; private set; }

    /// <summary>
    /// When true (default), text glyph submissions are also mirrored into <see cref="Sprites"/> for broad legacy coverage.
    /// Set false in tests that need production-faithful separation between sprite and text queues.
    /// </summary>
    public bool MirrorTextGlyphsIntoSprites { get; set; } = true;

    public TextureId RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height)
    {
        RegisterTextureRgbaCallCount++;
        return RegisterTextureRgbaOverride ?? _nextTextureId++;
    }

    public TextureId RegisterTextureRgbaLinear(ReadOnlySpan<byte> rgba, int width, int height) =>
        RegisterTextureRgba(rgba, width, height);

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

    public IShaderModuleHandle CreateShaderModuleFromSpirv(ReadOnlySpan<byte> spirvBytes, string? debugName = null)
    {
        _ = spirvBytes;
        _ = debugName;
        return new NoopShaderModuleHandle();
    }

    public IShaderModuleHandle CreateShaderModuleFromGlsl(
        string glsl,
        ShaderModuleStage stage,
        string? debugName = null,
        string? sourceDescription = null)
    {
        _ = glsl;
        _ = stage;
        _ = debugName;
        _ = sourceDescription;
        return new NoopShaderModuleHandle();
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

    public void SubmitTextGlyph(in TextGlyphDrawRequest draw)
    {
        lock (_lock)
        {
            TextGlyphs.Add(draw);
            if (MirrorTextGlyphsIntoSprites)
            {
                var mirrored = ToSprite(in draw);
                Sprites.Add(mirrored);
                MirroredTextSprites.Add(mirrored);
            }
        }
    }

    public void SubmitTextGlyphs(ReadOnlySpan<TextGlyphDrawRequest> draws)
    {
        if (draws.Length == 0)
            return;
        lock (_lock)
        {
            foreach (ref readonly var d in draws)
            {
                TextGlyphs.Add(d);
                if (MirrorTextGlyphsIntoSprites)
                {
                    var mirrored = ToSprite(in d);
                    Sprites.Add(mirrored);
                    MirroredTextSprites.Add(mirrored);
                }
            }
        }
    }

    private static SpriteDrawRequest ToSprite(in TextGlyphDrawRequest glyph) =>
        new()
        {
            CenterWorld = glyph.Center,
            HalfExtentsWorld = glyph.HalfExtents,
            RotationRadians = 0f,
            Layer = (int)SpriteLayer.Ui,
            SortKey = glyph.SortKey,
            AlbedoTextureId = glyph.TextureId,
            NormalTextureId = TextureId.MaxValue,
            EmissiveTextureId = TextureId.MaxValue,
            ColorMultiply = glyph.Color,
            Alpha = glyph.Color.W,
            EmissiveTint = default,
            EmissiveIntensity = 0f,
            DepthHint = glyph.DepthHint,
            UvRect = glyph.UvRect,
            Transparent = glyph.Space is CoordinateSpace.WorldSpace,
            Space = glyph.Space,
            ViewportClipEnabled = glyph.ViewportClipEnabled,
            ViewportClipRect = glyph.ViewportClipRect
        };

    /// <inheritdoc />
    public void ResetPendingSubmissionsForNewTick()
    {
        lock (_lock)
        {
            Sprites.Clear();
            MirroredTextSprites.Clear();
            TextGlyphs.Clear();
            PointLights.Clear();
            SpotLights.Clear();
            DirectionalLights.Clear();
            AmbientLights.Clear();
            Volumes.Clear();
            Cameras.Clear();
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

    public void SubmitPostProcessVolume(in PostProcessVolume volume, Vector2D<float> worldPosition, float worldRotationRadians, Vector2D<float> worldScale)
    {
        lock (_lock)
            Volumes.Add(new RecordedPostProcessVolume
            {
                Volume = volume,
                WorldPosition = worldPosition,
                WorldRotationRadians = worldRotationRadians,
                WorldScale = worldScale
            });
    }

    public void SetGlobalPostProcess(in GlobalPostProcessSettings settings)
    {
        lock (_lock)
            LastGlobal = settings;
    }

    public void SubmitCamera(in CameraViewRequest camera)
    {
        lock (_lock)
            Cameras.Add(camera);
    }

    private sealed class NoopShaderModuleHandle : IShaderModuleHandle
    {
        public void Dispose()
        {
        }
    }
}
