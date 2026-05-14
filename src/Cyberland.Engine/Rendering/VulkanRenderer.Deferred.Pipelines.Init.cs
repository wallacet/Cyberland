using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

// Purpose: Swapchain/offscreen orchestration for pipeline setup — sampler, framebuffers, memory helper, OffscreenTargets delegation.
// Order: CreateGraphicsPipelineAndSurfaces (main VulkanRenderer.cs hub) calls these before descriptor/pipeline partials.

/// <summary>Initialization steps for deferred rendering surfaces (partial).</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void CreateGraphicsPipelineAndSurfaces()
    {
        RunInitializationStage("vk.linear_sampler", CreateLinearSampler);
        RunInitializationStage("vk.render_passes.offscreen", CreateOffscreenRenderPasses);
        RunInitializationStage("vk.render_passes.gbuffer_wboit", CreateGbufferAndWboitRenderPasses);
        RunInitializationStage("vk.render_passes.composite", CreateCompositeRenderPass);
        RunInitializationStage("vk.render_passes.swapchain_ui_overlay", CreateSwapchainUiOverlayRenderPass);
        RunInitializationStage("vk.framebuffers.offscreen", CreateOffscreenImagesAndFramebuffers);
        RunInitializationStage("vk.framebuffers.swapchain", CreateSwapchainFramebuffers);
        RunInitializationStage("vk.descriptor_layouts_and_pool", CreateDescriptorLayoutsAndPool);
        _pipelineFactory ??= new PipelineFactory(this);
        RunInitializationStage("vk.pipelines.create_all", _pipelineFactory.CreateAllPipelines);
        RunInitializationStage("vk.descriptor_sets.composite", AllocateCompositeDescriptorSet);
        RunInitializationStage("vk.descriptor_sets.bloom", AllocateBloomDescriptorSets);
        RunInitializationStage("vk.descriptor_sets.emissive_scene", AllocateEmissiveSceneDescriptorSet);
        RunInitializationStage("vk.descriptor_sets.lighting", AllocateLightingDescriptorSet);
        RunInitializationStage("vk.point_light_ssbo.ensure", EnsurePointLightSsbo);
        RunInitializationStage("vk.descriptor_sets.deferred", AllocateDeferredDescriptorSets);
        RunInitializationStage("vk.debug_names.after_bootstrap", ApplyDeferredGpuDebugNamesAfterBootstrap);
    }

    private void RecreateSwapchainDependent()
    {
        RecreateOffscreenTargets();
        CreateSwapchainFramebuffers();
    }

    private void CreateSwapchainFramebuffers()
    {
        _swapchainFramebuffers = new Framebuffer[_swapchainImageViews!.Length];
        _swapchainUiOverlayFramebuffers = new Framebuffer[_swapchainImageViews.Length];

        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            var attachment = _swapchainImageViews[i];

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _rpComposite,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1
            };

            if (_vk!.CreateFramebuffer(_device, in framebufferInfo, null, out _swapchainFramebuffers[i]) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer (swapchain) failed.");

            framebufferInfo.RenderPass = _rpSwapchainUiOverlay;
            if (_vk.CreateFramebuffer(_device, in framebufferInfo, null, out _swapchainUiOverlayFramebuffers[i]) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer (swapchain UI overlay) failed.");

            SetGpuObjectName(ObjectType.Framebuffer, VkHandle(_swapchainFramebuffers[i]), $"fb.SwapchainComposite[{i}]");
            SetGpuObjectName(ObjectType.Framebuffer, VkHandle(_swapchainUiOverlayFramebuffers[i]), $"fb.SwapchainUiOverlay[{i}]");
        }
    }

    private void CreateLinearSampler()
    {
        SamplerCreateInfo sci = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge
        };

        if (_vk!.CreateSampler(_device, in sci, null, out _samplerLinear) != Result.Success)
            throw new GraphicsInitializationException("vkCreateSampler failed.");
    }

    /// <summary>
    /// Two compatible passes: Vulkan requires <see cref="AttachmentDescription.InitialLayout"/> to match the image's
    /// actual layout at <c>CmdBeginRenderPass</c>. New images start Undefined; after EndRenderPass they are
    /// ShaderReadOnlyOptimal — the next Begin on that image must declare that layout, or sampling sees garbage (splotches, flicker).
    /// </summary>
    private uint FindMemoryTypeDeviceLocal(uint typeFilter)
    {
        _vk!.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);
        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << (int)i)) != 0 &&
                (memProperties.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) ==
                MemoryPropertyFlags.DeviceLocalBit)
                return i;
        }

        throw new GraphicsInitializationException("No device-local memory type.");
    }

    private void CreateDeviceLocalImage(uint w, uint h, Format format, ImageUsageFlags usage, out Image img, out DeviceMemory mem, out ImageView view)
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.CreateDeviceLocalImage(w, h, format, usage, out img, out mem, out view);
    }

    private void CreateOffscreenImagesAndFramebuffers()
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.CreateOffscreenImagesAndFramebuffers();
    }

    private void CreateBloomHalfResTargets()
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.CreateBloomHalfResTargets();
    }

    private void RecreateOffscreenTargets()
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.RecreateOffscreenTargets();
    }

    private void DestroyOffscreenFramebuffer(ref Framebuffer fb, ref ImageView view, ref Image img, ref DeviceMemory mem)
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.DestroyOffscreenFramebuffer(ref fb, ref view, ref img, ref mem);
    }
}
