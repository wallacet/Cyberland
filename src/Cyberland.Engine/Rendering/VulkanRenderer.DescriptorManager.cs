using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

public sealed unsafe partial class VulkanRenderer
{
    /// <summary>
    /// Descriptor update helper for sprite/HDR render passes.
    /// </summary>
    private sealed class DescriptorManager
    {
        private readonly VulkanRenderer _r;

        public DescriptorManager(VulkanRenderer renderer) => _r = renderer;

        public void AllocateCompositeDescriptorSet()
        {
            fixed (DescriptorSetLayout* dslC = &_r._dslComposite)
            {
                for (var fi = 0; fi < MaxFramesInFlight; fi++)
                {
                    DescriptorSetAllocateInfo ai = new()
                    {
                        SType = StructureType.DescriptorSetAllocateInfo,
                        DescriptorPool = _r._descriptorPool,
                        DescriptorSetCount = 1,
                        PSetLayouts = dslC
                    };

                    if (_r._vk!.AllocateDescriptorSets(_r._device, in ai, out _r._dsCompositeSlots[fi]) != Result.Success)
                        throw new GraphicsInitializationException("alloc composite ds");
                }
            }

            _r.UpdateCompositeDescriptorSet();
        }

        public void AllocateBloomDescriptorSets()
        {
            fixed (DescriptorSetLayout* dslE = &_r._dslBloomExtract)
            {
                DescriptorSetAllocateInfo ai = new()
                {
                    SType = StructureType.DescriptorSetAllocateInfo,
                    DescriptorPool = _r._descriptorPool,
                    DescriptorSetCount = 1,
                    PSetLayouts = dslE
                };

                if (_r._vk!.AllocateDescriptorSets(_r._device, in ai, out _r._dsBloomExtract) != Result.Success)
                    throw new GraphicsInitializationException("alloc bloom extract ds");
            }

            fixed (DescriptorSetLayout* dslDu = &_r._dslBloomDual)
            {
                DescriptorSetAllocateInfo ai = new()
                {
                    SType = StructureType.DescriptorSetAllocateInfo,
                    DescriptorPool = _r._descriptorPool,
                    DescriptorSetCount = 1,
                    PSetLayouts = dslDu
                };

                if (_r._vk!.AllocateDescriptorSets(_r._device, in ai, out _r._dsBloomUpsample) != Result.Success)
                    throw new GraphicsInitializationException("alloc bloom upsample ds");
            }

            fixed (DescriptorSetLayout* dslT = &_r._dslTexture)
            {
                DescriptorSetAllocateInfo ai = new()
                {
                    SType = StructureType.DescriptorSetAllocateInfo,
                    DescriptorPool = _r._descriptorPool,
                    DescriptorSetCount = 1,
                    PSetLayouts = dslT
                };

                if (_r._vk.AllocateDescriptorSets(_r._device, in ai, out _r._dsBloomGaussianSrcBloom0) != Result.Success)
                    throw new GraphicsInitializationException("alloc bloom gaussian src0 ds");
                if (_r._vk.AllocateDescriptorSets(_r._device, in ai, out _r._dsBloomGaussianSrcBloom1) != Result.Success)
                    throw new GraphicsInitializationException("alloc bloom gaussian src1 ds");
                for (var i = 0; i < _r._dsBloomDownSrc.Length; i++)
                {
                    if (_r._vk.AllocateDescriptorSets(_r._device, in ai, out _r._dsBloomDownSrc[i]) != Result.Success)
                        throw new GraphicsInitializationException($"alloc bloom down src[{i}] ds");
                }
            }

            _r.UpdateBloomExtractDescriptorSet();
            _r.UpdateBloomGaussianDescriptorSets();
        }

        public void AllocateEmissiveSceneDescriptorSet()
        {
            fixed (DescriptorSetLayout* dslE = &_r._dslEmissiveScene)
            {
                DescriptorSetAllocateInfo ai = new()
                {
                    SType = StructureType.DescriptorSetAllocateInfo,
                    DescriptorPool = _r._descriptorPool,
                    DescriptorSetCount = 1,
                    PSetLayouts = dslE
                };

                if (_r._vk!.AllocateDescriptorSets(_r._device, in ai, out _r._dsEmissiveScene) != Result.Success)
                    throw new GraphicsInitializationException("alloc emissive scene ds");
            }

            UpdateEmissiveSceneDescriptorSet();
        }

