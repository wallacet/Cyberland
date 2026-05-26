using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

public sealed unsafe partial class VulkanRenderer
{
    /// <summary>
    /// Coordinates shader/pipeline construction and teardown ordering.
    /// </summary>
    private sealed class PipelineFactory
    {
        private readonly VulkanRenderer _r;

        public PipelineFactory(VulkanRenderer renderer) => _r = renderer;

        public void CreateAllPipelines()
        {
            _r.RunInitializationStage("vk.shaders.compile_sprite_composite_and_deferred", _r.CompileSpriteAndCompositeShaders);
            _r.RunInitializationStage("vk.pipelines.sprite_and_deferred", _r.CreateSpritePipelineLayoutsAndPipelines);
            _r.RunInitializationStage("vk.pipelines.bloom", _r.CreateBloomPipelineLayoutsAndPipelines);
            _r.RunInitializationStage("vk.pipelines.composite", _r.CreateCompositePipeline);
            _r.RunInitializationStage("vk.pipelines.shadow_jfa", _r.CreateShadowJfaPipelines);
            _r.RunInitializationStage("vk.pipelines.tiled_deferred_lighting", _r.CreateTiledLightingPipeline);
        }

        public void DestroyPipelineAndShaderObjects()
        {
            _r.DestroyDeferredPipelinesAndLayouts();

            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeTiledDeferredLighting);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plTiledDeferredLighting);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeComposite);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeEmissive);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomGaussian);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomCopy);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomUpsample);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomDownsample);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomExtract);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeTextMsdf);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeShadowOccluder);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeJfaInit);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeJfaStep);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeJfaToSdf);

            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plComposite);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plSpriteTwoTexture);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomGaussian);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomCopy);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomUpsample);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomDownsample);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomExtract);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plTextMsdf);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plShadowOccluder);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plJfaInit);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plJfaStep);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plJfaToSdf);

            _r.DestroyDeferredShaderModules();
            _r.DestroyShaderModule2(ref _r._modFragTiledDeferredLighting);

            _r.DestroyShaderModule2(ref _r._modFragBloomUpsample);
            _r.DestroyShaderModule2(ref _r._modFragBloomCopy);
            _r.DestroyShaderModule2(ref _r._modFragBloomGaussian);
            _r.DestroyShaderModule2(ref _r._modFragBloomDownsample);
            _r.DestroyShaderModule2(ref _r._modFragBloomExtract);
            _r.DestroyShaderModule2(ref _r._modFragComposite);
            _r.DestroyShaderModule2(ref _r._modFragEmissive);
            _r.DestroyShaderModule2(ref _r._modVertSprite);
            _r.DestroyShaderModule2(ref _r._modFragTextMsdf);
            _r.DestroyShaderModule2(ref _r._modVertTextMsdf);
            _r.DestroyShaderModule2(ref _r._modVertShadowOccluder);
            _r.DestroyShaderModule2(ref _r._modFragShadowOccluder);
            _r.DestroyShaderModule2(ref _r._modVertFullscreenTriangle);
            _r.DestroyShaderModule2(ref _r._modFragJfaInit);
            _r.DestroyShaderModule2(ref _r._modFragJfaStep);
            _r.DestroyShaderModule2(ref _r._modFragJfaToSdf);

            _r.DestroyShadowSdfTargets();
            _r.DestroyShadowSdfParamsUbo();
            _r.DestroyShadowSdfDescriptorLayout();
        }
    }
}
