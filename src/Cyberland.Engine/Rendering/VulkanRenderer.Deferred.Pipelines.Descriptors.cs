using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

// Purpose: Descriptor set layouts, pool, deferred/transparent descriptor allocation and updates, point-light SSBO ensure.
// Uses <see cref="VulkanGraphicsPipelineHelpers"/> for layout creation; see GraphicsPipelines partial for shader/pipeline objects.

/// <summary>Descriptor layouts and binding updates for deferred rendering (partial).</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void CreateDescriptorLayoutsAndPool()
    {
        Span<DescriptorSetLayoutBinding> tex = stackalloc DescriptorSetLayoutBinding[1];
        tex[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, tex, out _dslTexture, "dsl texture failed.");

        Span<DescriptorSetLayoutBinding> b = stackalloc DescriptorSetLayoutBinding[3];
        b[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        b[1] = new DescriptorSetLayoutBinding
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        b[2] = new DescriptorSetLayoutBinding
        {
            Binding = 2,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, b, out _dslComposite, "dsl composite failed.");

        Span<DescriptorSetLayoutBinding> oneTex = stackalloc DescriptorSetLayoutBinding[1];
        oneTex[0] = b[0];
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, oneTex, out _dslBloomExtract, "dsl bloom extract failed.");

        Span<DescriptorSetLayoutBinding> bloomDual = stackalloc DescriptorSetLayoutBinding[2];
        bloomDual[0] = b[0];
        bloomDual[1] = b[1];
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, bloomDual, out _dslBloomDual, "dsl bloom dual failed.");

        Span<DescriptorSetLayoutBinding> em = stackalloc DescriptorSetLayoutBinding[1];
        em[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, em, out _dslEmissiveScene, "dsl emissive scene failed.");

        Span<DescriptorSetLayoutBinding> light = stackalloc DescriptorSetLayoutBinding[1];
        light[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, light, out _dslLighting, "dsl lighting failed.");

        Span<DescriptorSetLayoutBinding> gbuf = stackalloc DescriptorSetLayoutBinding[2];
        gbuf[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        gbuf[1] = new DescriptorSetLayoutBinding
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit | ShaderStageFlags.VertexBit
        };
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, gbuf, out _dslGbufferRead, "dsl gbuffer read failed.");

        Span<DescriptorSetLayoutBinding> ssbo = stackalloc DescriptorSetLayoutBinding[1];
        ssbo[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit | ShaderStageFlags.VertexBit
        };
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, ssbo, out _dslPointSsbo, "dsl point ssbo failed.");

        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, b, out _dslTransparentResolve, "dsl transparent resolve failed.");

        DescriptorPoolSize ps1 = new() { Type = DescriptorType.CombinedImageSampler, DescriptorCount = 640 };
        DescriptorPoolSize ps2 = new() { Type = DescriptorType.UniformBuffer, DescriptorCount = 40 };
        DescriptorPoolSize ps3 = new() { Type = DescriptorType.StorageBuffer, DescriptorCount = 8 };
        var poolSizes = stackalloc DescriptorPoolSize[3];
        poolSizes[0] = ps1;
        poolSizes[1] = ps2;
        poolSizes[2] = ps3;

        DescriptorPoolCreateInfo dpci = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = 640,
            PoolSizeCount = 3,
            PPoolSizes = poolSizes
        };

        if (_vk!.CreateDescriptorPool(_device, in dpci, null, out _descriptorPool) != Result.Success)
            throw new GraphicsInitializationException("descriptor pool failed.");
    }

    private void AllocateCompositeDescriptorSet()
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.AllocateCompositeDescriptorSet();
    }

    private void UpdateCompositeDescriptorSet()
    {
        var hdrView = _viewHdrComposite.Handle != default ? _viewHdrComposite : _viewHdr;
        DescriptorImageInfo h = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = hdrView,
            Sampler = _samplerLinear
        };

        DescriptorImageInfo e = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _viewEmissive,
            Sampler = _samplerLinear
        };

        DescriptorImageInfo bl = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _viewBloom0,
            Sampler = _samplerLinear
        };

        var writes = stackalloc WriteDescriptorSet[3];
        for (var fi = 0; fi < MaxFramesInFlight; fi++)
        {
            writes[0] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _dsCompositeSlots[fi],
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &h
            };
            writes[1] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _dsCompositeSlots[fi],
                DstBinding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &e
            };
            writes[2] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _dsCompositeSlots[fi],
                DstBinding = 2,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &bl
            };

            _vk!.UpdateDescriptorSets(_device, 3, writes, 0, null);
        }
    }

    /// <summary>
    /// Bloom extract and composite both sample the same HDR scene texture; that image is either opaque-only (<see cref="_viewHdr"/>)
    /// or post-WBOIT resolve (<see cref="_viewHdrComposite"/>). Updates both bindings for the current frame before post-process.
    /// </summary>
    private void UpdateSceneHdrSourcesForPostProcess(ImageView sceneHdr)
    {
        if (_vk is null || _dsBloomExtract.Handle == default)
            return;

        DescriptorImageInfo h = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = sceneHdr,
            Sampler = _samplerLinear
        };

        WriteDescriptorSet wb = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsBloomExtract,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &h
        };

        WriteDescriptorSet wc = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsCompositeSlots[_currentFrame],
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &h
        };

        var batch = stackalloc WriteDescriptorSet[2];
        batch[0] = wb;
        batch[1] = wc;
        _vk.UpdateDescriptorSets(_device, 2, batch, 0, null);
    }

    private void UpdateCompositeBloomSource(ImageView bloomFinalView)
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.UpdateCompositeBloomSource(bloomFinalView);
    }

    private void AllocateBloomDescriptorSets()
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.AllocateBloomDescriptorSets();
    }

    /// <summary>
    /// One descriptor set per half-res bloom texture so Gaussian passes do not overwrite bindings between draws.
    /// </summary>
    private void UpdateBloomGaussianDescriptorSets()
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.UpdateBloomGaussianDescriptorSets();
    }

    private void UpdateBloomExtractDescriptorSet()
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.UpdateBloomExtractDescriptorSet();
    }

    private void UpdateBloomUpsampleDescriptorSet(ImageView coarseView, ImageView fineView)
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.UpdateBloomUpsampleDescriptorSet(coarseView, fineView);
    }

    private void AllocateEmissiveSceneDescriptorSet()
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.AllocateEmissiveSceneDescriptorSet();
    }

    private void UpdateEmissiveSceneDescriptorSet()
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.UpdateEmissiveSceneDescriptorSet();
    }

    private void AllocateLightingDescriptorSet()
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.AllocateLightingDescriptorSet();
    }

    private void UpdateLightingDescriptorSet()
    {
        _descriptorManager ??= new DescriptorManager(this);
        _descriptorManager.UpdateLightingDescriptorSet();
    }

    private void EnsurePointLightSsbo()
    {
        if (_pointLightSsbo.Handle != default)
            return;
        var bytes = (ulong)(DeferredRenderingConstants.MaxPointLights * 2 * sizeof(Vector4D<float>));
        CreateHostVisibleBuffer(bytes, BufferUsageFlags.StorageBufferBit, out _pointLightSsbo, out _pointLightSsboMemory);
        void* p;
        if (_vk!.MapMemory(_device, _pointLightSsboMemory, 0, bytes, 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map point ssbo (persistent)");
        _pointLightSsboMapped = p;
    }

    private void AllocateDeferredDescriptorSets()
    {
        fixed (DescriptorSetLayout* dslGb = &_dslGbufferRead)
        {
            DescriptorSetAllocateInfo ai = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = dslGb
            };
            if (_vk!.AllocateDescriptorSets(_device, in ai, out _dsGbufferRead) != Result.Success)
                throw new GraphicsInitializationException("alloc ds gbuffer");
        }

        fixed (DescriptorSetLayout* dslP = &_dslPointSsbo)
        {
            DescriptorSetAllocateInfo ai = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = dslP
            };
            if (_vk!.AllocateDescriptorSets(_device, in ai, out _dsPointSsbo) != Result.Success)
                throw new GraphicsInitializationException("alloc ds point ssbo");
        }

        fixed (DescriptorSetLayout* dslTr = &_dslTransparentResolve)
        {
            DescriptorSetAllocateInfo ai = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = dslTr
            };
            if (_vk!.AllocateDescriptorSets(_device, in ai, out _dsTransparentResolve) != Result.Success)
                throw new GraphicsInitializationException("alloc ds transparent resolve");
        }

        fixed (DescriptorSetLayout* dslT = &_dslTexture)
        {
            DescriptorSetAllocateInfo ai = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = dslT
            };
            if (_vk!.AllocateDescriptorSets(_device, in ai, out _dsHdrOpaqueForTransparent) != Result.Success)
                throw new GraphicsInitializationException("alloc ds hdr opaque sample");
        }

        UpdateDeferredGbufferAndResolveDescriptorSets();
        UpdatePointSsboDescriptorSet();
    }

    private void UpdatePointSsboDescriptorSet()
    {
        if (_vk is null || _dsPointSsbo.Handle == default || _pointLightSsbo.Handle == default)
            return;

        DescriptorBufferInfo bi = new()
        {
            Buffer = _pointLightSsbo,
            Offset = 0,
            Range = (ulong)(DeferredRenderingConstants.MaxPointLights * 2 * sizeof(Vector4D<float>))
        };

        WriteDescriptorSet w = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsPointSsbo,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            PBufferInfo = &bi
        };

        _vk!.UpdateDescriptorSets(_device, 1, &w, 0, null);
    }

    private void UpdateDeferredGbufferAndResolveDescriptorSets()
    {
        if (_vk is null || _viewGbuf0.Handle == default)
            return;

        DescriptorImageInfo i0 = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _viewGbuf0,
            Sampler = _samplerLinear
        };
        DescriptorImageInfo i1 = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _viewGbuf1,
            Sampler = _samplerLinear
        };

        var writes = stackalloc WriteDescriptorSet[2];
        writes[0] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsGbufferRead,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &i0
        };
        writes[1] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsGbufferRead,
            DstBinding = 1,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &i1
        };
        _vk!.UpdateDescriptorSets(_device, 2, writes, 0, null);

        if (_viewHdr.Handle == default || _viewWAccum.Handle == default)
            return;

        DescriptorImageInfo hOp = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _viewHdr,
            Sampler = _samplerLinear
        };
        DescriptorImageInfo wa = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _viewWAccum,
            Sampler = _samplerLinear
        };
        DescriptorImageInfo wr = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _viewWReveal,
            Sampler = _samplerLinear
        };

        var wrs = stackalloc WriteDescriptorSet[3];
        wrs[0] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsTransparentResolve,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &hOp
        };
        wrs[1] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsTransparentResolve,
            DstBinding = 1,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &wa
        };
        wrs[2] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsTransparentResolve,
            DstBinding = 2,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &wr
        };
        _vk.UpdateDescriptorSets(_device, 3, wrs, 0, null);

        WriteDescriptorSet wh = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dsHdrOpaqueForTransparent,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &hOp
        };
        _vk.UpdateDescriptorSets(_device, 1, &wh, 0, null);
    }
}
