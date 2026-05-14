using System;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Mod-facing renderer: layered sprites, materials, lights, post volumes, and the active 2D camera.
/// Implemented by <see cref="VulkanRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Maintainer reading order (how one frame reaches the screen):</b>
/// </para>
/// <list type="number">
/// <item><description><see cref="Cyberland.Engine.GameApplication"/> <c>OnRender</c>:
/// <see cref="ResetPendingSubmissionsForNewTick"/> (drops prior tick’s queues if <see cref="VulkanRenderer.DrawFrame"/> never drained),
/// ECS <c>RunFrame</c> (systems call <see cref="SubmitSprite"/> / <see cref="SubmitSprites"/> / lights / camera),
/// then <see cref="VulkanRenderer.DrawFrame"/>.</description></item>
/// <item><description><c>VulkanRenderer.FrameExecution.cs</c> builds a frame plan snapshot once per <see cref="VulkanRenderer.DrawFrame"/>:
/// drains concurrent queues, picks camera, merges post volumes, sorts draws (<c>FramePlanBuilder.Build</c>).</description></item>
/// <item><description><c>VulkanRenderer.Deferred.Recording</c>: encodes the deferred/HDR/bloom/composite passes from that plan,
/// then draws viewport UI overlay on the swapchain.</description></item>
/// </list>
/// <para>
/// <b>Coordinate spaces:</b>
/// </para>
/// <list type="bullet">
/// <item><b>World</b> (origin bottom-left, +Y up): gameplay, <see cref="SubmitSprite"/> with
/// <see cref="Scene.CoordinateSpace.WorldSpace"/>, all <c>*World</c> light / volume positions, and
/// <see cref="SubmitCamera"/> position / background.</item>
/// <item><b>Virtual viewport</b> (top-left, +Y down, extent <see cref="ActiveCameraViewportSize"/>): HUD / UI
/// sprites submitted with <see cref="Scene.CoordinateSpace.ViewportSpace"/>. Stays locked to the camera's virtual
/// canvas regardless of camera position, rotation, or window size.</item>
/// <item><b>Swapchain</b> (top-left, +Y down, extent <see cref="SwapchainPixelSize"/>): physical window pixels.
/// The renderer aspect-preserving letterboxes the virtual viewport into the swapchain; bars fill the unused
/// area with the active camera's background color.</item>
/// </list>
/// <para>
/// <b>Threading:</b> all <c>Submit*</c> members and <see cref="SetGlobalPostProcess"/> are safe to call from
/// <see cref="Cyberland.Engine.Core.Ecs.IParallelSystem"/> workers concurrently (implementations synchronize
/// CPU-side queues with the render thread). Property writes like <see cref="FramePacing"/> and
/// <see cref="RequestClose"/> should stay on the main thread or be externally coordinated. Vulkan command recording
/// and <c>DrawFrame</c> run on the window / render thread only.
/// </para>
/// <para>
/// <see cref="RegisterTextureRgba(ReadOnlySpan{byte}, int, int)"/> is normally used from the main thread during load. Implementations may serialize
/// texture uploads onto the graphics queue/command pools; call sites that invoke texture registration from parallel code should coordinate externally.
/// Upload APIs return <see cref="TextureId.MaxValue"/> / <c>false</c> for deterministic failure (invalid inputs, missing ids, or capacity limits).
/// </para>
/// </remarks>
public interface IRenderer
{
    /// <summary>Current backbuffer size in pixels (width × height).</summary>
    Vector2D<int> SwapchainPixelSize { get; }

    /// <summary>
    /// Virtual viewport size in world pixels of the camera that will render the <b>next</b> frame — either the
    /// highest-priority enabled <see cref="SubmitCamera"/> submission or a default camera equal to
    /// <see cref="SwapchainPixelSize"/> when no cameras are submitted. Use for HUD layout and viewport anchors
    /// so UI stays the same pixel size regardless of the physical window.
    /// </summary>
    Vector2D<int> ActiveCameraViewportSize { get; }

    /// <summary>
    /// Active camera snapshot for the next frame (pending submissions win); falls back to
    /// <see cref="CameraSelection.Default"/> when none are eligible.
    /// </summary>
    CameraViewRequest ActiveCameraView { get; }

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

    /// <summary>
    /// Creates a shader module from precompiled SPIR-V bytes for custom mod pipelines.
    /// </summary>
    /// <param name="spirvBytes">Raw little-endian SPIR-V payload.</param>
    /// <param name="debugName">Optional debug marker used by GPU tooling.</param>
    /// <returns>An opaque handle that must be disposed by the caller.</returns>
    /// <remarks>
    /// Call on the window/render thread. The returned handle owns native Vulkan shader module memory until disposed.
    /// </remarks>
    IShaderModuleHandle CreateShaderModuleFromSpirv(ReadOnlySpan<byte> spirvBytes, string? debugName = null);

    /// <summary>
    /// Compiles GLSL at runtime and creates a shader module. This path exists as a fallback and should not be used for shipped content.
    /// </summary>
    /// <param name="glsl">Full shader source text.</param>
    /// <param name="stage">Target shader stage.</param>
    /// <param name="debugName">Optional debug marker used by GPU tooling.</param>
    /// <param name="sourceDescription">Optional source label used in warning logs.</param>
    /// <returns>An opaque handle that must be disposed by the caller.</returns>
    /// <remarks>
    /// Call on the window/render thread. Runtime GLSL compilation is intentionally logged so missing precompiled SPIR-V can be diagnosed.
    /// </remarks>
    IShaderModuleHandle CreateShaderModuleFromGlsl(
        string glsl,
        ShaderModuleStage stage,
        string? debugName = null,
        string? sourceDescription = null);

