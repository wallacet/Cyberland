using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

internal readonly struct FramePlan
{
    public readonly SpriteDrawRequest[] Sprites;
    public readonly PointLight[] PointLights;
    public readonly SpotLight[] SpotLights;
    public readonly DirectionalLight[] DirectionalLights;
    public readonly AmbientLight[] AmbientLights;
    public readonly PostProcessVolume[] Volumes;
    public readonly GlobalPostProcessSettings GlobalPost;
    public readonly GlobalPostProcessSettings ResolvedPost;
    public readonly int[] SortIndices;
    public readonly Vector2D<float> Screen;

    public FramePlan(
        SpriteDrawRequest[] sprites,
        PointLight[] pointLights,
        SpotLight[] spotLights,
        DirectionalLight[] directionalLights,
        AmbientLight[] ambientLights,
        PostProcessVolume[] volumes,
        in GlobalPostProcessSettings globalPost,
        in GlobalPostProcessSettings resolvedPost,
        int[] sortIndices,
        in Vector2D<float> screen)
    {
        Sprites = sprites;
        PointLights = pointLights;
        SpotLights = spotLights;
        DirectionalLights = directionalLights;
        AmbientLights = ambientLights;
        Volumes = volumes;
        GlobalPost = globalPost;
        ResolvedPost = resolvedPost;
        SortIndices = sortIndices;
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
