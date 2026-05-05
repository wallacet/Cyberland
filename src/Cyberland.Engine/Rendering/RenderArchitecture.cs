// Purpose: Immutable snapshot structs consumed by Vulkan recording after FramePlanBuilder.Build().
// FramePlan splits deferred sprites from viewport/swapchain HUD overlay sprites — same SpriteDrawRequest type, different queues/passes.

using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

internal struct PostProcessVolumeSubmission
{
    public PostProcessVolume Volume;
    public Vector2D<float> WorldPosition;
    public float WorldRotationRadians;
    public Vector2D<float> WorldScale;
}

/// <summary>
/// Immutable snapshot of everything needed to encode one presented frame: drained sprite/light queues, resolved camera,
/// letterbox mapping, and sort permutations. Built once per <see cref="VulkanRenderer.DrawFrame"/> by the nested <c>FramePlanBuilder</c>
/// in <c>VulkanRenderer.FrameExecution.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SpriteCount"/> / <see cref="ViewportUiOverlaySpriteCount"/> are authoritative batch sizes — backing arrays can be
/// longer (grow-only scratch); iterators must stop at the counts.
/// </para>
/// </remarks>
internal readonly struct FramePlan
{
    /// <summary>Deferred / world-sprites queue drain (not viewport HUD overlay).</summary>
    public readonly SpriteDrawRequest[] Sprites;
    /// <summary>Logical sprite count; backing <see cref="Sprites"/> may be a reused scratch buffer longer than this.</summary>
    public readonly int SpriteCount;
    public readonly PointLight[] PointLights;
    public readonly int PointLightCount;
    public readonly SpotLight[] SpotLights;
    public readonly int SpotLightCount;
    public readonly DirectionalLight[] DirectionalLights;
    public readonly int DirectionalLightCount;
    public readonly AmbientLight[] AmbientLights;
    public readonly int AmbientLightCount;
    public readonly PostProcessVolumeSubmission[] Volumes;
    public readonly int VolumeCount;
    public readonly GlobalPostProcessSettings GlobalPost;
    public readonly GlobalPostProcessSettings ResolvedPost;
    public readonly int[] SortIndices;
    /// <summary>Count of sprites with <see cref="SpriteDrawRequest.Transparent"/> true (after sort order).</summary>
    public readonly int TransparentSpriteCount;
    /// <summary>Swapchain (physical window) size in pixels.</summary>
    public readonly Vector2D<float> Screen;
    /// <summary>The camera selected for this frame (either highest-priority submission or the default).</summary>
    public readonly CameraViewRequest Camera;
    /// <summary>Letterbox mapping from the camera's virtual viewport to the swapchain.</summary>
    public readonly PhysicalViewport Physical;

    /// <summary>Opaque viewport/swapchain-space UI sprites composited after HDR (straight-alpha), separate from deferred G-buffer sprites.</summary>
    public readonly SpriteDrawRequest[] ViewportUiOverlaySprites;

    public readonly int ViewportUiOverlaySpriteCount;

    public readonly int[] ViewportUiOverlaySortIndices;

    public FramePlan(
        SpriteDrawRequest[] sprites,
        int spriteCount,
        PointLight[] pointLights,
        int pointLightCount,
        SpotLight[] spotLights,
        int spotLightCount,
        DirectionalLight[] directionalLights,
        int directionalLightCount,
        AmbientLight[] ambientLights,
        int ambientLightCount,
        PostProcessVolumeSubmission[] volumes,
        int volumeCount,
        in GlobalPostProcessSettings globalPost,
        in GlobalPostProcessSettings resolvedPost,
        int[] sortIndices,
        int transparentSpriteCount,
        in Vector2D<float> screen,
        in CameraViewRequest camera,
        in PhysicalViewport physical,
        SpriteDrawRequest[]? viewportUiOverlaySprites = null,
        int viewportUiOverlaySpriteCount = 0,
        int[]? viewportUiOverlaySortIndices = null)
    {
        Sprites = sprites;
        SpriteCount = spriteCount;
        PointLights = pointLights;
        PointLightCount = pointLightCount;
        SpotLights = spotLights;
        SpotLightCount = spotLightCount;
        DirectionalLights = directionalLights;
        DirectionalLightCount = directionalLightCount;
        AmbientLights = ambientLights;
        AmbientLightCount = ambientLightCount;
        Volumes = volumes;
        VolumeCount = volumeCount;
        GlobalPost = globalPost;
        ResolvedPost = resolvedPost;
        SortIndices = sortIndices;
        TransparentSpriteCount = transparentSpriteCount;
        Screen = screen;
        Camera = camera;
        Physical = physical;
        ViewportUiOverlaySprites = viewportUiOverlaySprites ?? [];
        ViewportUiOverlaySpriteCount = viewportUiOverlaySpriteCount;
        ViewportUiOverlaySortIndices = viewportUiOverlaySortIndices ?? [];
    }
}

internal interface IFramePlanBuilder
{
    /// <summary>Dequeues all pending work and returns an immutable <see cref="FramePlan"/> for GPU recording this frame.</summary>
    FramePlan Build();
}

internal interface IRenderBackendExecutor
{
    void Record(CommandBuffer cmd, Framebuffer swapFb, Framebuffer swapUiOverlayFb, in FramePlan framePlan);
}

internal readonly struct PostEffectContext
{
    public readonly CommandBuffer Cmd;
    public readonly Framebuffer SwapFramebuffer;
    public readonly FramePlan FramePlan;
    public readonly Viewport FullViewport;
    public readonly Rect2D FullScissor;
    public readonly Viewport HalfViewport;
    public readonly Rect2D HalfScissor;

    public PostEffectContext(
        CommandBuffer cmd,
        Framebuffer swapFramebuffer,
        in FramePlan framePlan,
        in Viewport fullViewport,
        in Rect2D fullScissor,
        in Viewport halfViewport,
        in Rect2D halfScissor)
    {
        Cmd = cmd;
        SwapFramebuffer = swapFramebuffer;
        FramePlan = framePlan;
        FullViewport = fullViewport;
        FullScissor = fullScissor;
        HalfViewport = halfViewport;
        HalfScissor = halfScissor;
    }
}

internal interface IPostEffect
{
    void RecreateTargets();
    void Record(in PostEffectContext context, ref ImageView workingView);
}
