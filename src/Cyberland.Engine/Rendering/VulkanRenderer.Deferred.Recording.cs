using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.UI.Core;
using Glslang.NET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

// -----------------------------------------------------------------------------
// Deferred recording (this file)
// -----------------------------------------------------------------------------
// Consumes an immutable FramePlan from VulkanRenderer.FrameExecution.cs and records one command buffer:
// emissive prepass → G-buffer → lighting → transparency → resolve → post (bloom/composite in sibling partials).
// Viewport UI overlay draws happen last on the swapchain image (straight-alpha) — see RecordSwapchainUiOverlay.

/// <summary>Partial <see cref="VulkanRenderer"/>: Vulkan command encoding for the deferred HDR pipeline.</summary>
public sealed unsafe partial class VulkanRenderer
{
    /// <summary>Thin indirection so lazy recorder wiring stays in one place.</summary>
    private void RecordFullFrame(CommandBuffer cmd, Framebuffer swapFb, Framebuffer swapUiOverlayFb)
    {
        _renderFrameRecorder ??= new RenderFrameRecorder(this);
        _renderFrameRecorder.Record(cmd, swapFb, swapUiOverlayFb);
    }

    /// <summary>
    /// Snapshot pending CPU submissions into a <see cref="FramePlan"/>, then encode GPU work for this swapchain image.
    /// Called once per <see cref="DrawFrame"/> after acquire — queues are empty when this returns.
    /// </summary>
    private void RecordFullFrameCore(CommandBuffer cmd, Framebuffer swapFb, Framebuffer swapUiOverlayFb)
    {
        _framePlanBuilder ??= new FramePlanBuilder(this);
        _renderBackendExecutor ??= new RenderBackendExecutor(this);
        var framePlan = _framePlanBuilder.Build();
#if DEBUG
        using var __encodeFramePlan = FrameProfilerScope.Enter("DrawFrame.Record.EncodeFramePlan");
#endif
        _renderBackendExecutor.Record(cmd, swapFb, swapUiOverlayFb, in framePlan);
    }

    /// <summary>
    /// Encodes the full deferred pipeline + swapchain UI overlay from <paramref name="framePlan"/>.
    /// Kept separate from <see cref="RecordFullFrameCore"/> so tests could hypothetically inject plans.
    /// </summary>
    private void ExecuteFramePlanCore(CommandBuffer cmd, Framebuffer swapFb, Framebuffer swapUiOverlayFb, in FramePlan framePlan)
    {
        if (_vk!.ResetCommandBuffer(cmd, 0) != Result.Success)
            throw new InvalidOperationException("vkResetCommandBuffer failed.");

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };

        if (_vk.BeginCommandBuffer(cmd, in beginInfo) != Result.Success)
            throw new InvalidOperationException("vkBeginCommandBuffer failed.");

        BeginGpuLabel(cmd, "Frame");
        try
        {
        var screen = framePlan.Screen;
        var sortIdx = framePlan.SortIndices;
        var sprites = framePlan.Sprites;
        var nSprite = framePlan.SpriteCount;
        var overlaySpriteCap = framePlan.ViewportUiOverlaySpriteCount;
        // Host-visible instance VB is reused across emissive / opaque / transparent / overlay sprite passes. Recording the
        // full command buffer before submit means later CPU fills overwrite earlier draws unless each pass uses a
        // disjoint region (gpu executes after all writes — without regions, transparent pass clobbered opaque).
        var deferredStride = nSprite;
        var spriteInstanceCapacity = nSprite == 0 ? overlaySpriteCap : deferredStride * 3 + overlaySpriteCap;
        EnsureSpriteInstanceBufferCapacity(spriteInstanceCapacity);
        var emissiveInstanceBase = 0;
        var opaqueInstanceBase = deferredStride;
        var transparentInstanceBase = deferredStride * 2;
        var overlaySpriteInstanceBase = deferredStride * 3;
        var post = framePlan.ResolvedPost;
        ResetSpriteFrameCounters();
        _lastFrameSubmittedPointLights = framePlan.PointLightCount;
        _lastFrameSubmittedDirectionalLights = framePlan.DirectionalLightCount;
        _lastFrameSubmittedSpotLights = framePlan.SpotLightCount;
        _lastFrameDroppedPointLights = framePlan.PointLightDroppedCount;
        _lastFrameDroppedDirectionalLights = framePlan.DirectionalLightDroppedCount;
        _lastFrameDroppedSpotLights = framePlan.SpotLightDroppedCount;
        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Record.LightingUpload");
#endif
            BeginGpuLabel(cmd, "LightingUpload");
            try
            {
                UpdateLightingFrameData(in framePlan);
                UploadPointLightSsboData(in framePlan);
            }
            finally
            {
                EndGpuLabel(cmd);
            }
        }

        // Viewport / scissor bound to the physical (letterboxed) rectangle so offscreen passes don't waste
        // fragment work on bar areas. The bars keep whatever the attachment's clear value was (we drive HDR's
        // clear from the camera background color), so letterbox / pillarbox regions are visible but don't
        // participate in deferred lighting or transparency.
        var physical = framePlan.Physical;
        Viewport vp = new()
        {
            X = physical.OffsetPixels.X,
            Y = physical.OffsetPixels.Y,
            Width = physical.SizePixels.X,
            Height = physical.SizePixels.Y,
            MinDepth = 0f,
            MaxDepth = 1f
        };

        Rect2D sci = new()
        {
            Offset = new Offset2D { X = physical.OffsetPixels.X, Y = physical.OffsetPixels.Y },
            Extent = new Extent2D { Width = (uint)physical.SizePixels.X, Height = (uint)physical.SizePixels.Y }
        };
        var passRenderArea = sci;

        var vb = stackalloc[] { _vertexBuffer };
        var off = stackalloc ulong[] { 0 };

        ClearValue cEm = new()
        {
            Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
        };

