using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Mod-facing renderer: layered sprites, materials, lights, post volumes. Implemented by <see cref="VulkanRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading:</b> <see cref="SubmitSprite"/>, <see cref="SubmitPointLight"/>, <see cref="SubmitPostProcessVolume"/>,
/// and <see cref="SetGlobalPostProcess"/> are safe to call from <see cref="Cyberland.Engine.Core.Ecs.IParallelSystem"/> workers concurrently
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
    /// <summary>Current backbuffer size in pixels (width × height).</summary>
    Vector2D<int> SwapchainPixelSize { get; }

    /// <summary>
    /// Host-driven frame pacing: VSync (FIFO), uncapped, or CPU-limited FPS.
    /// </summary>
    /// <remarks>
    /// Prefer the main thread or options UI when changing this; avoid toggling from
    /// <see cref="Cyberland.Engine.Core.Ecs.IParallelSystem"/> workers without coordination.
    /// </remarks>
    FramePacing FramePacing { get; set; }

    /// <summary>Host assigns; mods call to exit cleanly (window close).</summary>
    Action? RequestClose { get; set; }

    /// <summary>Uploads an RGBA8 image; returns a stable id for <see cref="SpriteDrawRequest"/> and sprite components.</summary>
    /// <param name="rgba">Packed RGBA8 pixels, length <c>width * height * 4</c>.</param>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    int RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height);

    /// <summary>Default flat normal (128,128,255) 1×1 if not registered.</summary>
    int DefaultNormalTextureId { get; }

    /// <summary>1×1 white texture.</summary>
    int WhiteTextureId { get; }

    /// <summary>Queues one sprite for the next frame (thread-safe; see interface remarks).</summary>
    void SubmitSprite(in SpriteDrawRequest draw);

    /// <summary>Queues a radial point light (cleared/rebuilt each frame by the caller’s systems).</summary>
    void SubmitPointLight(in PointLight light);

    /// <summary>Queues a spot light.</summary>
    void SubmitSpotLight(in SpotLight light);

    /// <summary>Queues a directional light.</summary>
    void SubmitDirectionalLight(in DirectionalLight light);

    /// <summary>Queues ambient fill (typically one per frame).</summary>
    void SubmitAmbientLight(in AmbientLight light);

    /// <summary>Axis-aligned volume in world space (bottom-left +Y up) affecting post settings.</summary>
    void SubmitPostProcessVolume(in PostProcessVolume volume);

    /// <summary>Global post-process toggles and parameters. Persists until the next call (not reset each frame).</summary>
    void SetGlobalPostProcess(in GlobalPostProcessSettings settings);
}
