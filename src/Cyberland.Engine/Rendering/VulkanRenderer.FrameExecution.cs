using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

public sealed unsafe partial class VulkanRenderer
{
    private sealed class FramePlanBuilder : IFramePlanBuilder
    {
        private readonly VulkanRenderer _r;

        public FramePlanBuilder(VulkanRenderer renderer) => _r = renderer;

        public FramePlan Build()
        {
            SpriteDrawRequest[] sprites;
            PointLight[] pointLights;
            SpotLight[] spotLights;
            DirectionalLight[] directionalLights;
            AmbientLight[] ambientLights;
            PostProcessVolume[] volumes;
            GlobalPostProcessSettings globalPost;
            lock (_r._recordLock)
            {
                sprites = _r._spriteQueue.ToArray();
                _r._spriteQueue.Clear();
                pointLights = _r._pointLightQueue.ToArray();
                _r._pointLightQueue.Clear();
                spotLights = _r._spotLightQueue.ToArray();
                _r._spotLightQueue.Clear();
                directionalLights = _r._directionalLightQueue.ToArray();
                _r._directionalLightQueue.Clear();
                ambientLights = _r._ambientLightQueue.ToArray();
                _r._ambientLightQueue.Clear();
                volumes = _r._volumeQueue.ToArray();
                _r._volumeQueue.Clear();
                globalPost = _r._globalPost;
            }

            var screen = new Vector2D<float>(_r._swapchainExtent.Width, _r._swapchainExtent.Height);
            var viewMin = new Vector2D<float>(0f, 0f);
            var viewMax = new Vector2D<float>(screen.X, screen.Y);
            var resolvedPost = PostProcessVolumeMerge.Resolve(in globalPost, volumes, viewMin, viewMax);

            var sortIndices = new int[sprites.Length];
            SpriteDrawSorter.SortByLayerOrder(sortIndices, sprites);
            return new FramePlan(sprites, pointLights, spotLights, directionalLights, ambientLights, volumes, in globalPost, in resolvedPost, sortIndices, in screen);
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
            _r._bloomPipeline!.Record(
                context.Cmd,
                bloomIntensity > 0f,
                bloomIntensity,
                context.FramePlan.ResolvedPost.EmissiveToBloomGain,
                context.HalfViewport,
                context.HalfScissor,
                out var bloomFinalView);
            return bloomFinalView;
        }

        public ImageView ClearBloom(in PostEffectContext context)
        {
            _r._bloomPipeline!.Record(
                context.Cmd,
                false,
                0f,
                context.FramePlan.ResolvedPost.EmissiveToBloomGain,
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
