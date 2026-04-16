using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Mod-facing renderer: layered sprites, materials, lights, post volumes. Implemented by <see cref="VulkanRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate spaces:</b> <see cref="SubmitSprite"/> / <see cref="SpriteDrawRequest"/> use <b>world space</b> (origin bottom-left, +Y up).
/// Lights and volumes that take <c>*World</c> positions use the same convention. Fullscreen post-processing and compositing operate in
/// internal framebuffer space; <see cref="SetGlobalPostProcess"/> does not take world coordinates.
/// </para>
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
    /// Host-driven frame pacing: VSync (mailbox when available, else FIFO), uncapped, or CPU-limited FPS.
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
    TextureId RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height);

    /// <summary>
    /// Copies premultiplied RGBA into a sub-rectangle of an existing texture (same dimensions as when registered).
    /// Used by the glyph atlas to update packed regions without allocating a new GPU image per glyph.
    /// </summary>
    /// <param name="textureId">Slot from <see cref="RegisterTextureRgba"/>.</param>
    /// <param name="dstX">Destination X in pixels (top-left).</param>
    /// <param name="dstY">Destination Y in pixels (top-left).</param>
    /// <param name="width">Region width.</param>
    /// <param name="height">Region height.</param>
    /// <param name="rgba">Tightly packed RGBA8, length at least <c>width * height * 4</c>.</param>
    /// <returns>False if the id is invalid, the region is out of bounds, or <paramref name="rgba"/> is too small.</returns>
    bool TryUploadTextureRgbaSubregion(TextureId textureId, int dstX, int dstY, int width, int height, ReadOnlySpan<byte> rgba);

    /// <summary>Default flat normal (128,128,255) 1×1 if not registered.</summary>
    TextureId DefaultNormalTextureId { get; }

    /// <summary>1×1 white texture.</summary>
    TextureId WhiteTextureId { get; }

    /// <summary>Queues one sprite for the next frame (thread-safe; see interface remarks).</summary>
    void SubmitSprite(in SpriteDrawRequest draw);

    /// <summary>
    /// Queues multiple sprites with a single lock acquisition (preferred for text runs that would otherwise call
    /// <see cref="SubmitSprite"/> once per glyph).
    /// </summary>
    void SubmitSprites(ReadOnlySpan<SpriteDrawRequest> draws);

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
