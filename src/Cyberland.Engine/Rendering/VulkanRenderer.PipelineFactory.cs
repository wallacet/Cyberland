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
            _r.CompileSpriteAndCompositeShaders();
            _r.CreateSpritePipelineLayoutsAndPipelines();
            _r.CreateBloomPipelineLayoutsAndPipelines();
            _r.CreateCompositePipeline();
        }

        public void DestroyPipelineAndShaderObjects()
        {
            _r.DestroyDeferredPipelinesAndLayouts();

            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeComposite);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeEmissive);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomGaussian);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomCopy);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomUpsample);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomDownsample);
            VulkanGraphicsPipelineHelpers.DestroyPipelineIfValid(_r._vk!, _r._device, ref _r._pipeBloomExtract);

            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plComposite);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plSpriteEmissive);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomGaussian);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomCopy);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomUpsample);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomDownsample);
            VulkanGraphicsPipelineHelpers.DestroyPipelineLayoutIfValid(_r._vk!, _r._device, ref _r._plBloomExtract);

            _r.DestroyDeferredShaderModules();

            _r.DestroyShaderModule2(ref _r._modFragBloomUpsample);
            _r.DestroyShaderModule2(ref _r._modFragBloomCopy);
            _r.DestroyShaderModule2(ref _r._modFragBloomGaussian);
            _r.DestroyShaderModule2(ref _r._modFragBloomDownsample);
            _r.DestroyShaderModule2(ref _r._modFragBloomExtract);
            _r.DestroyShaderModule2(ref _r._modFragComposite);
            _r.DestroyShaderModule2(ref _r._modVertComposite);
            _r.DestroyShaderModule2(ref _r._modFragEmissive);
            _r.DestroyShaderModule2(ref _r._modVertSprite);
        }
    }
}
