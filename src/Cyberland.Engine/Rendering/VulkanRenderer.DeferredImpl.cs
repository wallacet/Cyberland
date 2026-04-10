using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Glslang.NET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

/// <summary>Deferred G-buffer, instanced point lights, emissive bleed, WBOIT transparency, and HDR resolve.</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void CreateGbufferAndWboitRenderPasses()
    {
        AttachmentDescription g0 = new()
        {
            Format = HdrFormat,
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

        SubpassDependency depG = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
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
            DependencyCount = 1,
            PDependencies = &depG
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
            Format = HdrFormat,
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
            Format = WboitRevealFormat,
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

        RenderPassCreateInfo rpcW = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = wAttU,
            SubpassCount = 1,
            PSubpasses = &subW,
            DependencyCount = 1,
            PDependencies = &depG
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

    private void EnsurePointLightSsbo()
    {
        if (_pointLightSsbo.Handle != default)
            return;
        var bytes = (ulong)(MaxPointLights * 2 * sizeof(Vector4D<float>));
        CreateHostVisibleBuffer(bytes, BufferUsageFlags.StorageBufferBit, out _pointLightSsbo, out _pointLightSsboMemory);
    }

    private void UploadPointLightSsboData(in FramePlan framePlan)
    {
        if (_vk is null || _pointLightSsboMemory.Handle == default)
            return;

        var pts = framePlan.PointLights;
        var n = Math.Min(pts.Length, MaxPointLights);
        void* p;
        if (_vk.MapMemory(_device, _pointLightSsboMemory, 0, (ulong)(MaxPointLights * 2 * sizeof(Vector4D<float>)), 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map point ssbo");

        var span = new Span<Vector4D<float>>((Vector4D<float>*)p, MaxPointLights * 2);
        span.Clear();
        for (var i = 0; i < n; i++)
        {
            ref readonly var pl = ref pts[i];
            var fall = pl.FalloffExponent > 1e-6f ? pl.FalloffExponent : 2f;
            span[i * 2] = new Vector4D<float>(pl.PositionWorld.X, pl.PositionWorld.Y, pl.Radius, fall);
            span[i * 2 + 1] = new Vector4D<float>(pl.Color.X, pl.Color.Y, pl.Color.Z, pl.Intensity);
        }

        _vk.UnmapMemory(_device, _pointLightSsboMemory);
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
            Range = (ulong)(MaxPointLights * 2 * sizeof(Vector4D<float>))
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

    private void CompileDeferredShaderModules()
    {
        var fragGb = EngineShaderSources.Load(EngineShaderSources.SpriteGbufferFrag);
        var fragDb = EngineShaderSources.Load(EngineShaderSources.DeferredBaseFrag);
        var vertDp = EngineShaderSources.Load(EngineShaderSources.DeferredPointVert);
        var fragDp = EngineShaderSources.Load(EngineShaderSources.DeferredPointFrag);
        var fragBleed = EngineShaderSources.Load(EngineShaderSources.DeferredEmissiveBleedFrag);
        var fragTw = EngineShaderSources.Load(EngineShaderSources.SpriteTransparentWboitFrag);
        var fragTr = EngineShaderSources.Load(EngineShaderSources.TransparentResolveFrag);

        _modFragGbuffer = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragGb, ShaderStage.Fragment));
        _modFragDeferredBase = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragDb, ShaderStage.Fragment));
        _modVertDeferredPoint = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(vertDp, ShaderStage.Vertex));
        _modFragDeferredPoint = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragDp, ShaderStage.Fragment));
        _modFragDeferredBleed = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragBleed, ShaderStage.Fragment));
        _modFragTransparentWboit = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragTw, ShaderStage.Fragment));
        _modFragTransparentResolve = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragTr, ShaderStage.Fragment));
    }

    private void CreateDeferredAndTransparencyPipelines()
    {
        var pushScr = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)(sizeof(float) * 4)
        };
        var pushPt = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)(sizeof(float) * 4)
        };

        var pcrF = stackalloc PushConstantRange[1];
        pcrF[0] = pushScr;
        var pcrP = stackalloc PushConstantRange[1];
        pcrP[0] = pushPt;

        var dslGbLit = stackalloc DescriptorSetLayout[2];
        dslGbLit[0] = _dslGbufferRead;
        dslGbLit[1] = _dslLighting;
        PipelineLayoutCreateInfo plDb = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 2,
            PSetLayouts = dslGbLit,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcrF
        };
        if (_vk!.CreatePipelineLayout(_device, in plDb, null, out _plDeferredBase) != Result.Success)
            throw new GraphicsInitializationException("pl deferred base failed.");

        var dslGbPt = stackalloc DescriptorSetLayout[2];
        dslGbPt[0] = _dslGbufferRead;
        dslGbPt[1] = _dslPointSsbo;
        PipelineLayoutCreateInfo plDpt = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 2,
            PSetLayouts = dslGbPt,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcrP
        };
        if (_vk!.CreatePipelineLayout(_device, in plDpt, null, out _plDeferredPoint) != Result.Success)
            throw new GraphicsInitializationException("pl deferred point failed.");

        var dslGbEm = stackalloc DescriptorSetLayout[2];
        dslGbEm[0] = _dslGbufferRead;
        dslGbEm[1] = _dslEmissiveScene;
        PipelineLayoutCreateInfo plBleed = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 2,
            PSetLayouts = dslGbEm,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcrF
        };
        if (_vk.CreatePipelineLayout(_device, in plBleed, null, out _plDeferredBleed) != Result.Success)
            throw new GraphicsInitializationException("pl deferred bleed failed.");

        var dslTr = stackalloc DescriptorSetLayout[1];
        dslTr[0] = _dslTransparentResolve;
        PipelineLayoutCreateInfo plTr = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = dslTr,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcrF
        };
        if (_vk.CreatePipelineLayout(_device, in plTr, null, out _plTransparentResolve) != Result.Success)
            throw new GraphicsInitializationException("pl transparent resolve failed.");

        var mainName = Marshal.StringToHGlobalAnsi("main");

        PipelineShaderStageCreateInfo vertSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _modVertSprite,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo fragGbSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragGbuffer,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo vertCompSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _modVertComposite,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo fragDbSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragDeferredBase,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo vertDpSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _modVertDeferredPoint,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo fragDpSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragDeferredPoint,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo fragBleedSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragDeferredBleed,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo fragTwSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragTransparentWboit,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo fragTrSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragTransparentResolve,
            PName = (byte*)mainName
        };

        VertexInputBindingDescription bind = new()
        {
            Binding = 0,
            Stride = 2 * sizeof(float),
            InputRate = VertexInputRate.Vertex
        };
        VertexInputAttributeDescription attr = new()
        {
            Binding = 0,
            Location = 0,
            Format = Format.R32G32Sfloat,
            Offset = 0
        };
        PipelineVertexInputStateCreateInfo vi = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 1,
            PVertexBindingDescriptions = &bind,
            VertexAttributeDescriptionCount = 1,
            PVertexAttributeDescriptions = &attr
        };
        PipelineVertexInputStateCreateInfo viEmpty = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 0,
            VertexAttributeDescriptionCount = 0
        };
        PipelineInputAssemblyStateCreateInfo ia = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false
        };
        PipelineViewportStateCreateInfo vpSt = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            ScissorCount = 1
        };
        PipelineRasterizationStateCreateInfo rs = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1f,
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.CounterClockwise
        };
        PipelineMultisampleStateCreateInfo ms = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };

        PipelineColorBlendAttachmentState blendPremul = new()
        {
            BlendEnable = true,
            SrcColorBlendFactor = BlendFactor.One,
            DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
            AlphaBlendOp = BlendOp.Add,
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
        };
        PipelineColorBlendAttachmentState blendOff = new()
        {
            BlendEnable = false,
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
        };
        PipelineColorBlendAttachmentState blendAddRgb = new()
        {
            BlendEnable = true,
            SrcColorBlendFactor = BlendFactor.One,
            DstColorBlendFactor = BlendFactor.One,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.Zero,
            DstAlphaBlendFactor = BlendFactor.One,
            AlphaBlendOp = BlendOp.Add,
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
        };

        PipelineColorBlendAttachmentState wAccum = new()
        {
            BlendEnable = true,
            SrcColorBlendFactor = BlendFactor.One,
            DstColorBlendFactor = BlendFactor.One,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.One,
            AlphaBlendOp = BlendOp.Add,
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
        };
        PipelineColorBlendAttachmentState wReveal = new()
        {
            BlendEnable = true,
            SrcColorBlendFactor = BlendFactor.Zero,
            DstColorBlendFactor = BlendFactor.OneMinusSrcColor,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.Zero,
            DstAlphaBlendFactor = BlendFactor.One,
            AlphaBlendOp = BlendOp.Add,
            ColorWriteMask = ColorComponentFlags.RBit
        };

        var cbGb = stackalloc PipelineColorBlendAttachmentState[2];
        cbGb[0] = blendPremul;
        cbGb[1] = blendOff;

        var cbHdrBase = stackalloc PipelineColorBlendAttachmentState[1];
        cbHdrBase[0] = blendOff;

        var cbHdrAdd = stackalloc PipelineColorBlendAttachmentState[1];
        cbHdrAdd[0] = blendAddRgb;

        var cbW = stackalloc PipelineColorBlendAttachmentState[2];
        cbW[0] = wAccum;
        cbW[1] = wReveal;

        var cbRes = stackalloc PipelineColorBlendAttachmentState[1];
        cbRes[0] = blendOff;

        PipelineColorBlendStateCreateInfo cbsGb = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 2,
            PAttachments = cbGb
        };
        PipelineColorBlendStateCreateInfo cbs1Off = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 1,
            PAttachments = cbHdrBase
        };
        PipelineColorBlendStateCreateInfo cbs1Add = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 1,
            PAttachments = cbHdrAdd
        };
        PipelineColorBlendStateCreateInfo cbsW = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 2,
            PAttachments = cbW
        };
        PipelineColorBlendStateCreateInfo cbsRes = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 1,
            PAttachments = cbRes
        };

        DynamicState[] dyn = [DynamicState.Viewport, DynamicState.Scissor];
        fixed (DynamicState* pDyn = dyn)
        {
            PipelineDynamicStateCreateInfo ds = new()
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dyn.Length,
                PDynamicStates = pDyn
            };

            var stGb = stackalloc PipelineShaderStageCreateInfo[2];
            stGb[0] = vertSt;
            stGb[1] = fragGbSt;
            GraphicsPipelineCreateInfo gpGb = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stGb,
                PVertexInputState = &vi,
                PInputAssemblyState = &ia,
                PViewportState = &vpSt,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cbsGb,
                PDynamicState = &ds,
                Layout = _plSpriteEmissive,
                RenderPass = _rpGbufferUndefined,
                Subpass = 0
            };
            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpGb, null, out _pipeSpriteGbuffer) != Result.Success)
                throw new GraphicsInitializationException("pipe gbuffer failed.");

            var stDb = stackalloc PipelineShaderStageCreateInfo[2];
            stDb[0] = vertCompSt;
            stDb[1] = fragDbSt;
            GraphicsPipelineCreateInfo gpDb = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stDb,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vpSt,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cbs1Off,
                PDynamicState = &ds,
                Layout = _plDeferredBase,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };
            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpDb, null, out _pipeDeferredBase) != Result.Success)
                throw new GraphicsInitializationException("pipe deferred base failed.");

            var stDpt = stackalloc PipelineShaderStageCreateInfo[2];
            stDpt[0] = vertDpSt;
            stDpt[1] = fragDpSt;
            GraphicsPipelineCreateInfo gpDpt = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stDpt,
                PVertexInputState = &vi,
                PInputAssemblyState = &ia,
                PViewportState = &vpSt,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cbs1Add,
                PDynamicState = &ds,
                Layout = _plDeferredPoint,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };
            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpDpt, null, out _pipeDeferredPoint) != Result.Success)
                throw new GraphicsInitializationException("pipe deferred point failed.");

            var stBl = stackalloc PipelineShaderStageCreateInfo[2];
            stBl[0] = vertCompSt;
            stBl[1] = fragBleedSt;
            GraphicsPipelineCreateInfo gpBl = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stBl,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vpSt,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cbs1Add,
                PDynamicState = &ds,
                Layout = _plDeferredBleed,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };
            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpBl, null, out _pipeDeferredBleed) != Result.Success)
                throw new GraphicsInitializationException("pipe deferred bleed failed.");

            var stTw = stackalloc PipelineShaderStageCreateInfo[2];
            stTw[0] = vertSt;
            stTw[1] = fragTwSt;
            GraphicsPipelineCreateInfo gpTw = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stTw,
                PVertexInputState = &vi,
                PInputAssemblyState = &ia,
                PViewportState = &vpSt,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cbsW,
                PDynamicState = &ds,
                Layout = _plSpriteEmissive,
                RenderPass = _rpWboitUndefined,
                Subpass = 0
            };
            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpTw, null, out _pipeTransparentWboit) != Result.Success)
                throw new GraphicsInitializationException("pipe transparent wboit failed.");

            var stTr = stackalloc PipelineShaderStageCreateInfo[2];
            stTr[0] = vertCompSt;
            stTr[1] = fragTrSt;
            GraphicsPipelineCreateInfo gpTr = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stTr,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vpSt,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cbsRes,
                PDynamicState = &ds,
                Layout = _plTransparentResolve,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };
            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpTr, null, out _pipeTransparentResolve) != Result.Success)
                throw new GraphicsInitializationException("pipe transparent resolve failed.");
        }

        Marshal.FreeHGlobal(mainName);
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
        if (_pipeSpriteGbuffer.Handle != default)
        {
            _vk!.DestroyPipeline(_device, _pipeSpriteGbuffer, null);
            _pipeSpriteGbuffer = default;
        }

        if (_pipeDeferredBase.Handle != default)
        {
            _vk!.DestroyPipeline(_device, _pipeDeferredBase, null);
            _pipeDeferredBase = default;
        }

        if (_pipeDeferredPoint.Handle != default)
        {
            _vk!.DestroyPipeline(_device, _pipeDeferredPoint, null);
            _pipeDeferredPoint = default;
        }

        if (_pipeDeferredBleed.Handle != default)
        {
            _vk!.DestroyPipeline(_device, _pipeDeferredBleed, null);
            _pipeDeferredBleed = default;
        }

        if (_pipeTransparentWboit.Handle != default)
        {
            _vk!.DestroyPipeline(_device, _pipeTransparentWboit, null);
            _pipeTransparentWboit = default;
        }

        if (_pipeTransparentResolve.Handle != default)
        {
            _vk!.DestroyPipeline(_device, _pipeTransparentResolve, null);
            _pipeTransparentResolve = default;
        }

        if (_plDeferredBase.Handle != default)
        {
            _vk!.DestroyPipelineLayout(_device, _plDeferredBase, null);
            _plDeferredBase = default;
        }

        if (_plDeferredPoint.Handle != default)
        {
            _vk!.DestroyPipelineLayout(_device, _plDeferredPoint, null);
            _plDeferredPoint = default;
        }

        if (_plDeferredBleed.Handle != default)
        {
            _vk!.DestroyPipelineLayout(_device, _plDeferredBleed, null);
            _plDeferredBleed = default;
        }

        if (_plTransparentResolve.Handle != default)
        {
            _vk!.DestroyPipelineLayout(_device, _plTransparentResolve, null);
            _plTransparentResolve = default;
        }
    }
}