        public void AllocateLightingDescriptorSet()
        {
            _r.CreateLightingBuffer();
            fixed (DescriptorSetLayout* dslL = &_r._dslLighting)
            {
                DescriptorSetAllocateInfo ai = new()
                {
                    SType = StructureType.DescriptorSetAllocateInfo,
                    DescriptorPool = _r._descriptorPool,
                    DescriptorSetCount = 1,
                    PSetLayouts = dslL
                };

                if (_r._vk!.AllocateDescriptorSets(_r._device, in ai, out _r._dsLighting) != Result.Success)
                    throw new GraphicsInitializationException("alloc lighting ds");
            }
            UpdateLightingDescriptorSet();
        }

        public void UpdateCompositeBloomSource(ImageView bloomFinalView)
        {
            DescriptorImageInfo bl = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = bloomFinalView,
                Sampler = _r._samplerLinear
            };

            WriteDescriptorSet w = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _r._dsCompositeSlots[_r._currentFrame],
                DstBinding = 2,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &bl
            };

            _r._vk!.UpdateDescriptorSets(_r._device, 1, &w, 0, null);
        }

        public void UpdateBloomGaussianDescriptorSets()
        {
            if (_r._vk is null || _r._viewBloom0.Handle == default)
                return;

            DescriptorImageInfo b0 = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = _r._viewBloom0,
                Sampler = _r._samplerLinear
            };

            DescriptorImageInfo b1 = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = _r._viewBloom1,
                Sampler = _r._samplerLinear
            };

            WriteDescriptorSet w0 = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _r._dsBloomGaussianSrcBloom0,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &b0
            };

            WriteDescriptorSet w1 = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _r._dsBloomGaussianSrcBloom1,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &b1
            };

            _r._vk!.UpdateDescriptorSets(_r._device, 1, &w0, 0, null);
            _r._vk.UpdateDescriptorSets(_r._device, 1, &w1, 0, null);

            for (var i = 0; i < _r._dsBloomDownSrc.Length; i++)
            {
                DescriptorImageInfo di = new()
                {
                    ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                    ImageView = i == 0 ? _r._viewBloom0 : _r._viewBloomDown[i - 1],
                    Sampler = _r._samplerLinear
                };

                WriteDescriptorSet wd = new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _r._dsBloomDownSrc[i],
                    DstBinding = 0,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    PImageInfo = &di
                };

                _r._vk.UpdateDescriptorSets(_r._device, 1, &wd, 0, null);
            }
        }

        public void UpdateBloomExtractDescriptorSet()
        {
            if (_r._vk is null || _r._dsBloomExtract.Handle == default)
                return;

            var hdrView = _r._viewHdrComposite.Handle != default ? _r._viewHdrComposite : _r._viewHdr;
            DescriptorImageInfo h = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = hdrView,
                Sampler = _r._samplerLinear
            };

            WriteDescriptorSet w = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _r._dsBloomExtract,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &h
            };

            _r._vk!.UpdateDescriptorSets(_r._device, 1, &w, 0, null);
        }

        public void UpdateBloomUpsampleDescriptorSet(ImageView coarseView, ImageView fineView)
        {
            if (_r._vk is null || _r._dsBloomUpsample.Handle == default)
                return;

            DescriptorImageInfo c = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = coarseView,
                Sampler = _r._samplerLinear
            };

            DescriptorImageInfo f = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = fineView,
                Sampler = _r._samplerLinear
            };

            var writes = stackalloc WriteDescriptorSet[2];
            writes[0] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _r._dsBloomUpsample,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &c
            };
            writes[1] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _r._dsBloomUpsample,
                DstBinding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &f
            };

            _r._vk!.UpdateDescriptorSets(_r._device, 2, writes, 0, null);
        }

        public void UpdateEmissiveSceneDescriptorSet()
        {
            if (_r._vk is null || _r._dsEmissiveScene.Handle == default)
                return;

            DescriptorImageInfo e = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = _r._viewEmissive,
                Sampler = _r._samplerLinear
            };

            WriteDescriptorSet w = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _r._dsEmissiveScene,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &e
            };

            _r._vk!.UpdateDescriptorSets(_r._device, 1, &w, 0, null);
        }

        public void UpdateLightingDescriptorSet()
        {
            if (_r._vk is null || _r._dsLighting.Handle == default || _r._lightingBuffer.Handle == default)
                return;

            DescriptorBufferInfo bi = new()
            {
                Buffer = _r._lightingBuffer,
                Offset = 0,
                Range = (ulong)sizeof(LightingUbo)
            };

            WriteDescriptorSet w = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _r._dsLighting,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PBufferInfo = &bi
            };

            _r._vk!.UpdateDescriptorSets(_r._device, 1, &w, 0, null);
        }
    }
}
