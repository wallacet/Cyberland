using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Glslang.NET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;
// Purpose: Per-frame command buffer recording: emissive, G-buffer, deferred lighting, transparency, bloom, composite.
// Runs on the window/render thread only; consumes FramePlan built on the main thread.

/// <summary>Full-frame GPU recording for one swapchain image.</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void RecordFullFrame(CommandBuffer cmd, Framebuffer swapFb)
    {
        _renderFrameRecorder ??= new RenderFrameRecorder(this);
        _renderFrameRecorder.Record(cmd, swapFb);
    }

    private void RecordFullFrameCore(CommandBuffer cmd, Framebuffer swapFb)
    {
        _framePlanBuilder ??= new FramePlanBuilder(this);
        _renderBackendExecutor ??= new RenderBackendExecutor(this);
        var framePlan = _framePlanBuilder.Build();
        _renderBackendExecutor.Record(cmd, swapFb, in framePlan);
    }

    private void ExecuteFramePlanCore(CommandBuffer cmd, Framebuffer swapFb, in FramePlan framePlan)
    {
        if (_vk!.ResetCommandBuffer(cmd, 0) != Result.Success)
            throw new InvalidOperationException("vkResetCommandBuffer failed.");

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };

        if (_vk.BeginCommandBuffer(cmd, in beginInfo) != Result.Success)
            throw new InvalidOperationException("vkBeginCommandBuffer failed.");

        var screen = framePlan.Screen;
        var sortIdx = framePlan.SortIndices;
        var sprites = framePlan.Sprites;
        var nSprite = framePlan.SpriteCount;
        var post = framePlan.ResolvedPost;
        UpdateLightingFrameData(in framePlan);
        UploadPointLightSsboData(in framePlan);

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

        ClearValue cEm = new()
        {
            Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
        };

        RenderPassBeginInfo rpEm = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = OffscreenRpFor(_offsWrittenEmissive),
            Framebuffer = _fbEmissive,
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 1,
            PClearValues = &cEm
        };

        _vk.CmdBeginRenderPass(cmd, &rpEm, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeEmissive);

        var vb = stackalloc[] { _vertexBuffer };
        var off = stackalloc ulong[] { 0 };
        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        for (var si = 0; si < nSprite; si++)
        {
            var idx = sortIdx[si];
            ref readonly var s = ref sprites[idx];
            DrawSprite(cmd, in s, in framePlan, 0);
        }

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenEmissive = true;

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
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 2,
            PClearValues = cGbuf
        };

        _vk.CmdBeginRenderPass(cmd, &rpGb, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeSpriteGbuffer);
        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        for (var si = 0; si < nSprite; si++)
        {
            var idx = sortIdx[si];
            ref readonly var s = ref sprites[idx];
            if (!s.Transparent)
                DrawSprite(cmd, in s, in framePlan, 1);
        }

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenGbuffer = true;

        // Camera-driven HDR clear: the active camera's background color doubles as the letterbox bar color
        // (pixels outside the scissor never get written by any pass, so the clear we pick here is what shows
        // through in the bars after the composite copies HDR → swapchain).
        var bg = framePlan.Camera.BackgroundColor;
        ClearValue cHdr = new()
        {
            Color = new ClearColorValue { Float32_0 = bg.X, Float32_1 = bg.Y, Float32_2 = bg.Z, Float32_3 = bg.W }
        };

        RenderPassBeginInfo rpH = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = OffscreenRpFor(_offsWrittenHdr),
            Framebuffer = _fbHdr,
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 1,
            PClearValues = &cHdr
        };

        _vk.CmdBeginRenderPass(cmd, &rpH, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);

        // Fullscreen deferred fragments only need swapchain size for UVs; point-light quads also need the
        // letterboxed VkViewport rect so clip-space matches sprite rendering.
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

        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredBase);
        var setsBase = stackalloc DescriptorSet[2];
        setsBase[0] = _dsGbufferRead;
        setsBase[1] = _dsLighting;
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredBase, 0, 2, setsBase, 0, null);
        _vk.CmdPushConstants(cmd, _plDeferredBase, ShaderStageFlags.FragmentBit, 0, (uint)(sizeof(float) * 4), screenPushHdr);
        _vk.CmdDraw(cmd, 3, 1, 0, 0);

        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredPoint);
        var setsPt = stackalloc DescriptorSet[2];
        setsPt[0] = _dsGbufferRead;
        setsPt[1] = _dsPointSsbo;
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredPoint, 0, 2, setsPt, 0, null);
        _vk.CmdPushConstants(cmd, _plDeferredPoint, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
            (uint)(sizeof(float) * 8), pointPushHdr);
        var nPt = Math.Min(framePlan.PointLightCount, DeferredRenderingConstants.MaxPointLights);
        if (nPt > 0)
        {
            _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
            _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);
            _vk.CmdDrawIndexed(cmd, 6, (uint)nPt, 0, 0, 0);
        }

        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredBleed);
        var setsBl = stackalloc DescriptorSet[2];
        setsBl[0] = _dsGbufferRead;
        setsBl[1] = _dsEmissiveScene;
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredBleed, 0, 2, setsBl, 0, null);
        _vk.CmdPushConstants(cmd, _plDeferredBleed, ShaderStageFlags.FragmentBit, 0, (uint)(sizeof(float) * 4), screenPushHdr);
        _vk.CmdDraw(cmd, 3, 1, 0, 0);

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenHdr = true;

        var hasTransparentSprites = framePlan.TransparentSpriteCount > 0;
        if (hasTransparentSprites)
        {
            ClearValue cWAccum = cEm;
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
                RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
                ClearValueCount = 2,
                PClearValues = cWboit
            };

            _vk.CmdBeginRenderPass(cmd, &rpW, SubpassContents.Inline);
            _vk.CmdSetViewport(cmd, 0, 1, &vp);
            _vk.CmdSetScissor(cmd, 0, 1, &sci);
            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeTransparentWboit);
            _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
            _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

            for (var si = 0; si < nSprite; si++)
            {
                var idx = sortIdx[si];
                ref readonly var s = ref sprites[idx];
                if (!s.Transparent)
                    continue;
                DrawSprite(cmd, in s, in framePlan, 2);
            }

            _vk.CmdEndRenderPass(cmd);
            _offsWrittenWboit = true;

            ClearValue cRes = cEm;
            RenderPassBeginInfo rpRes = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = OffscreenRpFor(_offsWrittenHdrComposite),
                Framebuffer = _fbHdrComposite,
                RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
                ClearValueCount = 1,
                PClearValues = &cRes
            };

            _vk.CmdBeginRenderPass(cmd, &rpRes, SubpassContents.Inline);
            _vk.CmdSetViewport(cmd, 0, 1, &vp);
            _vk.CmdSetScissor(cmd, 0, 1, &sci);
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

        UpdateSceneHdrSourcesForPostProcess(hasTransparentSprites ? _viewHdrComposite : _viewHdr);

        var bloomGain = post.BloomEnabled ? post.BloomGain : 0f;
        var bloomOn = bloomGain > 0f;
        var bloomRadius = post.BloomRadius;

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

        _postProcessGraph ??= new PostProcessGraph(this);
        var ppContext = new PostEffectContext(cmd, swapFb, framePlan, vp, sci, vpHalf, sciHalf);
        _postProcessGraph.Record(in ppContext, bloomOn, bloomGain, bloomRadius, post);

        if (_vk.EndCommandBuffer(cmd) != Result.Success)
            throw new InvalidOperationException("vkEndCommandBuffer failed.");
    }

    private void DrawSprite(CommandBuffer cmd, in SpriteDrawRequest s, in FramePlan plan, int mode)
    {
        var al = TryGetTextureSlot(s.AlbedoTextureId);
        if (al is null)
            return;

        // Project the sprite's authored center down to swapchain pixels (+Y down). World sprites go through
        // the camera transform first, then letterbox. Viewport sprites skip the camera transform so HUD stays
        // locked to the virtual viewport regardless of camera pose.
        var viewportSize = new Vector2D<float>(plan.Camera.ViewportSizeWorld.X, plan.Camera.ViewportSizeWorld.Y);
        Vector2D<float> vpPixel;
        float rotScreen;
        if (s.Space == Scene.CoordinateSpace.ViewportSpace)
        {
            vpPixel = s.CenterWorld;
            rotScreen = s.RotationRadians;
        }
        else
        {
            vpPixel = CameraProjection.WorldToViewportPixel(
                s.CenterWorld,
                plan.Camera.PositionWorld,
                plan.Camera.RotationRadians,
                viewportSize);
            rotScreen = s.RotationRadians - plan.Camera.RotationRadians;
        }

        var px = CameraProjection.ViewportPixelToSwapchainPixel(vpPixel, in plan.Physical);
        var halfX = s.HalfExtentsWorld.X * plan.Physical.Scale;
        var halfY = s.HalfExtentsWorld.Y * plan.Physical.Scale;
        var uv = s.UvRect;
        if (uv.X == 0f && uv.Y == 0f && uv.Z == 0f && uv.W == 0f)
            uv = new Vector4D<float>(0f, 0f, 1f, 1f);

        var screen = plan.Screen;
        var phys = plan.Physical;
        var push = new SpritePushData
        {
            CenterHalfPx = new Vector4D<float>(px.X, px.Y, halfX, halfY),
            UvRect = uv,
            ColorAlpha = new Vector4D<float>(s.ColorMultiply.X * s.Alpha, s.ColorMultiply.Y * s.Alpha, s.ColorMultiply.Z * s.Alpha, s.ColorMultiply.W * s.Alpha),
            EmissiveRgbIntensity = new Vector4D<float>(s.EmissiveTint.X, s.EmissiveTint.Y, s.EmissiveTint.Z, s.EmissiveIntensity),
            ViewportPhysical = new Vector4D<float>(phys.OffsetPixels.X, phys.OffsetPixels.Y, phys.SizePixels.X, phys.SizePixels.Y),
            ScreenRot = new Vector4D<float>(screen.X, screen.Y, rotScreen, 0f),
            Mode = mode,
            UseEmissiveMap = 0
        };

        if (mode == 0)
        {
            var useEm = TryGetTextureSlot(s.EmissiveTextureId) is not null ? 1 : 0;
            var emTexId = useEm != 0 ? s.EmissiveTextureId : _blackTextureId;
            var emSlot = TryGetTextureSlot(emTexId);
            if (emSlot is null)
                return;
            push.UseEmissiveMap = useEm;

            var setsE = stackalloc DescriptorSet[2];
            setsE[0] = al.DescriptorSet;
            setsE[1] = emSlot.DescriptorSet;
            _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteEmissive, 0, 2, setsE, 0, null);
            _vk.CmdPushConstants(cmd, _plSpriteEmissive, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpritePushData), &push);
        }
        else if (mode == 1)
        {
            var nid = TryGetTextureSlot(s.NormalTextureId) is not null
                ? s.NormalTextureId
                : _defaultNormalTextureId;
            var nt = TryGetTextureSlot(nid);
            if (nt is null)
                return;

            var setsG = stackalloc DescriptorSet[2];
            setsG[0] = al.DescriptorSet;
            setsG[1] = nt.DescriptorSet;
            _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteEmissive, 0, 2, setsG, 0, null);
            push.UseEmissiveMap = 0;
            _vk.CmdPushConstants(cmd, _plSpriteEmissive, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpritePushData), &push);
        }
        else
        {
            var setsW = stackalloc DescriptorSet[2];
            setsW[0] = al.DescriptorSet;
            setsW[1] = _dsHdrOpaqueForTransparent;
            push.UseEmissiveMap = 0;
            _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteEmissive, 0, 2, setsW, 0, null);
            _vk.CmdPushConstants(cmd, _plSpriteEmissive, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpritePushData), &push);
        }

        _vk!.CmdDrawIndexed(cmd, 6, 1, 0, 0, 0);
    }
}
