using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

// Purpose: Frame plan construction (sprite/light queues → merged post), post-process graph (bloom + composite), and thin effect adapters.
// Threading: FramePlanBuilder reads queues under _recordLock; GPU Record methods run on the render thread.

/// <summary>ECS-to-GPU bridge: builds <see cref="FramePlan"/> and records bloom/composite passes.</summary>
public sealed unsafe partial class VulkanRenderer
{
    private static void EnsureFrameScratch<T>(ref T[]? buffer, int requiredLength)
    {
        if (requiredLength == 0)
            return;
        if (buffer is null || buffer.Length < requiredLength)
            Array.Resize(ref buffer, requiredLength);
    }

    /// <summary>
    /// Delegates full-frame recording to <see cref="RecordFullFrameCore"/> (keeps the entrypoint in one place).
    /// </summary>
    private sealed class RenderFrameRecorder
    {
        private readonly VulkanRenderer _r;

        public RenderFrameRecorder(VulkanRenderer renderer) => _r = renderer;

        public void Record(CommandBuffer cmd, Framebuffer swapFb) => _r.RecordFullFrameCore(cmd, swapFb);
    }

    private sealed class FramePlanBuilder : IFramePlanBuilder
    {
        private readonly VulkanRenderer _r;

        public FramePlanBuilder(VulkanRenderer renderer) => _r = renderer;

        public FramePlan Build()
        {
            int spriteCount, pointCount, spotCount, directionalCount, ambientCount, volumeCount;
            SpriteDrawRequest[] sprites = [];
            PointLight[] pointLights = [];
            SpotLight[] spotLights = [];
            DirectionalLight[] directionalLights = [];
            AmbientLight[] ambientLights = [];
            PostProcessVolumeSubmission[] volumes = [];
            GlobalPostProcessSettings globalPost;
            lock (_r._recordLock)
            {
                spriteCount = _r._spriteQueue.Count;
                if (spriteCount > 0)
                {
                    EnsureFrameScratch(ref _r._frameScratchSprites, spriteCount);
                    _r._spriteQueue.CopyTo(0, _r._frameScratchSprites!, 0, spriteCount);
                    sprites = _r._frameScratchSprites!;
                }
                _r._spriteQueue.Clear();

                pointCount = _r._pointLightQueue.Count;
                if (pointCount > 0)
                {
                    EnsureFrameScratch(ref _r._frameScratchPointLights, pointCount);
                    _r._pointLightQueue.CopyTo(0, _r._frameScratchPointLights!, 0, pointCount);
                    pointLights = _r._frameScratchPointLights!;
                }
                _r._pointLightQueue.Clear();

                spotCount = _r._spotLightQueue.Count;
                if (spotCount > 0)
                {
                    EnsureFrameScratch(ref _r._frameScratchSpotLights, spotCount);
                    _r._spotLightQueue.CopyTo(0, _r._frameScratchSpotLights!, 0, spotCount);
                    spotLights = _r._frameScratchSpotLights!;
                }
                _r._spotLightQueue.Clear();

                directionalCount = _r._directionalLightQueue.Count;
                if (directionalCount > 0)
                {
                    EnsureFrameScratch(ref _r._frameScratchDirectionalLights, directionalCount);
                    _r._directionalLightQueue.CopyTo(0, _r._frameScratchDirectionalLights!, 0, directionalCount);
                    directionalLights = _r._frameScratchDirectionalLights!;
                }
                _r._directionalLightQueue.Clear();

                ambientCount = _r._ambientLightQueue.Count;
                if (ambientCount > 0)
                {
                    EnsureFrameScratch(ref _r._frameScratchAmbientLights, ambientCount);
                    _r._ambientLightQueue.CopyTo(0, _r._frameScratchAmbientLights!, 0, ambientCount);
                    ambientLights = _r._frameScratchAmbientLights!;
                }
                _r._ambientLightQueue.Clear();

                volumeCount = _r._volumeQueue.Count;
                if (volumeCount > 0)
                {
                    EnsureFrameScratch(ref _r._frameScratchVolumes, volumeCount);
                    _r._volumeQueue.CopyTo(0, _r._frameScratchVolumes!, 0, volumeCount);
                    volumes = _r._frameScratchVolumes!;
                }
                _r._volumeQueue.Clear();

                globalPost = _r._globalPost;
            }

            var screen = new Vector2D<float>(_r._swapchainExtent.Width, _r._swapchainExtent.Height);
            var viewMin = new Vector2D<float>(0f, 0f);
            var viewMax = new Vector2D<float>(screen.X, screen.Y);
            var resolvedPost = PostProcessVolumeMerge.Resolve(in globalPost, volumes.AsSpan(0, volumeCount), viewMin, viewMax);

            int[] sortIndices;
            if (spriteCount == 0)
                sortIndices = [];
            else
            {
                EnsureFrameScratch(ref _r._frameScratchSortIndices, spriteCount);
                sortIndices = _r._frameScratchSortIndices!;
                // Scratch arrays are resized to exact spriteCount so SortByLayerOrder avoids partial-range Array.Sort overload ambiguity.
                SpriteDrawSorter.SortByLayerOrder(sortIndices, sprites);
            }

            var transparentSpriteCount = 0;
            if (spriteCount > 0)
            {
                for (var i = 0; i < spriteCount; i++)
                {
                    if (sprites[sortIndices[i]].Transparent)
                        transparentSpriteCount++;
                }
            }

            return new FramePlan(
                sprites,
                spriteCount,
                pointLights,
                pointCount,
                spotLights,
                spotCount,
                directionalLights,
                directionalCount,
                ambientLights,
                ambientCount,
                volumes,
                volumeCount,
                in globalPost,
                in resolvedPost,
                sortIndices,
                transparentSpriteCount,
                in screen);
        }
    }

