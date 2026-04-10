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

            if (_r._pipeComposite.Handle != default)
            {
                _r._vk!.DestroyPipeline(_r._device, _r._pipeComposite, null);
                _r._pipeComposite = default;
            }

            if (_r._pipeEmissive.Handle != default)
            {
                _r._vk!.DestroyPipeline(_r._device, _r._pipeEmissive, null);
                _r._pipeEmissive = default;
            }

            if (_r._pipeBloomGaussian.Handle != default)
            {
                _r._vk!.DestroyPipeline(_r._device, _r._pipeBloomGaussian, null);
                _r._pipeBloomGaussian = default;
            }

            if (_r._pipeBloomCopy.Handle != default)
            {
                _r._vk!.DestroyPipeline(_r._device, _r._pipeBloomCopy, null);
                _r._pipeBloomCopy = default;
            }

            if (_r._pipeBloomUpsample.Handle != default)
            {
                _r._vk!.DestroyPipeline(_r._device, _r._pipeBloomUpsample, null);
                _r._pipeBloomUpsample = default;
            }

            if (_r._pipeBloomDownsample.Handle != default)
            {
                _r._vk!.DestroyPipeline(_r._device, _r._pipeBloomDownsample, null);
                _r._pipeBloomDownsample = default;
            }

            if (_r._pipeBloomExtract.Handle != default)
            {
                _r._vk!.DestroyPipeline(_r._device, _r._pipeBloomExtract, null);
                _r._pipeBloomExtract = default;
            }

            if (_r._plComposite.Handle != default)
            {
                _r._vk!.DestroyPipelineLayout(_r._device, _r._plComposite, null);
                _r._plComposite = default;
            }

            if (_r._plSpriteEmissive.Handle != default)
            {
                _r._vk!.DestroyPipelineLayout(_r._device, _r._plSpriteEmissive, null);
                _r._plSpriteEmissive = default;
            }

            if (_r._plBloomGaussian.Handle != default)
            {
                _r._vk!.DestroyPipelineLayout(_r._device, _r._plBloomGaussian, null);
                _r._plBloomGaussian = default;
            }

            if (_r._plBloomCopy.Handle != default)
            {
                _r._vk!.DestroyPipelineLayout(_r._device, _r._plBloomCopy, null);
                _r._plBloomCopy = default;
            }

            if (_r._plBloomUpsample.Handle != default)
            {
                _r._vk!.DestroyPipelineLayout(_r._device, _r._plBloomUpsample, null);
                _r._plBloomUpsample = default;
            }

            if (_r._plBloomDownsample.Handle != default)
            {
                _r._vk!.DestroyPipelineLayout(_r._device, _r._plBloomDownsample, null);
                _r._plBloomDownsample = default;
            }

            if (_r._plBloomExtract.Handle != default)
            {
                _r._vk!.DestroyPipelineLayout(_r._device, _r._plBloomExtract, null);
                _r._plBloomExtract = default;
            }

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
