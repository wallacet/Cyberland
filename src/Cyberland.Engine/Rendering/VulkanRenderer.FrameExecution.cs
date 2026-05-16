using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cyberland.Engine.Diagnostics;
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
    private void EnsureBloomPipelineInitialized()
    {
        _bloomPipeline ??= new BloomPipeline(this);
    }

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
#if DEBUG
            using var __build = FrameProfilerScope.Enter("FramePlan.Build");
#endif
            int spriteCount, pointCount, spotCount, directionalCount, ambientCount, volumeCount, cameraCount;
            SpriteDrawRequest[] sprites = [];
            PointLight[] pointLights = [];
            SpotLight[] spotLights = [];
            DirectionalLight[] directionalLights = [];
            AmbientLight[] ambientLights = [];
            PostProcessVolumeSubmission[] volumes = [];
            CameraViewRequest[] cameras = [];
            GlobalPostProcessSettings globalPost;
            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("FramePlan.DrainQueues");
#endif
                spriteCount = DrainQueue(_r._spriteQueue, ref _r._frameScratchSprites, out sprites);
                pointCount = DrainQueue(_r._pointLightQueue, ref _r._frameScratchPointLights, out pointLights);
                spotCount = DrainQueue(_r._spotLightQueue, ref _r._frameScratchSpotLights, out spotLights);
                directionalCount = DrainQueue(_r._directionalLightQueue, ref _r._frameScratchDirectionalLights, out directionalLights);
                ambientCount = DrainQueue(_r._ambientLightQueue, ref _r._frameScratchAmbientLights, out ambientLights);
                volumeCount = DrainQueue(_r._volumeQueue, ref _r._frameScratchVolumes, out volumes);
                cameraCount = DrainQueue(_r._cameraQueue, ref _r._frameScratchCameras, out cameras);
            }

            lock (_r._globalPostLock)
            {
                globalPost = _r._globalPost;
            }

            var screen = new Vector2D<float>(_r._swapchainExtent.Width, _r._swapchainExtent.Height);
            var swapchainPixelSize = new Vector2D<int>((int)_r._swapchainExtent.Width, (int)_r._swapchainExtent.Height);
            var camera = CameraSelection.PickActive(cameras.AsSpan(0, cameraCount), swapchainPixelSize);
            var physical = CameraProjection.ComputePhysicalViewport(camera.ViewportSizeWorld, swapchainPixelSize);
            var presentationSize = CameraPresentationLayout.ResolvePresentationViewportSize(camera);
            var presentationPhysical = CameraProjection.ComputePhysicalViewport(presentationSize, swapchainPixelSize);

            // Publish the resolved viewport size so the NEXT RunFrame's mod systems (anchors, HUD layout) see the
            // camera chosen for THIS DrawFrame — stable virtual canvas size even under concurrent SubmitCamera races.
            lock (_r._cameraStateLock)
            {
                _r._activeCameraViewportSize = camera.ViewportSizeWorld;
                _r._activeCameraView = camera;
            }

            GlobalPostProcessSettings resolvedPost;
            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("FramePlan.PostMerge");
