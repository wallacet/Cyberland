using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Mod-facing 2D renderer: layered sprites, materials, lights, post volumes. Implemented by <see cref="VulkanRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading:</b> <see cref="SubmitSprite"/>, <see cref="SubmitPointLight"/>, <see cref="SubmitPostProcessVolume"/>,
/// and <see cref="SetGlobalPostProcess"/> are safe to call from <see cref="Cyberland.Engine.Core.Tasks.IParallelSystem"/> workers concurrently
/// (implementations synchronize CPU-side queues with the render thread). Vulkan command recording and <c>DrawFrame</c> run on
/// the window/render thread only.
/// </para>
/// <para>
/// <see cref="RegisterTextureRgba(ReadOnlySpan{byte}, int, int)"/> is normally used from the main thread during load; call sites that invoke it from
/// parallel code should coordinate externally if the implementation is not made concurrent.
/// </para>
/// </remarks>
public interface IRenderer
{
    Vector2D<int> SwapchainPixelSize { get; }

    /// <summary>Host assigns; mods call to exit cleanly (window close).</summary>
    Action? RequestClose { get; set; }

    /// <summary>Register RGBA8 texture; returns stable id for draws.</summary>
    int RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height);

    /// <summary>Default flat normal (128,128,255) 1×1 if not registered.</summary>
    int DefaultNormalTextureId { get; }

    /// <summary>1×1 white texture.</summary>
    int WhiteTextureId { get; }

    void SubmitSprite(in SpriteDrawRequest draw);

    void SubmitPointLight(in PointLight light);

    void SubmitSpotLight(in SpotLight light);

    void SubmitDirectionalLight(in DirectionalLight light);

    void SubmitAmbientLight(in AmbientLight light);

    /// <summary>Axis-aligned volume in world space (bottom-left +Y up) affecting post settings.</summary>
    void SubmitPostProcessVolume(in PostProcessVolume volume);

    /// <summary>Global post-process toggles and parameters. Persists until the next call (not reset each frame).</summary>
    void SetGlobalPostProcess(in GlobalPostProcessSettings settings);
}