    private sealed class RenderBackendExecutor : IRenderBackendExecutor
    {
        private readonly VulkanRenderer _r;

        public RenderBackendExecutor(VulkanRenderer renderer) => _r = renderer;

        public void Record(CommandBuffer cmd, Framebuffer swapFb, in FramePlan framePlan) =>
            _r.ExecuteFramePlanCore(cmd, swapFb, in framePlan);
    }

    private sealed class PostProcessGraph
    {
        private readonly IPostEffect _bloom;
        private readonly IPostEffect _composite;

        public PostProcessGraph(VulkanRenderer renderer)
        {
            _bloom = new BloomEffect(renderer);
            _composite = new CompositeEffect(renderer);
        }

        public void Record(in PostEffectContext context, bool bloomOn, float bloomGain, float bloomRadius, in GlobalPostProcessSettings post)
        {
            ImageView current = default;
            if (bloomOn)
                current = ((BloomEffect)_bloom).RecordBloom(in context, bloomRadius);
            else
                current = ((BloomEffect)_bloom).ClearBloom(in context);
            ((CompositeEffect)_composite).RecordComposite(in context, in post, current, bloomGain);
        }
    }

    private sealed class BloomEffect : IPostEffect
    {
        private readonly VulkanRenderer _r;

        public BloomEffect(VulkanRenderer renderer)
        {
            _r = renderer;
            _r._bloomPipeline ??= new BloomPipeline(_r);
        }

        public void RecreateTargets() { }

        public void Record(in PostEffectContext context, ref ImageView workingView) => workingView = RecordBloom(in context, context.FramePlan.ResolvedPost.BloomRadius);

        public ImageView RecordBloom(in PostEffectContext context, float bloomIntensity)
        {
            var rp = context.FramePlan.ResolvedPost;
            _r._bloomPipeline!.Record(
                context.Cmd,
                bloomIntensity > 0f,
                bloomIntensity,
                rp.EmissiveToBloomGain,
                rp.BloomExtractThreshold,
                rp.BloomExtractKnee,
                context.HalfViewport,
                context.HalfScissor,
                out var bloomFinalView);
            return bloomFinalView;
        }

        public ImageView ClearBloom(in PostEffectContext context)
        {
            var rp = context.FramePlan.ResolvedPost;
            _r._bloomPipeline!.Record(
                context.Cmd,
                false,
                0f,
                rp.EmissiveToBloomGain,
                rp.BloomExtractThreshold,
                rp.BloomExtractKnee,
                context.HalfViewport,
                context.HalfScissor,
                out var bloomFinalView);
            return bloomFinalView;
        }
    }

    private sealed class CompositeEffect : IPostEffect
    {
        private readonly VulkanRenderer _r;

        public CompositeEffect(VulkanRenderer renderer) => _r = renderer;

        public void RecreateTargets() { }

        public void Record(in PostEffectContext context, ref ImageView workingView) =>
            RecordComposite(
                in context,
                in context.FramePlan.ResolvedPost,
                workingView,
                context.FramePlan.ResolvedPost.BloomEnabled ? context.FramePlan.ResolvedPost.BloomGain : 0f);

        public void RecordComposite(in PostEffectContext context, in GlobalPostProcessSettings post, ImageView bloomView, float bloomGain)
        {
            _r.UpdateCompositeBloomSource(bloomView);
            ClearValue cSw = new()
            {
                Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 1f }
            };

            RenderPassBeginInfo rpS = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _r._rpComposite,
                Framebuffer = context.SwapFramebuffer,
                RenderArea = new Rect2D { Offset = default, Extent = _r._swapchainExtent },
                ClearValueCount = 1,
                PClearValues = &cSw
            };

            _r._vk!.CmdBeginRenderPass(context.Cmd, &rpS, SubpassContents.Inline);
            var vp = context.FullViewport;
            var sci = context.FullScissor;
            _r._vk.CmdSetViewport(context.Cmd, 0, 1, &vp);
            _r._vk.CmdSetScissor(context.Cmd, 0, 1, &sci);
            _r._vk.CmdBindPipeline(context.Cmd, PipelineBindPoint.Graphics, _r._pipeComposite);

            fixed (DescriptorSet* dsc = &_r._dsCompositeSlots[_r._currentFrame])
            {
                _r._vk.CmdBindDescriptorSets(context.Cmd, PipelineBindPoint.Graphics, _r._plComposite, 0, 1, dsc, 0, null);
            }

            var cp = new CompositePush
            {
                Bloom = post.BloomEnabled ? bloomGain : 0f,
                Exposure = post.Exposure,
                Saturation = post.Saturation,
                EmissiveHdrGain = post.EmissiveToHdrGain,
                EmissiveBloomGain = post.EmissiveToBloomGain,
                ApplyManualDisplayGamma = _r._swapchainUsesSrgbFramebuffer ? 0f : 1f,
                Pad1 = 0f
            };

            _r._vk.CmdPushConstants(context.Cmd, _r._plComposite, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(CompositePush), &cp);
            _r._vk.CmdDraw(context.Cmd, 3, 1, 0, 0);
            _r._vk.CmdEndRenderPass(context.Cmd);
        }
    }
}
