using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

internal sealed class RecordingRenderer : IRenderer
{
    private readonly object _lock = new();

    public Vector2D<int> SwapchainPixelSize { get; set; } = new(800, 600);
    public Action? RequestClose { get; set; }
    public FramePacing FramePacing { get; set; } = FramePacing.VSync;
    public int DefaultNormalTextureId => 1;
    public int WhiteTextureId => 2;

    public List<SpriteDrawRequest> Sprites { get; } = new();
    public List<PointLight> PointLights { get; } = new();
    public List<SpotLight> SpotLights { get; } = new();
    public List<DirectionalLight> DirectionalLights { get; } = new();
    public List<AmbientLight> AmbientLights { get; } = new();
    public List<PostProcessVolume> Volumes { get; } = new();
    public GlobalPostProcessSettings? LastGlobal { get; private set; }

    public int RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height) => 3;

    public void SubmitSprite(in SpriteDrawRequest draw)
    {
        lock (_lock)
            Sprites.Add(draw);
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
