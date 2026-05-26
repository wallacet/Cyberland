using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Glslang.NET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;
// Purpose: Vulkan render pass objects for HDR offscreen, G-buffer, WBOIT, and swapchain composite.
// Two pass variants per attachment (Undefined vs ShaderReadOnly initial layout) avoid sampling uninitialized tiles.
// See CreateOffscreenRenderPasses summary for why layout must match image state.

/// <summary>Vulkan render pass creation for deferred HDR.</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void CreateOffscreenRenderPasses()
    {
        AttachmentDescription colorUndef = new()
        {
            Format = DeferredRenderingConstants.HdrFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ShaderReadOnlyOptimal
        };

        AttachmentDescription colorRead = colorUndef;
        colorRead.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

        AttachmentReference colorRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

        SubpassDescription sub = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorRef
        };

        SubpassDependency dep = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo rpci = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            SubpassCount = 1,
            PSubpasses = &sub,
            DependencyCount = 1,
            PDependencies = &dep
        };

        rpci.PAttachments = &colorUndef;
        if (_vk!.CreateRenderPass(_device, in rpci, null, out _rpOffscreenInitialUndefined) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass (offscreen, initial Undefined) failed.");

        rpci.PAttachments = &colorRead;
        if (_vk.CreateRenderPass(_device, in rpci, null, out _rpOffscreenInitialShaderRead) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass (offscreen, initial ShaderRead) failed.");
    }

    private RenderPass OffscreenRpFor(bool writtenSinceAlloc) =>
        writtenSinceAlloc ? _rpOffscreenInitialShaderRead : _rpOffscreenInitialUndefined;

    private void ResetOffscreenAttachmentWrittenFlags()
    {
        _offsWrittenEmissive = false;
        _offsWrittenHdr = false;
        _offsWrittenGbuffer = false;
        _offsWrittenWboit = false;
        _offsWrittenHdrComposite = false;
        _offsWrittenBloom0 = false;
        _offsWrittenBloom1 = false;
        Array.Fill(_offsWrittenBloomDown, false);
    }

    private void CreateCompositeRenderPass()
    {
        // Must use Clear (not DontCare): the composite draw is scissored to the letterboxed drawable rect; bar regions and
        // any pixels the fullscreen tri does not rewrite must still take the clear color. DontCare leaves attachment
        // contents undefined where fragments do not run — typical drivers retain prior swapchain pixels → HUD glyph “tails”
        // and digit fragments persist after shorter strings / layout changes.
        AttachmentDescription swapColor = new()
        {
            Format = _swapchainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference swapRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

        SubpassDescription sub = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &swapRef
        };

        SubpassDependency dep = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo rpci = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &swapColor,
            SubpassCount = 1,
            PSubpasses = &sub,
            DependencyCount = 1,
            PDependencies = &dep
        };

        if (_vk!.CreateRenderPass(_device, in rpci, null, out _rpComposite) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass (composite) failed.");
    }

    /// <summary>
    /// Second pass on the swapchain image: load the tonemapped composite, draw HUD sprites with straight-alpha.
    /// Load preserves the composite output from the composite render pass (full-surface clear + scissored fullscreen draw) —
    /// we blend UI on top of fresh tonemap output, not an accumulation buffer that holds last frame’s HUD alone.
    /// </summary>
    private void CreateSwapchainUiOverlayRenderPass()
    {
        AttachmentDescription swapColor = new()
        {
            Format = _swapchainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Load,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.PresentSrcKhr,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference swapRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

        SubpassDescription sub = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &swapRef
        };

        SubpassDependency dep = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.ColorAttachmentReadBit
        };

        RenderPassCreateInfo rpci = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &swapColor,
            SubpassCount = 1,
            PSubpasses = &sub,
            DependencyCount = 1,
            PDependencies = &dep
        };

        if (_vk!.CreateRenderPass(_device, in rpci, null, out _rpSwapchainUiOverlay) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass (swapchain UI overlay) failed.");
    }

    private void CreateGbufferAndWboitRenderPasses()
    {
        AttachmentDescription g0 = new()
        {
            Format = DeferredRenderingConstants.HdrFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ShaderReadOnlyOptimal
        };
        AttachmentDescription g1 = g0;
        AttachmentDescription gRead0 = g0;
        gRead0.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;
        AttachmentDescription gRead1 = gRead0;

        AttachmentReference rg0 = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };
        AttachmentReference rg1 = new() { Attachment = 1, Layout = ImageLayout.ColorAttachmentOptimal };
        var gRefs = stackalloc AttachmentReference[2];
        gRefs[0] = rg0;
        gRefs[1] = rg1;

        SubpassDescription subG = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 2,
            PColorAttachments = gRefs
        };

        var depsG = stackalloc SubpassDependency[2];
        depsG[0] = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };
        depsG[1] = new SubpassDependency
        {
            SrcSubpass = 0,
            DstSubpass = Vk.SubpassExternal,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.FragmentShaderBit,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.ShaderReadBit
        };

        var gAttU = stackalloc AttachmentDescription[2];
        gAttU[0] = g0;
        gAttU[1] = g1;

        RenderPassCreateInfo rpcG = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = gAttU,
            SubpassCount = 1,
            PSubpasses = &subG,
            DependencyCount = 2,
            PDependencies = depsG
        };

        if (_vk!.CreateRenderPass(_device, in rpcG, null, out _rpGbufferUndefined) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass gbuffer undef failed.");

        var gAttR = stackalloc AttachmentDescription[2];
        gAttR[0] = gRead0;
        gAttR[1] = gRead1;
        rpcG.PAttachments = gAttR;
        if (_vk.CreateRenderPass(_device, in rpcG, null, out _rpGbufferShaderRead) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass gbuffer read failed.");

        AttachmentDescription wAccum = new()
        {
            Format = DeferredRenderingConstants.HdrFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ShaderReadOnlyOptimal
        };
        AttachmentDescription wReveal = new()
        {
            Format = DeferredRenderingConstants.WboitRevealFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ShaderReadOnlyOptimal
        };
        AttachmentDescription wAccumR = wAccum;
        wAccumR.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;
        AttachmentDescription wRevealR = wReveal;
        wRevealR.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

        AttachmentReference rw0 = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };
        AttachmentReference rw1 = new() { Attachment = 1, Layout = ImageLayout.ColorAttachmentOptimal };
        var wRefs = stackalloc AttachmentReference[2];
        wRefs[0] = rw0;
        wRefs[1] = rw1;

        SubpassDescription subW = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 2,
            PColorAttachments = wRefs
        };

        var wAttU = stackalloc AttachmentDescription[2];
        wAttU[0] = wAccum;
        wAttU[1] = wReveal;

        SubpassDependency depW = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo rpcW = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = wAttU,
            SubpassCount = 1,
            PSubpasses = &subW,
            DependencyCount = 1,
            PDependencies = &depW
        };

        if (_vk.CreateRenderPass(_device, in rpcW, null, out _rpWboitUndefined) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass wboit undef failed.");

        var wAttR = stackalloc AttachmentDescription[2];
        wAttR[0] = wAccumR;
        wAttR[1] = wRevealR;
        rpcW.PAttachments = wAttR;
        if (_vk.CreateRenderPass(_device, in rpcW, null, out _rpWboitShaderRead) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass wboit read failed.");
    }

    private RenderPass GbufferRpFor(bool written) =>
        written ? _rpGbufferShaderRead : _rpGbufferUndefined;

    private RenderPass WboitRpFor(bool written) =>
        written ? _rpWboitShaderRead : _rpWboitUndefined;

    private RenderPass HdrCompositeRpFor(bool written) =>
        written ? _rpOffscreenInitialShaderRead : _rpOffscreenInitialUndefined;

    private void CreateShadowSdfRenderPasses()
    {
        // _rpShadowOccluderMask: R8 mask, Clear→Store, Undefined→ShaderReadOnly.
        {
            AttachmentDescription att = new()
            {
                Format = DeferredRenderingConstants.ShadowOccluderMaskFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.ShaderReadOnlyOptimal
            };

            AttachmentReference colorRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

            SubpassDescription sub = new()
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorRef
            };

            SubpassDependency dep = new()
            {
                SrcSubpass = 0,
                DstSubpass = Vk.SubpassExternal,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = PipelineStageFlags.FragmentShaderBit,
                SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };

            RenderPassCreateInfo rpci = new()
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &att,
                SubpassCount = 1,
                PSubpasses = &sub,
                DependencyCount = 1,
                PDependencies = &dep
            };

            if (_vk!.CreateRenderPass(_device, in rpci, null, out _rpShadowOccluderMask) != Result.Success)
                throw new GraphicsInitializationException("vkCreateRenderPass (shadow occluder mask) failed.");
        }

        // _rpShadowJfaSeed: R16G16_SNORM seed, Clear→Store, Undefined→ShaderReadOnly.
        {
            AttachmentDescription att = new()
            {
                Format = DeferredRenderingConstants.ShadowJfaSeedFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.ShaderReadOnlyOptimal
            };

            AttachmentReference colorRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

            SubpassDescription sub = new()
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorRef
            };

            SubpassDependency dep = new()
            {
                SrcSubpass = 0,
                DstSubpass = Vk.SubpassExternal,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = PipelineStageFlags.FragmentShaderBit,
                SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };

            RenderPassCreateInfo rpci = new()
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &att,
                SubpassCount = 1,
                PSubpasses = &sub,
                DependencyCount = 1,
                PDependencies = &dep
            };

            if (_vk.CreateRenderPass(_device, in rpci, null, out _rpShadowJfaSeed) != Result.Success)
                throw new GraphicsInitializationException("vkCreateRenderPass (shadow JFA seed) failed.");
        }

        // _rpShadowSdfFinal: R16F SDF, Load→Store, ShaderReadOnly→ShaderReadOnly.
        {
            AttachmentDescription att = new()
            {
                Format = DeferredRenderingConstants.ShadowSdfFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Load,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.ShaderReadOnlyOptimal,
                FinalLayout = ImageLayout.ShaderReadOnlyOptimal
            };

            AttachmentReference colorRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

            SubpassDescription sub = new()
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorRef
            };

            SubpassDependency dep = new()
            {
                SrcSubpass = 0,
                DstSubpass = Vk.SubpassExternal,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = PipelineStageFlags.FragmentShaderBit,
                SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };

            RenderPassCreateInfo rpci = new()
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &att,
                SubpassCount = 1,
                PSubpasses = &sub,
                DependencyCount = 1,
                PDependencies = &dep
            };

            if (_vk.CreateRenderPass(_device, in rpci, null, out _rpShadowSdfFinal) != Result.Success)
                throw new GraphicsInitializationException("vkCreateRenderPass (shadow SDF final) failed.");
        }
    }
}
