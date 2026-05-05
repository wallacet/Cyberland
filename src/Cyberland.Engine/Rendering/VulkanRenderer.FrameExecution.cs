using System.Collections.Concurrent;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

// -----------------------------------------------------------------------------
// Frame execution bridge (this file)
// -----------------------------------------------------------------------------
// After ECS submits sprites/lights/cameras into ConcurrentQueues, DrawFrame calls RecordFullFrameCore →
//   1) FramePlanBuilder.Build() — drains every queue into grow-only scratch arrays, resolves camera + letterbox,
//      sorts deferred sprites and viewport-overlay sprites separately (SpriteDrawSorter uses explicit counts!).
//   2) RenderBackendExecutor.Record → ExecuteFramePlanCore in Deferred.Recording.cs — Vulkan passes from the plan.
// PostProcessGraph here owns bloom + fullscreen composite to the swapchain (HDR pipeline earlier in Recording).
//
// Threading: Build runs on the window thread immediately before GPU encode; queues are concurrent-safe so parallel
// ECS workers can still be submitting until RunFrame returns — the host orders RunFrame before DrawFrame.

/// <summary>Partial <see cref="VulkanRenderer"/>: constructs <see cref="FramePlan"/> and hosts bloom/composite helpers.</summary>
public sealed unsafe partial class VulkanRenderer
{
    /// <summary>
    /// Ensures <paramref name="buffer"/> can hold at least <paramref name="requiredLength"/> elements (grow-only — never shrinks).
    /// Pair with <see cref="SpriteDrawSorter.SortByLayerOrder(int[], SpriteDrawRequest[], int)"/> using the drained count,
    /// not <c>buffer.Length</c>.
    /// </summary>
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

        public void Record(CommandBuffer cmd, Framebuffer swapFb, Framebuffer swapUiOverlayFb) =>
            _r.RecordFullFrameCore(cmd, swapFb, swapUiOverlayFb);
    }

    /// <summary>
    /// Drains all renderer submission queues into reusable scratch, computes camera + post merge + sort keys,
    /// publishes <see cref="VulkanRenderer.ActiveCameraViewportSize"/> for the <i>next</i> ECS tick.
    /// </summary>
    private sealed class FramePlanBuilder : IFramePlanBuilder
    {
        private readonly VulkanRenderer _r;

        public FramePlanBuilder(VulkanRenderer renderer) => _r = renderer;

        /// <inheritdoc cref="IFramePlanBuilder.Build"/>
        public FramePlan Build()
        {
            int spriteCount, pointCount, spotCount, directionalCount, ambientCount, volumeCount, cameraCount;
            SpriteDrawRequest[] sprites = [];
            PointLight[] pointLights = [];
            SpotLight[] spotLights = [];
            DirectionalLight[] directionalLights = [];
            AmbientLight[] ambientLights = [];
            PostProcessVolumeSubmission[] volumes = [];
            CameraViewRequest[] cameras = [];
            GlobalPostProcessSettings globalPost;
            spriteCount = DrainQueue(_r._spriteQueue, ref _r._frameScratchSprites, out sprites);
            pointCount = DrainQueue(_r._pointLightQueue, ref _r._frameScratchPointLights, out pointLights);
            spotCount = DrainQueue(_r._spotLightQueue, ref _r._frameScratchSpotLights, out spotLights);
            directionalCount = DrainQueue(_r._directionalLightQueue, ref _r._frameScratchDirectionalLights, out directionalLights);
            ambientCount = DrainQueue(_r._ambientLightQueue, ref _r._frameScratchAmbientLights, out ambientLights);
            volumeCount = DrainQueue(_r._volumeQueue, ref _r._frameScratchVolumes, out volumes);
            cameraCount = DrainQueue(_r._cameraQueue, ref _r._frameScratchCameras, out cameras);
            lock (_r._globalPostLock)
            {
                globalPost = _r._globalPost;
            }

            var screen = new Vector2D<float>(_r._swapchainExtent.Width, _r._swapchainExtent.Height);
            var swapchainPixelSize = new Vector2D<int>((int)_r._swapchainExtent.Width, (int)_r._swapchainExtent.Height);
            var camera = CameraSelection.PickActive(cameras.AsSpan(0, cameraCount), swapchainPixelSize);
            var physical = CameraProjection.ComputePhysicalViewport(camera.ViewportSizeWorld, swapchainPixelSize);

            // Publish the resolved viewport size so the NEXT RunFrame's mod systems (anchors, HUD layout) see the
            // camera chosen for THIS DrawFrame — stable virtual canvas size even under concurrent SubmitCamera races.
            lock (_r._cameraStateLock)
            {
                _r._activeCameraViewportSize = camera.ViewportSizeWorld;
                _r._activeCameraView = camera;
            }

            var resolvedPost = PostProcessVolumeMerge.ResolveAtPoint(
                in globalPost,
                volumes.AsSpan(0, volumeCount),
                camera.PositionWorld);

            int[] sortIndices;
            if (spriteCount == 0)
                sortIndices = [];
            else
            {
                EnsureFrameScratch(ref _r._frameScratchSortIndices, spriteCount);
                sortIndices = _r._frameScratchSortIndices!;
                // Grow-only scratch may be longer than spriteCount; sort only [0, spriteCount) so stale tail slots are not compared.
                SpriteDrawSorter.SortByLayerOrder(sortIndices, sprites, spriteCount);
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

            int voCount;
            SpriteDrawRequest[] voSprites;
            voCount = DrainQueue(_r._viewportUiOverlayQueue, ref _r._frameScratchViewportUiOverlay, out voSprites);
            int[] voSort;
            if (voCount == 0)
                voSort = [];
            else
            {
                EnsureFrameScratch(ref _r._frameScratchViewportUiSortIndices, voCount);
                voSort = _r._frameScratchViewportUiSortIndices!;
                SpriteDrawSorter.SortByLayerOrder(voSort, voSprites, voCount);
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
                in screen,
                in camera,
                in physical,
                voSprites,
                voCount,
                voSort);
        }

        /// <summary>Thin wrapper so <see cref="ConcurrentQueueDrain.DrainToScratch{T}"/> stays testable in isolation.</summary>
        private static int DrainQueue<T>(ConcurrentQueue<T> queue, ref T[]? scratch, out T[] result) =>
            ConcurrentQueueDrain.DrainToScratch(queue, ref scratch, out result);
    }

    private sealed class RenderBackendExecutor : IRenderBackendExecutor
    {
        private readonly VulkanRenderer _r;

        public RenderBackendExecutor(VulkanRenderer renderer) => _r = renderer;

        public void Record(CommandBuffer cmd, Framebuffer swapFb, Framebuffer swapUiOverlayFb, in FramePlan framePlan) =>
            _r.ExecuteFramePlanCore(cmd, swapFb, swapUiOverlayFb, in framePlan);
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
            // Clear the full swapchain with the camera's background color; the actual draw is scissored to the
            // letterbox rect via context.FullViewport / FullScissor so bar areas keep this clear value.
            var bg = context.FramePlan.Camera.BackgroundColor;
            ClearValue cSw = new()
            {
                Color = new ClearColorValue { Float32_0 = bg.X, Float32_1 = bg.Y, Float32_2 = bg.Z, Float32_3 = bg.W }
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