    /// <summary>Uploads an RGBA8 image; returns a stable id for <see cref="SpriteDrawRequest"/> and sprite components.</summary>
    /// <param name="rgba">Packed RGBA8 pixels, length <c>width * height * 4</c>.</param>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    /// <returns>
    /// Texture id on success; <see cref="TextureId.MaxValue"/> on failure (invalid size/data, or implementation capacity reached).
    /// </returns>
    TextureId RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height);

    /// <summary>
    /// Uploads RGBA8 sampled as linear UNORM (no sRGB decode). Use for MSDF / distance-field atlases and other
    /// non–display-referred encodings; ordinary sprites and photos should use <see cref="RegisterTextureRgba"/>.
    /// </summary>
    /// <returns>
    /// Texture id on success; <see cref="TextureId.MaxValue"/> on failure (invalid size/data, or implementation capacity reached).
    /// </returns>
    TextureId RegisterTextureRgbaLinear(ReadOnlySpan<byte> rgba, int width, int height);

    /// <summary>
    /// Copies premultiplied RGBA into a sub-rectangle of an existing texture (same dimensions as when registered).
    /// Used by the glyph atlas to update packed regions without allocating a new GPU image per glyph.
    /// </summary>
    /// <param name="textureId">Slot from <see cref="RegisterTextureRgba"/> or <see cref="RegisterTextureRgbaLinear"/>.</param>
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
    /// Queues multiple sprites in one call (preferred for text runs that would otherwise call
    /// <see cref="SubmitSprite"/> once per glyph).
    /// </summary>
    void SubmitSprites(ReadOnlySpan<SpriteDrawRequest> draws);

    /// <summary>
    /// Queues one text glyph for the dedicated instanced text pipeline. This path batches by atlas page and clip state
    /// to keep draw counts low on text-heavy UI.
    /// </summary>
    void SubmitTextGlyph(in TextGlyphDrawRequest draw);

    /// <summary>
    /// Queues multiple text glyphs. Prefer this over repeated <see cref="SubmitTextGlyph"/> calls for long runs.
    /// </summary>
    void SubmitTextGlyphs(ReadOnlySpan<TextGlyphDrawRequest> draws);

    /// <summary>
    /// Drops all pending CPU-side sprite/light/camera/post queues before the next simulation tick enqueues work.
    /// The stock host calls this once per render callback <strong>before</strong> the ECS scheduler runs <c>RunFrame</c>
    /// so a failed or skipped <c>DrawFrame</c> (swapchain out-of-date, recording exception, etc.) cannot leave undrained
    /// submissions that merge with the following tick — which otherwise produces extra <c>vkCmdDraw</c> for stale HUD glyphs.
    /// </summary>
    void ResetPendingSubmissionsForNewTick();

    /// <summary>Queues a radial point light (cleared/rebuilt each frame by the caller’s systems).</summary>
    /// <remarks>
    /// The stock deferred path keeps at most <c>DeferredRenderingConstants.MaxPointLights</c> lights per frame.
    /// When submissions exceed the cap, the renderer first applies a deterministic value-ordering pass, keeps the
    /// first lights in that ordering, and drops the tail.
    /// </remarks>
    void SubmitPointLight(in PointLight light);

    /// <summary>Queues a spot light.</summary>
    /// <remarks>
    /// The stock deferred path keeps at most <c>DeferredRenderingConstants.MaxSpotLights</c> lights per frame.
    /// When submissions exceed the cap, the renderer first applies a deterministic value-ordering pass, keeps the
    /// first lights in that ordering, and drops the tail.
    /// </remarks>
    void SubmitSpotLight(in SpotLight light);

    /// <summary>Queues a directional light.</summary>
    /// <remarks>
    /// The stock deferred path keeps at most <c>DeferredRenderingConstants.MaxDirectionalLights</c> lights per frame.
    /// When submissions exceed the cap, the renderer first applies a deterministic value-ordering pass, keeps the
    /// first lights in that ordering, and drops the tail.
    /// </remarks>
    void SubmitDirectionalLight(in DirectionalLight light);

    /// <summary>Queues ambient fill (typically one per frame).</summary>
    void SubmitAmbientLight(in AmbientLight light);

    /// <summary>Queues one post-process volume plus world transform snapshot for overlap/merge.</summary>
    void SubmitPostProcessVolume(in PostProcessVolume volume, Vector2D<float> worldPosition, float worldRotationRadians, Vector2D<float> worldScale);

    /// <summary>Global post-process toggles and parameters. Persists until the next call (not reset each frame).</summary>
    void SetGlobalPostProcess(in GlobalPostProcessSettings settings);

    /// <summary>
    /// Queues one camera for the next frame. The renderer picks the highest-<see cref="CameraViewRequest.Priority"/>
    /// enabled entry per frame (submit order breaks ties). When no eligible camera is submitted the renderer falls
    /// back to a default camera centered on <see cref="SwapchainPixelSize"/>.
    /// </summary>
    void SubmitCamera(in CameraViewRequest camera);
}
