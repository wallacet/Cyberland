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

internal readonly struct FramePlan
{
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
    public readonly Vector2D<float> Screen;

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
        in Vector2D<float> screen)
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
    }
}

internal interface IFramePlanBuilder
{
    FramePlan Build();
}

internal interface IRenderBackendExecutor
{
    void Record(CommandBuffer cmd, Framebuffer swapFb, in FramePlan framePlan);
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