#endif
                resolvedPost = PostProcessVolumeMerge.ResolveAtPoint(
                    in globalPost,
                    volumes.AsSpan(0, volumeCount),
                    camera.PositionWorld);
            }

            var drainedSpriteCount = spriteCount;
            int[] sortIndices;
            if (drainedSpriteCount == 0)
                sortIndices = [];
            else
            {
                EnsureFrameScratch(ref _r._frameScratchSortIndices, drainedSpriteCount);
                sortIndices = _r._frameScratchSortIndices!;
                if (drainedSpriteCount > 1)
                {
#if DEBUG
                    using var __ = FrameProfilerScope.Enter("FramePlan.Sort.Sprites");
#endif
                    // Grow-only scratch may be longer than spriteCount; sort only [0, spriteCount) so stale tail slots are not compared.
                    SpriteDrawSorter.SortByLayerOrder(sortIndices, sprites, drainedSpriteCount);
                }
                else
                {
                    sortIndices[0] = 0;
                }
            }
            LightSubmissionPolicy.ClampWithDropCount(drainedSpriteCount, DeferredRenderingConstants.MaxDeferredSprites, out var droppedDeferredSprites);
            spriteCount = drainedSpriteCount - droppedDeferredSprites;
            if (droppedDeferredSprites > 0 && Interlocked.Exchange(ref _r._deferredSpriteOverflowWarningIssued, 1) == 0)
            {
                EngineDiagnostics.Report(
                    EngineErrorSeverity.Warning,
                    "Cyberland.Engine.Rendering",
                    $"Deferred sprite submissions exceeded cap ({DeferredRenderingConstants.MaxDeferredSprites}); dropped {droppedDeferredSprites} draws after deterministic layer/sort-key prioritization.");
            }

            var transparentSpriteCount = 0;
            for (var i = 0; i < spriteCount; i++)
            {
                if (sprites[sortIndices[i]].Transparent)
                    transparentSpriteCount++;
            }

            int voCount;
            SpriteDrawRequest[] voSprites;
            voCount = DrainQueue(_r._viewportUiOverlayQueue, ref _r._frameScratchViewportUiOverlay, out voSprites);
            LightSubmissionPolicy.ClampWithDropCount(voCount, DeferredRenderingConstants.MaxViewportOverlaySprites, out var droppedOverlaySprites);
            if (droppedOverlaySprites > 0 && Interlocked.Exchange(ref _r._overlaySpriteOverflowWarningIssued, 1) == 0)
            {
                EngineDiagnostics.Report(
                    EngineErrorSeverity.Warning,
                    "Cyberland.Engine.Rendering",
                    $"Viewport overlay sprite submissions exceeded cap ({DeferredRenderingConstants.MaxViewportOverlaySprites}); dropped {droppedOverlaySprites} draws.");
            }
            voCount -= droppedOverlaySprites;
            int[] voSort;
            if (voCount == 0)
                voSort = [];
            else
            {
                EnsureFrameScratch(ref _r._frameScratchViewportUiSortIndices, voCount);
                voSort = _r._frameScratchViewportUiSortIndices!;
                if (voCount > 1)
                {
#if DEBUG
                    using var __ = FrameProfilerScope.Enter("FramePlan.Sort.Overlay");
#endif
                    SpriteDrawSorter.SortByLayerOrder(voSort, voSprites, voCount);
                }
                else
                {
                    voSort[0] = 0;
                }
            }

            int textCount;
            TextGlyphDrawRequest[] textGlyphs;
            textCount = _r.DrainPendingTextGlyphs(ref _r._frameScratchTextGlyphs, out textGlyphs);
            LightSubmissionPolicy.ClampWithDropCount(textCount, DeferredRenderingConstants.MaxTextGlyphs, out var droppedTextGlyphs);
            if (droppedTextGlyphs > 0 && Interlocked.Exchange(ref _r._textGlyphOverflowWarningIssued, 1) == 0)
            {
                EngineDiagnostics.Report(
                    EngineErrorSeverity.Warning,
                    "Cyberland.Engine.Rendering",
                    $"Text glyph submissions exceeded cap ({DeferredRenderingConstants.MaxTextGlyphs}); dropped {droppedTextGlyphs} glyphs.");
            }
            textCount -= droppedTextGlyphs;
            int[] textSort;
            if (textCount == 0)
                textSort = [];
            else
            {
                EnsureFrameScratch(ref _r._frameScratchTextSortIndices, textCount);
                textSort = _r._frameScratchTextSortIndices!;
                if (textCount > 1)
                    TextGlyphSortComparer.SortByOrder(textSort, textGlyphs, textCount);
                else
                    textSort[0] = 0;
            }

            if (pointCount > DeferredRenderingConstants.MaxPointLights)
                LightSubmissionOrdering.SortPointLights(pointLights, pointCount);
            if (directionalCount > DeferredRenderingConstants.MaxDirectionalLights)
                LightSubmissionOrdering.SortDirectionalLights(directionalLights, directionalCount);
            if (spotCount > DeferredRenderingConstants.MaxSpotLights)
                LightSubmissionOrdering.SortSpotLights(spotLights, spotCount);

            LightSubmissionPolicy.ClampWithDropCount(pointCount, DeferredRenderingConstants.MaxPointLights, out var droppedPointLights);
            LightSubmissionPolicy.ClampWithDropCount(directionalCount, DeferredRenderingConstants.MaxDirectionalLights, out var droppedDirectionalLights);
            LightSubmissionPolicy.ClampWithDropCount(spotCount, DeferredRenderingConstants.MaxSpotLights, out var droppedSpotLights);

            return new FramePlan(
                sprites,
                spriteCount,
                pointLights,
                pointCount,
                droppedPointLights,
                spotLights,
                spotCount,
                droppedSpotLights,
                directionalLights,
                directionalCount,
                droppedDirectionalLights,
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
                in presentationPhysical,
                voSprites,
                voCount,
                voSort,
                textGlyphs,
                textCount,
                textSort);
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
        private readonly VulkanRenderer _r;
        private readonly IPostEffect _bloom;
        private readonly IPostEffect _composite;

        public PostProcessGraph(VulkanRenderer renderer)
        {
            _r = renderer;
            renderer.EnsureBloomPipelineInitialized();
            _bloom = new BloomEffect(renderer);
            _composite = new CompositeEffect(renderer);
        }

        public void Record(in PostEffectContext context, bool bloomOn, float bloomGain, float bloomRadius, in GlobalPostProcessSettings post)
        {
            Debug.Assert(
                RenderPassDependencyModel.IsExecutionOrderValid(
                [
                    RenderPassDependencyModel.PassStage.EmissivePrepass,
                    RenderPassDependencyModel.PassStage.GBufferOpaque,
                    RenderPassDependencyModel.PassStage.DeferredLighting,
                    RenderPassDependencyModel.PassStage.TransparentWboit,
                    RenderPassDependencyModel.PassStage.TransparentResolve,
                    RenderPassDependencyModel.PassStage.Bloom,
                    RenderPassDependencyModel.PassStage.CompositeToSwapchain
                ]),
                "Deferred frame pass ordering must satisfy RenderPassDependencyModel edges.");

            ImageView current = default;
            if (bloomOn)
            {
                _r.BeginGpuLabel(context.Cmd, "Pass.Bloom");
                try
                {
                    current = ((BloomEffect)_bloom).RecordBloom(in context, bloomRadius);
                }
                finally
                {
                    _r.EndGpuLabel(context.Cmd);
                }
            }
            else
            {
                _r.BeginGpuLabel(context.Cmd, "Pass.Bloom.Clear");
                try
                {
                    current = ((BloomEffect)_bloom).ClearBloom(in context);
                }
                finally
                {
                    _r.EndGpuLabel(context.Cmd);
                }
            }

            _r.BeginGpuLabel(context.Cmd, "Pass.CompositeToSwapchain");
            try
            {
                ((CompositeEffect)_composite).RecordComposite(in context, in post, current, bloomGain);
            }
            finally
            {
                _r.EndGpuLabel(context.Cmd);
            }
        }
    }

    private sealed class BloomEffect : IPostEffect
    {
        private readonly VulkanRenderer _r;

        public BloomEffect(VulkanRenderer renderer)
        {
            _r = renderer;
        }

        public void RecreateTargets() { }

        public void Record(in PostEffectContext context, ref ImageView workingView) => workingView = RecordBloom(in context, context.FramePlan.ResolvedPost.BloomRadius);

        public ImageView RecordBloom(in PostEffectContext context, float bloomRadius)
        {
            var rp = context.FramePlan.ResolvedPost;
            _r._bloomPipeline!.Record(
                context.Cmd,
                bloomRadius > 0f,
                bloomRadius,
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
                BloomSourceGain = post.EmissiveToBloomGain,
                ApplyManualDisplayGamma = _r._swapchainUsesSrgbFramebuffer ? 0f : 1f,
                Pad1 = 0f
            };

            _r._vk.CmdPushConstants(context.Cmd, _r._plComposite, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(CompositePush), &cp);
            _r._vk.CmdDraw(context.Cmd, 3, 1, 0, 0);
            _r._vk.CmdEndRenderPass(context.Cmd);
        }
    }
}