        RenderPassBeginInfo rpEm = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = OffscreenRpFor(_offsWrittenEmissive),
            Framebuffer = _fbEmissive,
            RenderArea = passRenderArea,
            ClearValueCount = 1,
            PClearValues = &cEm
        };

        BeginGpuLabel(cmd, "Pass.EmissivePrepass");
        try
        {
        _vk.CmdBeginRenderPass(cmd, &rpEm, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeEmissive);

        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Record.EmissiveSprites");
#endif
            RecordDeferredSpritesEmissiveInstanced(cmd, in framePlan, sortIdx, sprites, nSprite, emissiveInstanceBase);
        }

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenEmissive = true;
        }
        finally
        {
            EndGpuLabel(cmd);
        }

        ClearValue cGb0 = cEm;
        ClearValue cGb1 = cEm;
        var cGbuf = stackalloc ClearValue[2];
        cGbuf[0] = cGb0;
        cGbuf[1] = cGb1;

        RenderPassBeginInfo rpGb = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = GbufferRpFor(_offsWrittenGbuffer),
            Framebuffer = _fbGbuffer,
            RenderArea = passRenderArea,
            ClearValueCount = 2,
            PClearValues = cGbuf
        };

        BeginGpuLabel(cmd, "Pass.GBufferOpaque");
        try
        {
        _vk.CmdBeginRenderPass(cmd, &rpGb, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeSpriteGbuffer);
        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Record.GbufferSprites");
#endif
            RecordDeferredSpritesOpaqueInstanced(cmd, in framePlan, sortIdx, sprites, nSprite, opaqueInstanceBase);
        }

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenGbuffer = true;
        }
        finally
        {
            EndGpuLabel(cmd);
        }

        // Camera-driven HDR clear: the active camera's background color doubles as the letterbox bar color
        // (pixels outside the scissor never get written by any pass, so the clear we pick here is what shows
        // through in the bars after the composite copies HDR → swapchain).
        var bg = framePlan.Camera.BackgroundColor;
        ClearValue cHdr = new()
        {
            Color = new ClearColorValue { Float32_0 = bg.X, Float32_1 = bg.Y, Float32_2 = bg.Z, Float32_3 = bg.W }
        };

        var screenPushHdr = stackalloc float[4];
        screenPushHdr[0] = screen.X;
        screenPushHdr[1] = screen.Y;
        screenPushHdr[2] = 0f;
        screenPushHdr[3] = 0f;

        var pointPushHdr = stackalloc float[8];
        pointPushHdr[0] = physical.OffsetPixels.X;
        pointPushHdr[1] = physical.OffsetPixels.Y;
        pointPushHdr[2] = physical.SizePixels.X;
        pointPushHdr[3] = physical.SizePixels.Y;
        pointPushHdr[4] = screen.X;
        pointPushHdr[5] = screen.Y;
        pointPushHdr[6] = 0f;
        pointPushHdr[7] = 0f;

        RenderPassBeginInfo rpH = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = OffscreenRpFor(_offsWrittenHdr),
            Framebuffer = _fbHdr,
            RenderArea = passRenderArea,
            ClearValueCount = 1,
            PClearValues = &cHdr
        };

        BeginGpuLabel(cmd, "Pass.DeferredLighting");
        try
        {
        _vk.CmdBeginRenderPass(cmd, &rpH, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);

        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Record.DeferredLighting");
#endif
            BeginGpuLabel(cmd, "Draw.DeferredBase");
            try
            {
                _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredBase);
                var setsBase = stackalloc DescriptorSet[2];
                setsBase[0] = _dsGbufferRead;
                setsBase[1] = _dsLighting;
                _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredBase, 0, 2, setsBase, 0, null);
                _vk.CmdPushConstants(cmd, _plDeferredBase, ShaderStageFlags.FragmentBit, 0, (uint)(sizeof(float) * 4), screenPushHdr);
                _vk.CmdDraw(cmd, 3, 1, 0, 0);
            }
            finally
            {
                EndGpuLabel(cmd);
            }

            BeginGpuLabel(cmd, "Draw.PointLights");
            try
            {
                _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredPoint);
                var setsPt = stackalloc DescriptorSet[2];
                setsPt[0] = _dsGbufferRead;
                setsPt[1] = _dsPointSsbo;
                _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredPoint, 0, 2, setsPt, 0, null);
                _vk.CmdPushConstants(cmd, _plDeferredPoint, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                    (uint)(sizeof(float) * 8), pointPushHdr);
                var nPt = LightSubmissionPolicy.ClampWithDropCount(
                    framePlan.PointLightCount,
                    DeferredRenderingConstants.MaxPointLights,
                    out _);
                if (nPt > 0)
                {
                    _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
                    _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);
                    _vk.CmdDrawIndexed(cmd, 6, (uint)nPt, 0, 0, 0);
                }
            }
            finally
            {
                EndGpuLabel(cmd);
            }

            BeginGpuLabel(cmd, "Draw.DeferredBleedEmissive");
            try
            {
                _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredBleed);
                var setsBl = stackalloc DescriptorSet[2];
                setsBl[0] = _dsGbufferRead;
                setsBl[1] = _dsEmissiveScene;
                _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredBleed, 0, 2, setsBl, 0, null);
                _vk.CmdPushConstants(cmd, _plDeferredBleed, ShaderStageFlags.FragmentBit, 0, (uint)(sizeof(float) * 4), screenPushHdr);
                _vk.CmdDraw(cmd, 3, 1, 0, 0);
            }
            finally
            {
                EndGpuLabel(cmd);
            }

            _vk.CmdEndRenderPass(cmd);
            _offsWrittenHdr = true;
        }

        }
        finally
        {
            EndGpuLabel(cmd);
        }

        var hasTransparentSprites = framePlan.TransparentSpriteCount > 0;
        if (hasTransparentSprites)
            RecordTransparentWboitAndResolve(cmd, in framePlan, in vp, in sci, sortIdx, sprites, nSprite, transparentInstanceBase, in cHdr, screenPushHdr);

        RecordPostProcessAndSwapchainOverlay(
            cmd,
            swapFb,
            swapUiOverlayFb,
            in framePlan,
            in vp,
            in sci,
            in post,
            hasTransparentSprites,
            overlaySpriteInstanceBase);

        }
        finally
        {
            EndGpuLabel(cmd);
        }

        if (_vk.EndCommandBuffer(cmd) != Result.Success)
            throw new InvalidOperationException("vkEndCommandBuffer failed.");
    }

    private void RecordTransparentWboitAndResolve(
        CommandBuffer cmd,
        in FramePlan framePlan,
        in Viewport vp,
        in Rect2D sci,
        int[] sortIdx,
        SpriteDrawRequest[] sprites,
        int nSprite,
        int transparentInstanceBase,
        in ClearValue hdrClearColor,
        float* screenPushHdr)
    {
#if DEBUG
        using var __ = FrameProfilerScope.Enter("Record.TransparentWboit");
#endif
        var vpLocal = vp;
        var sciLocal = sci;
        BeginGpuLabel(cmd, "Pass.TransparentWboit");
        try
        {
        ClearValue cWAccum = new()
        {
            Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
        };
        ClearValue cWReveal = new()
        {
            Color = new ClearColorValue { Float32_0 = 1f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
        };
        var cWboit = stackalloc ClearValue[2];
        cWboit[0] = cWAccum;
        cWboit[1] = cWReveal;

        RenderPassBeginInfo rpW = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = WboitRpFor(_offsWrittenWboit),
            Framebuffer = _fbWboit,
            RenderArea = sci,
            ClearValueCount = 2,
            PClearValues = cWboit
        };

        _vk!.CmdBeginRenderPass(cmd, &rpW, SubpassContents.Inline);
        vpLocal = vp;
        sciLocal = sci;
        _vk.CmdSetViewport(cmd, 0, 1, &vpLocal);
        _vk.CmdSetScissor(cmd, 0, 1, &sciLocal);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeTransparentWboit);
        var vb = stackalloc[] { _vertexBuffer };
        var off = stackalloc ulong[] { 0 };
        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        RecordDeferredSpritesTransparentInstanced(cmd, in framePlan, sortIdx, sprites, nSprite, transparentInstanceBase);

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenWboit = true;

        }
        finally
        {
            EndGpuLabel(cmd);
        }

        BeginGpuLabel(cmd, "Pass.TransparentResolve");
        try
        {
        // Keep transparent resolve bars consistent with the opaque-only path: use camera-driven HDR clear.
        ClearValue cRes = hdrClearColor;
        RenderPassBeginInfo rpRes = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = OffscreenRpFor(_offsWrittenHdrComposite),
            Framebuffer = _fbHdrComposite,
            RenderArea = sci,
            ClearValueCount = 1,
            PClearValues = &cRes
        };

        _vk.CmdBeginRenderPass(cmd, &rpRes, SubpassContents.Inline);
        vpLocal = vp;
        sciLocal = sci;
        _vk.CmdSetViewport(cmd, 0, 1, &vpLocal);
        _vk.CmdSetScissor(cmd, 0, 1, &sciLocal);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeTransparentResolve);
        fixed (DescriptorSet* dsTr = &_dsTransparentResolve)
        {
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plTransparentResolve, 0, 1, dsTr, 0, null);
        }

        _vk.CmdPushConstants(cmd, _plTransparentResolve, ShaderStageFlags.FragmentBit, 0, (uint)(sizeof(float) * 4), screenPushHdr);
        _vk.CmdDraw(cmd, 3, 1, 0, 0);
        _vk.CmdEndRenderPass(cmd);
        _offsWrittenHdrComposite = true;
        }
        finally
        {
            EndGpuLabel(cmd);
        }
    }

    private void RecordPostProcessAndSwapchainOverlay(
        CommandBuffer cmd,
        Framebuffer swapFb,
        Framebuffer swapUiOverlayFb,
        in FramePlan framePlan,
        in Viewport vp,
        in Rect2D sci,
        in GlobalPostProcessSettings post,
        bool hasTransparentSprites,
        int overlaySpriteInstanceBase)
    {
        UpdateSceneHdrSourcesForPostProcess(hasTransparentSprites ? _viewHdrComposite : _viewHdr);

        var bloomGain = post.BloomEnabled ? post.BloomGain : 0f;
        var bloomOn = bloomGain > 0f;
        var bloomRadius = post.BloomRadius;

        BeginGpuLabel(cmd, "Pass.PostProcess");
        try
        {
        Viewport vpHalf = new()
        {
            X = 0f,
            Y = 0f,
            Width = _bloomHalfW,
            Height = _bloomHalfH,
            MinDepth = 0f,
            MaxDepth = 1f
        };

        Rect2D sciHalf = new()
        {
            Offset = default,
            Extent = new Extent2D { Width = _bloomHalfW, Height = _bloomHalfH }
        };

        {
#if DEBUG
            using var __ = FrameProfilerScope.Enter("Record.PostProcess");
#endif
            _postProcessGraph ??= new PostProcessGraph(this);
            var ppContext = new PostEffectContext(cmd, swapFb, framePlan, vp, sci, vpHalf, sciHalf);
            _postProcessGraph.Record(in ppContext, bloomOn, bloomGain, bloomRadius, post);
        }

        }
        finally
        {
            EndGpuLabel(cmd);
        }

        if (framePlan.ViewportUiOverlaySpriteCount > 0 || framePlan.TextGlyphCount > 0)
        {
            BeginGpuLabel(cmd, "Transition.CompositeToSwapchainOverlay");
            try
            {
                BarrierCompositeColorToSwapchainUiOverlay(cmd);
            }
            finally
            {
                EndGpuLabel(cmd);
            }

            BeginGpuLabel(cmd, "Pass.SwapchainOverlay");
            try
            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("Record.SwapchainOverlay");
#endif
                RecordSwapchainUiOverlay(cmd, swapUiOverlayFb, in framePlan, in vp, in sci, overlaySpriteInstanceBase);
            }
            finally
            {
                EndGpuLabel(cmd);
            }
        }
    }

    private void BarrierCompositeColorToSwapchainUiOverlay(CommandBuffer cmd)
    {
        MemoryBarrier barrier = new()
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit
        };
        _vk!.CmdPipelineBarrier(
            cmd,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            0,
            1,
            &barrier,
            0,
            null,
            0,
            null);
    }

    private void RecordSwapchainUiOverlay(CommandBuffer cmd, Framebuffer uiFb, in FramePlan framePlan, in Viewport vp, in Rect2D sci, int overlaySpriteInstanceBase)
    {
        var n = framePlan.ViewportUiOverlaySpriteCount;
        var textCount = framePlan.TextGlyphCount;
        if (n == 0 && textCount == 0)
            return;

        RenderPassBeginInfo rpUi = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _rpSwapchainUiOverlay,
            Framebuffer = uiFb,
            RenderArea = sci,
            ClearValueCount = 0,
            PClearValues = null
        };

        _vk!.CmdBeginRenderPass(cmd, &rpUi, SubpassContents.Inline);
        var vpUi = vp;
        var sciUi = sci;
        _vk.CmdSetViewport(cmd, 0, 1, &vpUi);
        _vk.CmdSetScissor(cmd, 0, 1, &sciUi);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeSwapchainUiOverlay);

        var vb = stackalloc[] { _vertexBuffer };
        var off = stackalloc ulong[] { 0 };
        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        if (n > 0)
            RecordSwapchainOverlaySpritesInstanced(cmd, in framePlan, sciUi, overlaySpriteInstanceBase);

        if (textCount > 0)
            DrawTextSwapchainUi(cmd, in framePlan, sciUi);

        _vk.CmdEndRenderPass(cmd);
    }

    private void DrawTextSwapchainUi(CommandBuffer cmd, in FramePlan framePlan, in Rect2D passScissor)
    {
        var textCount = framePlan.TextGlyphCount;
        if (textCount <= 0)
        {
            _lastFrameTextGlyphInstances = 0;
            _lastFrameTextBatchCount = 0;
            _lastFrameTextDrawCalls = 0;
            return;
        }

        EnsureTextInstanceBufferCapacity(textCount);

        var sprites = framePlan.TextGlyphs;
        var sortIdx = framePlan.TextGlyphSortIndices;
        var upload = new Span<TextGlyphInstanceGpu>(_textInstanceBufferMapped, textCount);
        var valid = 0;
        for (var si = 0; si < textCount; si++)
        {
            ref readonly var g = ref sprites[sortIdx[si]];
            if (!TryBuildTextGlyphInstance(in g, in framePlan, out var inst))
                continue;
            upload[valid++] = inst;
        }

        if (valid == 0)
        {
            _lastFrameTextGlyphInstances = 0;
            _lastFrameTextBatchCount = 0;
            _lastFrameTextDrawCalls = 0;
            return;
        }

        var instBind = stackalloc[] { _textInstanceBuffer };
        var instOffset = stackalloc ulong[] { 0 };
        _vk!.CmdBindVertexBuffers(cmd, 1, 1, instBind, instOffset);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeTextMsdf);

        var push = new TextMsdfPushData
        {
            ViewportPhysical = new Vector4D<float>(
                framePlan.Physical.OffsetPixels.X,
                framePlan.Physical.OffsetPixels.Y,
                framePlan.Physical.SizePixels.X,
                framePlan.Physical.SizePixels.Y),
            Screen = framePlan.Screen,
            EdgeSharpness = TextMsdfEdgeSharpness
        };
        _vk.CmdPushConstants(cmd, _plTextMsdf, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
            (uint)sizeof(TextMsdfPushData), &push);

        BeginGpuLabel(cmd, "Draw.TextUi.Batch");
        try
        {
        var first = 0;
        var batchCount = 0;
        var drawCount = 0;
        while (first < valid)
        {
            ref readonly var startReq = ref sprites[sortIdx[first]];
            var tex = TryGetTextureSlot(startReq.TextureId);
            if (tex is null)
            {
                first++;
                continue;
            }

            var scissor = startReq.ViewportClipEnabled
                ? ViewportClipRectToSwapchainScissor(in startReq.ViewportClipRect, in framePlan, in passScissor)
                : passScissor;
            _vk.CmdSetScissor(cmd, 0, 1, &scissor);
            var set = tex.DescriptorSet;
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plTextMsdf, 0, 1, &set, 0, null);

            var run = 1;
            while (first + run < valid)
            {
                ref readonly var nextReq = ref sprites[sortIdx[first + run]];
                if (nextReq.TextureId != startReq.TextureId ||
                    nextReq.ViewportClipEnabled != startReq.ViewportClipEnabled)
                    break;
                if (nextReq.ViewportClipEnabled && !nextReq.ViewportClipRect.Equals(startReq.ViewportClipRect))
                    break;
                run++;
            }

            _vk.CmdDrawIndexed(cmd, 6, (uint)run, 0, 0, (uint)first);
            batchCount++;
            drawCount++;
            first += run;
        }

        _lastFrameTextGlyphInstances = valid;
        _lastFrameTextBatchCount = batchCount;
        _lastFrameTextDrawCalls = drawCount;
        }
        finally
        {
            EndGpuLabel(cmd);
        }
    }

    private bool TryBuildTextGlyphInstance(in TextGlyphDrawRequest g, in FramePlan plan, out TextGlyphInstanceGpu outInst)
    {
        outInst = default;
        Vector2D<float> px;
        if (g.Space == Scene.CoordinateSpace.ViewportSpace)
        {
            px = CameraProjection.ViewportPixelToSwapchainPixel(g.Center, in plan.Physical);
        }
        else if (g.Space == Scene.CoordinateSpace.SwapchainSpace)
        {
            px = g.Center;
        }
        else
        {
            var viewportSize = new Vector2D<float>(plan.Camera.ViewportSizeWorld.X, plan.Camera.ViewportSizeWorld.Y);
            var vpPixel = CameraProjection.WorldToViewportPixel(
                g.Center,
                plan.Camera.PositionWorld,
                plan.Camera.RotationRadians,
                viewportSize);
            px = CameraProjection.ViewportPixelToSwapchainPixel(vpPixel, in plan.Physical);
        }

        // Align sprite centers to integer swapchain pixels. Letterboxing uses a fractional uniform scale; without this,
        // bilinear MSDF sampling lands between LCD texels and reads persistently soft at typical DPI.
        px = new Vector2D<float>(MathF.Round(px.X), MathF.Round(px.Y));

        var uv = g.UvRect;
        if (uv.X == 0f && uv.Y == 0f && uv.Z == 0f && uv.W == 0f)
            uv = new Vector4D<float>(0f, 0f, 1f, 1f);

        var halfX = g.HalfExtents.X * plan.Physical.Scale;
        var halfY = g.HalfExtents.Y * plan.Physical.Scale;
        // Snap extents to half-pixel increments so scaled quads align better under non-integer letterbox scales.
        halfX = MathF.Max(0.5f, MathF.Round(halfX * 2f) * 0.5f);
        halfY = MathF.Max(0.5f, MathF.Round(halfY * 2f) * 0.5f);

        outInst = new TextGlyphInstanceGpu
        {
            CenterHalfPx = new Vector4D<float>(px.X, px.Y, halfX, halfY),
            UvRect = uv,
            Color = g.Color,
            MsdfParams = new Vector4D<float>(g.MsdfPixelRange, 0f, 0f, 0f)
        };
        return true;
    }

    private void EnsureTextInstanceBufferCapacity(int requiredGlyphs)
    {
        if (requiredGlyphs <= _textInstanceCapacity && _textInstanceBufferMapped != null)
            return;

        if (_textInstanceBufferMapped != null && _textInstanceBufferMemory.Handle != default)
        {
            _vk!.UnmapMemory(_device, _textInstanceBufferMemory);
            _textInstanceBufferMapped = null;
        }
        if (_textInstanceBuffer.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _textInstanceBuffer, null);
            _textInstanceBuffer = default;
        }
        if (_textInstanceBufferMemory.Handle != default)
        {
            _vk!.FreeMemory(_device, _textInstanceBufferMemory, null);
            _textInstanceBufferMemory = default;
        }

        // Favor fewer large reallocations over frequent small growth spikes during HUD/tutorial bursts.
        _textInstanceCapacity = Math.Max(requiredGlyphs, Math.Max(4096, _textInstanceCapacity * 2));
        var bytes = (ulong)(_textInstanceCapacity * sizeof(TextGlyphInstanceGpu));
        CreateHostVisibleBuffer(bytes, BufferUsageFlags.VertexBufferBit, out _textInstanceBuffer, out _textInstanceBufferMemory);
        void* map;
        if (_vk!.MapMemory(_device, _textInstanceBufferMemory, 0, bytes, 0, &map) != Result.Success)
            throw new InvalidOperationException("map text instance buffer failed.");
        _textInstanceBufferMapped = map;
    }

    /// <summary>
    /// Viewport clip (+Y down) → swapchain scissor, intersected with <paramref name="passScissor"/>.
    /// </summary>
    private static Rect2D ViewportClipRectToSwapchainScissor(in UiRect clipVp, in FramePlan plan, in Rect2D passScissor)
    {
        var tl = CameraProjection.ViewportPixelToSwapchainPixel(new Vector2D<float>(clipVp.X, clipVp.Y), plan.Physical);
        var br = CameraProjection.ViewportPixelToSwapchainPixel(new Vector2D<float>(clipVp.Right, clipVp.Bottom),
            plan.Physical);

        var minX = MathF.Min(tl.X, br.X);
        var minY = MathF.Min(tl.Y, br.Y);
        var maxX = MathF.Max(tl.X, br.X);
        var maxY = MathF.Max(tl.Y, br.Y);

        var ix0 = (int)Math.Floor(minX);
        var iy0 = (int)Math.Floor(minY);
        var ix1 = (int)Math.Ceiling(maxX);
        var iy1 = (int)Math.Ceiling(maxY);

        var px0 = (int)passScissor.Offset.X;
        var py0 = (int)passScissor.Offset.Y;
        var px1 = px0 + (int)passScissor.Extent.Width;
        var py1 = py0 + (int)passScissor.Extent.Height;

        var cx0 = Math.Max(0, Math.Max(ix0, px0));
        var cy0 = Math.Max(0, Math.Max(iy0, py0));
        var cx1 = Math.Min(ix1, px1);
        var cy1 = Math.Min(iy1, py1);
        var cw = Math.Max(0, cx1 - cx0);
        var ch = Math.Max(0, cy1 - cy0);

        return new Rect2D
        {
            Offset = new Offset2D { X = cx0, Y = cy0 },
            Extent = new Extent2D { Width = (uint)cw, Height = (uint)ch }
        };
    }

    /// <summary>
    /// sprite_emissive.frag scales radiance by emissive.w; when intensity is zero skip the instance so additive blending
    /// does not accumulate alphas into the emissive RT for non-emissive sprites.
    /// </summary>
    private static bool NeedsEmissivePrepass(in SpriteDrawRequest s) =>
        s.EmissiveIntensity > 1e-5f;

    private void ResetSpriteFrameCounters()
    {
        _lastFrameSubmittedPointLights = 0;
        _lastFrameSubmittedDirectionalLights = 0;
        _lastFrameSubmittedSpotLights = 0;
        _lastFrameDroppedPointLights = 0;
        _lastFrameDroppedDirectionalLights = 0;
        _lastFrameDroppedSpotLights = 0;
        _lastFrameOverlaySpriteInstances = 0;
        _lastFrameOverlaySpriteBatchCount = 0;
        _lastFrameOverlaySpriteDrawCalls = 0;
        _lastFrameDeferredEmissiveSpriteInstances = 0;
        _lastFrameDeferredEmissiveSpriteBatchCount = 0;
        _lastFrameDeferredEmissiveSpriteDrawCalls = 0;
        _lastFrameDeferredOpaqueSpriteInstances = 0;
        _lastFrameDeferredOpaqueSpriteBatchCount = 0;
        _lastFrameDeferredOpaqueSpriteDrawCalls = 0;
        _lastFrameDeferredTransparentSpriteInstances = 0;
        _lastFrameDeferredTransparentSpriteBatchCount = 0;
        _lastFrameDeferredTransparentSpriteDrawCalls = 0;
    }

    private void EnsureSpriteInstanceBufferCapacity(int requiredInstances)
    {
        if (requiredInstances <= _spriteInstanceCapacity && _spriteInstanceBufferMapped != null)
            return;

        if (_spriteInstanceBufferMapped != null && _spriteInstanceBufferMemory.Handle != default)
        {
            _vk!.UnmapMemory(_device, _spriteInstanceBufferMemory);
            _spriteInstanceBufferMapped = null;
        }

        if (_spriteInstanceBuffer.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _spriteInstanceBuffer, null);
            _spriteInstanceBuffer = default;
        }

        if (_spriteInstanceBufferMemory.Handle != default)
        {
            _vk!.FreeMemory(_device, _spriteInstanceBufferMemory, null);
            _spriteInstanceBufferMemory = default;
        }

        // Favor fewer large reallocations over frequent small growth spikes during particle-heavy frames.
        _spriteInstanceCapacity = Math.Max(requiredInstances, Math.Max(4096, _spriteInstanceCapacity * 2));
        var bytes = (ulong)(_spriteInstanceCapacity * sizeof(SpriteInstanceGpu));
        CreateHostVisibleBuffer(bytes, BufferUsageFlags.VertexBufferBit, out _spriteInstanceBuffer, out _spriteInstanceBufferMemory);
        void* map;
        if (_vk!.MapMemory(_device, _spriteInstanceBufferMemory, 0, bytes, 0, &map) != Result.Success)
            throw new InvalidOperationException("map sprite instance buffer failed.");
        _spriteInstanceBufferMapped = map;
    }

    private static SpriteInstancingPush BuildSpriteInstancingPush(in FramePlan plan)
    {
        var phys = plan.Physical;
        return new SpriteInstancingPush
        {
            ViewportPhysical = new Vector4D<float>(phys.OffsetPixels.X, phys.OffsetPixels.Y, phys.SizePixels.X, phys.SizePixels.Y)
        };
    }

    private bool TryBuildSpriteInstance(in SpriteDrawRequest s, in FramePlan plan, out SpriteInstanceGpu inst)
    {
        inst = default;
        var al = TryGetTextureSlot(s.AlbedoTextureId);
        if (al is null)
            return false;

        var viewportSize = new Vector2D<float>(plan.Camera.ViewportSizeWorld.X, plan.Camera.ViewportSizeWorld.Y);
        Vector2D<float> px;
        float rotScreen;
        if (s.Space == Scene.CoordinateSpace.ViewportSpace)
        {
            rotScreen = s.RotationRadians;
            px = CameraProjection.ViewportPixelToSwapchainPixel(s.CenterWorld, in plan.Physical);
        }
        else if (s.Space == Scene.CoordinateSpace.SwapchainSpace)
        {
            rotScreen = s.RotationRadians;
            px = s.CenterWorld;
        }
        else
        {
            var vpPixel = CameraProjection.WorldToViewportPixel(
                s.CenterWorld,
                plan.Camera.PositionWorld,
                plan.Camera.RotationRadians,
                viewportSize);
            rotScreen = s.RotationRadians - plan.Camera.RotationRadians;
            px = CameraProjection.ViewportPixelToSwapchainPixel(vpPixel, in plan.Physical);
        }

        var halfX = s.HalfExtentsWorld.X * plan.Physical.Scale;
        var halfY = s.HalfExtentsWorld.Y * plan.Physical.Scale;
        var uv = s.UvRect;
        if (uv.X == 0f && uv.Y == 0f && uv.Z == 0f && uv.W == 0f)
            uv = new Vector4D<float>(0f, 0f, 1f, 1f);

        // MaxValue = no emissive map (see Sprite). Do not treat slot 0 as "has map" when authors used default/new Sprite().
        var useEm = s.EmissiveTextureId != TextureId.MaxValue && TryGetTextureSlot(s.EmissiveTextureId) is not null ? 1 : 0;
        inst = new SpriteInstanceGpu
        {
            CenterHalfPx = new Vector4D<float>(px.X, px.Y, halfX, halfY),
            UvRect = uv,
            ColorAlpha = new Vector4D<float>(s.ColorMultiply.X * s.Alpha, s.ColorMultiply.Y * s.Alpha, s.ColorMultiply.Z * s.Alpha, s.ColorMultiply.W * s.Alpha),
            EmissiveRgbIntensity = new Vector4D<float>(s.EmissiveTint.X, s.EmissiveTint.Y, s.EmissiveTint.Z, s.EmissiveIntensity),
            RotAndFlags = new Vector4D<float>(0f, 0f, rotScreen, useEm)
        };
        return true;
    }

    private void RecordDeferredSpritesEmissiveInstanced(CommandBuffer cmd, in FramePlan plan, int[] sortIdx,
        SpriteDrawRequest[] sprites, int nSprite, int instanceBase)
    {
        if (nSprite == 0)
            return;
        var push = BuildSpriteInstancingPush(in plan);
        var rented = ArrayPool<int>.Shared.Rent(nSprite);
        var emissiveTextureIds = ArrayPool<TextureId>.Shared.Rent(nSprite);
        var emissiveEnabled = ArrayPool<int>.Shared.Rent(nSprite);
        try
        {
            var upload = new Span<SpriteInstanceGpu>(
                (SpriteInstanceGpu*)_spriteInstanceBufferMapped! + instanceBase, nSprite);
            var valid = 0;
            for (var si = 0; si < nSprite; si++)
            {
                var idx = sortIdx[si];
                ref readonly var s = ref sprites[idx];
                if (!NeedsEmissivePrepass(in s))
                    continue;
                if (!TryBuildSpriteInstance(in s, in plan, out var inst))
                    continue;
                GpuTexture? emissiveSlot = null;
                if (s.EmissiveTextureId != TextureId.MaxValue)
                    emissiveSlot = TryGetTextureSlot(s.EmissiveTextureId);
                emissiveEnabled[valid] = emissiveSlot is not null ? 1 : 0;
                emissiveTextureIds[valid] = emissiveEnabled[valid] != 0 ? s.EmissiveTextureId : _blackTextureId;
                upload[valid] = inst;
                rented[valid++] = idx;
            }

            if (valid == 0)
                return;

            BeginGpuLabel(cmd, "Draw.SpritesEmissive.Batch");
            try
            {
            var instBind = stackalloc[] { _spriteInstanceBuffer };
            var instOff = stackalloc ulong[] { 0 };
            _vk!.CmdBindVertexBuffers(cmd, 1, 1, instBind, instOff);
            _vk.CmdPushConstants(cmd, _plSpriteTwoTexture, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpriteInstancingPush), &push);

            var first = 0;
            var batchCount = 0;
            var drawCount = 0;
            var setsE = stackalloc DescriptorSet[2];
            while (first < valid)
            {
                ref readonly var startS = ref sprites[rented[first]];
                var al = TryGetTextureSlot(startS.AlbedoTextureId);
                if (al is null)
                {
                    first++;
                    continue;
                }

                var useEmStart = emissiveEnabled[first];
                var emTexStart = emissiveTextureIds[first];
                var emSlotStart = TryGetTextureSlot(emTexStart);
                if (emSlotStart is null)
                {
                    first++;
                    continue;
                }

                var run = 1;
                while (first + run < valid)
                {
                    ref readonly var prevS = ref sprites[rented[first + run - 1]];
                    ref readonly var nextS = ref sprites[rented[first + run]];
                    var useEmPrev = emissiveEnabled[first + run - 1];
                    var useEmNext = emissiveEnabled[first + run];
                    var emTexPrev = emissiveTextureIds[first + run - 1];
                    var emTexNext = emissiveTextureIds[first + run];
                    if (!SpriteBatchRuns.DeferredEmissiveRunCanExtend(in prevS, in nextS, emTexPrev, emTexNext, useEmPrev, useEmNext))
                        break;
                    run++;
                }

                setsE[0] = al.DescriptorSet;
                setsE[1] = emSlotStart.DescriptorSet;
                _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteTwoTexture, 0, 2, setsE, 0, null);
                _vk.CmdDrawIndexed(cmd, 6, (uint)run, 0, 0, (uint)(instanceBase + first));
                batchCount++;
                drawCount++;
                first += run;
            }

            _lastFrameDeferredEmissiveSpriteInstances = valid;
            _lastFrameDeferredEmissiveSpriteBatchCount = batchCount;
            _lastFrameDeferredEmissiveSpriteDrawCalls = drawCount;
            }
            finally
            {
                EndGpuLabel(cmd);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
            ArrayPool<TextureId>.Shared.Return(emissiveTextureIds);
            ArrayPool<int>.Shared.Return(emissiveEnabled);
        }
    }

    private void RecordDeferredSpritesOpaqueInstanced(CommandBuffer cmd, in FramePlan plan, int[] sortIdx,
        SpriteDrawRequest[] sprites, int nSprite, int instanceBase)
    {
        if (nSprite == 0)
            return;
        var push = BuildSpriteInstancingPush(in plan);
        var rented = ArrayPool<int>.Shared.Rent(nSprite);
        try
        {
            var upload = new Span<SpriteInstanceGpu>(
                (SpriteInstanceGpu*)_spriteInstanceBufferMapped! + instanceBase, nSprite);
            var valid = 0;
            for (var si = 0; si < nSprite; si++)
            {
                var idx = sortIdx[si];
                ref readonly var s = ref sprites[idx];
                if (s.Transparent)
                    continue;
                if (!TryBuildSpriteInstance(in s, in plan, out var inst))
                    continue;
                upload[valid] = inst;
                rented[valid++] = idx;
            }

            if (valid == 0)
                return;

            BeginGpuLabel(cmd, "Draw.SpritesOpaque.Batch");
            try
            {
            var instBind = stackalloc[] { _spriteInstanceBuffer };
            var instOff = stackalloc ulong[] { 0 };
            _vk!.CmdBindVertexBuffers(cmd, 1, 1, instBind, instOff);
            _vk.CmdPushConstants(cmd, _plSpriteTwoTexture, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpriteInstancingPush), &push);

            var first = 0;
            var batchCount = 0;
            var drawCount = 0;
            var setsG = stackalloc DescriptorSet[2];
            while (first < valid)
            {
                ref readonly var startS = ref sprites[rented[first]];
                var al = TryGetTextureSlot(startS.AlbedoTextureId);
                if (al is null)
                {
                    first++;
                    continue;
                }

                var nid = SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(
                    in startS,
                    _defaultNormalTextureId,
                    TryGetTextureSlot(startS.NormalTextureId) is not null);
                var nt = TryGetTextureSlot(nid);
                if (nt is null)
                {
                    first++;
                    continue;
                }

                var run = 1;
                while (first + run < valid)
                {
                    ref readonly var prevS = ref sprites[rented[first + run - 1]];
                    ref readonly var nextS = ref sprites[rented[first + run]];
                    var nPrev = SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(
                        in prevS,
                        _defaultNormalTextureId,
                        TryGetTextureSlot(prevS.NormalTextureId) is not null);
                    var nNext = SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(
                        in nextS,
                        _defaultNormalTextureId,
                        TryGetTextureSlot(nextS.NormalTextureId) is not null);
                    if (!SpriteBatchRuns.DeferredOpaqueRunCanExtend(in prevS, in nextS, nPrev, nNext))
                        break;
                    run++;
                }

                setsG[0] = al.DescriptorSet;
                setsG[1] = nt.DescriptorSet;
                _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteTwoTexture, 0, 2, setsG, 0, null);
                _vk.CmdDrawIndexed(cmd, 6, (uint)run, 0, 0, (uint)(instanceBase + first));
                batchCount++;
                drawCount++;
                first += run;
            }

            _lastFrameDeferredOpaqueSpriteInstances = valid;
            _lastFrameDeferredOpaqueSpriteBatchCount = batchCount;
            _lastFrameDeferredOpaqueSpriteDrawCalls = drawCount;
            }
            finally
            {
                EndGpuLabel(cmd);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    private void RecordDeferredSpritesTransparentInstanced(CommandBuffer cmd, in FramePlan plan, int[] sortIdx,
        SpriteDrawRequest[] sprites, int nSprite, int instanceBase)
    {
        if (nSprite == 0)
            return;
        var push = BuildSpriteInstancingPush(in plan);
        var rented = ArrayPool<int>.Shared.Rent(nSprite);
        try
        {
            var upload = new Span<SpriteInstanceGpu>(
                (SpriteInstanceGpu*)_spriteInstanceBufferMapped! + instanceBase, nSprite);
            var valid = 0;
            for (var si = 0; si < nSprite; si++)
            {
                var idx = sortIdx[si];
                ref readonly var s = ref sprites[idx];
                if (!s.Transparent)
                    continue;
                if (!TryBuildSpriteInstance(in s, in plan, out var inst))
                    continue;
                upload[valid] = inst;
                rented[valid++] = idx;
            }

            if (valid == 0)
                return;

            BeginGpuLabel(cmd, "Draw.SpritesTransparent.Batch");
            try
            {
            var instBind = stackalloc[] { _spriteInstanceBuffer };
            var instOff = stackalloc ulong[] { 0 };
            _vk!.CmdBindVertexBuffers(cmd, 1, 1, instBind, instOff);
            _vk.CmdPushConstants(cmd, _plSpriteTwoTexture, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpriteInstancingPush), &push);

            var first = 0;
            var batchCount = 0;
            var drawCount = 0;
            var setsW = stackalloc DescriptorSet[2];
            while (first < valid)
            {
                ref readonly var startS = ref sprites[rented[first]];
                var al = TryGetTextureSlot(startS.AlbedoTextureId);
                if (al is null)
                {
                    first++;
                    continue;
                }

                var run = 1;
                while (first + run < valid)
                {
                    ref readonly var prevS = ref sprites[rented[first + run - 1]];
                    ref readonly var nextS = ref sprites[rented[first + run]];
                    if (!SpriteBatchRuns.DeferredTransparentRunCanExtend(in prevS, in nextS))
                        break;
                    run++;
                }

                setsW[0] = al.DescriptorSet;
                setsW[1] = _dsHdrOpaqueForTransparent;
                _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteTwoTexture, 0, 2, setsW, 0, null);
                _vk.CmdDrawIndexed(cmd, 6, (uint)run, 0, 0, (uint)(instanceBase + first));
                batchCount++;
                drawCount++;
                first += run;
            }

            _lastFrameDeferredTransparentSpriteInstances = valid;
            _lastFrameDeferredTransparentSpriteBatchCount = batchCount;
            _lastFrameDeferredTransparentSpriteDrawCalls = drawCount;
            }
            finally
            {
                EndGpuLabel(cmd);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    private void RecordSwapchainOverlaySpritesInstanced(CommandBuffer cmd, in FramePlan plan, in Rect2D passScissor, int instanceBase)
    {
        var n = plan.ViewportUiOverlaySpriteCount;
        if (n <= 0)
            return;

        var push = BuildSpriteInstancingPush(in plan);
        var sprites = plan.ViewportUiOverlaySprites;
        var sortIdx = plan.ViewportUiOverlaySortIndices;
        var rented = ArrayPool<int>.Shared.Rent(n);
        try
        {
            var upload = new Span<SpriteInstanceGpu>(
                (SpriteInstanceGpu*)_spriteInstanceBufferMapped! + instanceBase, n);
            var valid = 0;
            for (var si = 0; si < n; si++)
            {
                var idx = sortIdx[si];
                ref readonly var s = ref sprites[idx];
                if (!TryBuildSpriteInstance(in s, in plan, out var inst))
                    continue;
                upload[valid] = inst;
                rented[valid++] = idx;
            }

            if (valid == 0)
                return;

            BeginGpuLabel(cmd, "Draw.SpritesOverlay.Batch");
            try
            {
            var instBind = stackalloc[] { _spriteInstanceBuffer };
            var instOff = stackalloc ulong[] { 0 };
            _vk!.CmdBindVertexBuffers(cmd, 1, 1, instBind, instOff);
            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeSwapchainUiOverlay);
            _vk.CmdPushConstants(cmd, _plSpriteTwoTexture, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpriteInstancingPush), &push);

            var first = 0;
            var batchCount = 0;
            var drawCount = 0;
            var setsOv = stackalloc DescriptorSet[2];
            while (first < valid)
            {
                ref readonly var startS = ref sprites[rented[first]];
                var al = TryGetTextureSlot(startS.AlbedoTextureId);
                if (al is null)
                {
                    first++;
                    continue;
                }

                var nidStart = SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(
                    in startS,
                    _defaultNormalTextureId,
                    TryGetTextureSlot(startS.NormalTextureId) is not null);
                var nt = TryGetTextureSlot(nidStart);
                if (nt is null)
                {
                    first++;
                    continue;
                }

                Rect2D sci = passScissor;
                if (startS.ViewportClipEnabled)
                {
                    sci = ViewportClipRectToSwapchainScissor(startS.ViewportClipRect, in plan, passScissor);
                    if (sci.Extent.Width == 0 || sci.Extent.Height == 0)
                    {
                        first++;
                        continue;
                    }
                }

                _vk!.CmdSetScissor(cmd, 0, 1, &sci);

                var run = 1;
                while (first + run < valid)
                {
                    ref readonly var prevS = ref sprites[rented[first + run - 1]];
                    ref readonly var nextS = ref sprites[rented[first + run]];
                    var nPrev = SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(
                        in prevS,
                        _defaultNormalTextureId,
                        TryGetTextureSlot(prevS.NormalTextureId) is not null);
                    var nNext = SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(
                        in nextS,
                        _defaultNormalTextureId,
                        TryGetTextureSlot(nextS.NormalTextureId) is not null);
                    if (!SpriteBatchRuns.OverlayRunCanExtend(in prevS, in nextS, nPrev, nNext))
                        break;
                    run++;
                }

                setsOv[0] = al.DescriptorSet;
                setsOv[1] = nt.DescriptorSet;
                _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteTwoTexture, 0, 2, setsOv, 0, null);
                _vk.CmdDrawIndexed(cmd, 6, (uint)run, 0, 0, (uint)(instanceBase + first));
                batchCount++;
                drawCount++;
                first += run;
            }

            _lastFrameOverlaySpriteInstances = valid;
            _lastFrameOverlaySpriteBatchCount = batchCount;
            _lastFrameOverlaySpriteDrawCalls = drawCount;
            }
            finally
            {
                EndGpuLabel(cmd);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }
}
