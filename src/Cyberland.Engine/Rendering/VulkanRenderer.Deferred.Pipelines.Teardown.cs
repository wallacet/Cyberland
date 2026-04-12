using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

// Purpose: Tear down pipelines, layouts, shaders, descriptor layouts, and render passes in an order compatible with Vulkan.
// PipelineFactory sequences deferred vs sprite/bloom/composite destruction; this file completes pool, DSLs, passes, and surfaces.

/// <summary>Resource teardown for deferred rendering pipelines (partial).</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void DestroyGraphicsResources()
    {
        if (_vk is null)
            return;

        _pipelineFactory ??= new PipelineFactory(this);
        _pipelineFactory.DestroyPipelineAndShaderObjects();

        if (_descriptorPool.Handle != default)
        {
            _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
            _descriptorPool = default;
        }

        DestroyDsl2(ref _dslComposite);
        DestroyDsl2(ref _dslBloomExtract);
        DestroyDsl2(ref _dslBloomDual);
        DestroyDsl2(ref _dslEmissiveScene);
        DestroyDsl2(ref _dslLighting);
        DestroyDsl2(ref _dslGbufferRead);
        DestroyDsl2(ref _dslPointSsbo);
        DestroyDsl2(ref _dslTransparentResolve);
        DestroyDsl2(ref _dslTexture);
        DestroyLightingBuffer();
        DestroyPointLightSsboResources();

        VulkanGraphicsPipelineHelpers.DestroySamplerIfValid(_vk, _device, ref _samplerLinear);

        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.DestroyGbufferAndWboitAndComposite();

        DestroyOffscreenFramebuffer(ref _fbEmissive, ref _viewEmissive, ref _imgEmissive, ref _memEmissive);
        DestroyOffscreenFramebuffer(ref _fbHdr, ref _viewHdr, ref _imgHdr, ref _memHdr);
        for (var i = 0; i < DeferredRenderingConstants.BloomDownsampleLevels; i++)
            DestroyOffscreenFramebuffer(ref _fbBloomDown[i], ref _viewBloomDown[i], ref _imgBloomDown[i], ref _memBloomDown[i]);
        DestroyOffscreenFramebuffer(ref _fbBloom0, ref _viewBloom0, ref _imgBloom0, ref _memBloom0);
        DestroyOffscreenFramebuffer(ref _fbBloom1, ref _viewBloom1, ref _imgBloom1, ref _memBloom1);

        VulkanGraphicsPipelineHelpers.DestroyRenderPassIfValid(_vk, _device, ref _rpOffscreenInitialUndefined);
        VulkanGraphicsPipelineHelpers.DestroyRenderPassIfValid(_vk, _device, ref _rpOffscreenInitialShaderRead);
        VulkanGraphicsPipelineHelpers.DestroyRenderPassIfValid(_vk, _device, ref _rpComposite);
        VulkanGraphicsPipelineHelpers.DestroyRenderPassIfValid(_vk, _device, ref _rpGbufferUndefined);
        VulkanGraphicsPipelineHelpers.DestroyRenderPassIfValid(_vk, _device, ref _rpGbufferShaderRead);
        VulkanGraphicsPipelineHelpers.DestroyRenderPassIfValid(_vk, _device, ref _rpWboitUndefined);
        VulkanGraphicsPipelineHelpers.DestroyRenderPassIfValid(_vk, _device, ref _rpWboitShaderRead);
    }

    private void DestroyShaderModule2(ref ShaderModule m)
    {
        if (m.Handle != default)
        {
            _vk!.DestroyShaderModule(_device, m, null);
            m = default;
        }
    }

    private void DestroyDsl2(ref DescriptorSetLayout dsl)
    {
        VulkanGraphicsPipelineHelpers.DestroyDescriptorSetLayoutIfValid(_vk!, _device, ref dsl);
    }

    private void DestroyDeferredShaderModules()
    {
        DestroyShaderModule2(ref _modFragGbuffer);
        DestroyShaderModule2(ref _modFragDeferredBase);
        DestroyShaderModule2(ref _modVertDeferredPoint);
        DestroyShaderModule2(ref _modFragDeferredPoint);
        DestroyShaderModule2(ref _modFragDeferredBleed);
        DestroyShaderModule2(ref _modFragTransparentWboit);
        DestroyShaderModule2(ref _modFragTransparentResolve);
    }

    private void DestroyPointLightSsboResources()
    {
        if (_pointLightSsboMemory.Handle != default && _pointLightSsboMapped != null)
        {
            _vk!.UnmapMemory(_device, _pointLightSsboMemory);
            _pointLightSsboMapped = null;
        }
        if (_pointLightSsbo.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _pointLightSsbo, null);
            _pointLightSsbo = default;
        }
        if (_pointLightSsboMemory.Handle != default)
        {
            _vk!.FreeMemory(_device, _pointLightSsboMemory, null);
            _pointLightSsboMemory = default;
        }
    }

    private void DestroyDeferredPipelinesAndLayouts()
    {
        VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_vk!, _device, ref _pipeSpriteGbuffer);
        VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_vk!, _device, ref _pipeDeferredBase);
        VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_vk!, _device, ref _pipeDeferredPoint);
        VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_vk!, _device, ref _pipeDeferredBleed);
        VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_vk!, _device, ref _pipeTransparentWboit);
        VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_vk!, _device, ref _pipeTransparentResolve);

        VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_vk!, _device, ref _plDeferredBase);
        VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_vk!, _device, ref _plDeferredPoint);
        VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_vk!, _device, ref _plDeferredBleed);
        VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_vk!, _device, ref _plTransparentResolve);
    }
}
