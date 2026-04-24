using System.Runtime.InteropServices;
using Glslang.NET;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

// Purpose: Shader modules and graphics pipelines for emissive, G-buffer, deferred lighting, WBOIT, bloom, and composite.
// Shared blend factors and fullscreen pass setup live in <see cref="VulkanGraphicsPipelineHelpers"/>.

/// <summary>Shader compilation and graphics pipeline creation for deferred rendering (partial).</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void CompileSpriteAndCompositeShaders()
    {
        var vertSprite = EngineShaderSources.Load(EngineShaderSources.SpriteVert);
        var fragEm = EngineShaderSources.Load(EngineShaderSources.SpriteEmissiveFrag);
        var vertComp = EngineShaderSources.Load(EngineShaderSources.CompositeVert);
        var fragComp = EngineShaderSources.Load(EngineShaderSources.CompositeFrag);
        var fragBloomEx = EngineShaderSources.Load(EngineShaderSources.BloomExtractFrag);
        var fragBloomDn = EngineShaderSources.Load(EngineShaderSources.BloomDownsampleFrag);
        var fragBloomG = EngineShaderSources.Load(EngineShaderSources.BloomGaussianFrag);
        var fragBloomUp = EngineShaderSources.Load(EngineShaderSources.BloomUpsampleFrag);
        var fragBloomCopy = EngineShaderSources.Load(EngineShaderSources.BloomCopyFrag);

        _modVertSprite = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(vertSprite, ShaderStage.Vertex));
        _modFragEmissive = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragEm, ShaderStage.Fragment));
        _modVertComposite = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(vertComp, ShaderStage.Vertex));
        _modFragComposite = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragComp, ShaderStage.Fragment));
        _modFragBloomExtract = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragBloomEx, ShaderStage.Fragment));
        _modFragBloomDownsample = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragBloomDn, ShaderStage.Fragment));
        _modFragBloomGaussian = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragBloomG, ShaderStage.Fragment));
        _modFragBloomUpsample = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragBloomUp, ShaderStage.Fragment));
        _modFragBloomCopy = CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(fragBloomCopy, ShaderStage.Fragment));
        CompileDeferredShaderModules();
    }

    private void CreateSpritePipelineLayoutsAndPipelines()
    {
        var pushSprite = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)sizeof(SpritePushData)
        };

        var pcr = stackalloc PushConstantRange[1];
        pcr[0] = pushSprite;

        var dslTwoTex = stackalloc DescriptorSetLayout[2];
        dslTwoTex[0] = _dslTexture;
        dslTwoTex[1] = _dslTexture;

        PipelineLayoutCreateInfo plTwo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 2,
            PSetLayouts = dslTwoTex,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcr
        };

        if (_vk!.CreatePipelineLayout(_device, in plTwo, null, out _plSpriteEmissive) != Result.Success)
            throw new GraphicsInitializationException("pl sprite two-texture (emissive/gbuffer/wboit) failed.");

        var mainName = Marshal.StringToHGlobalAnsi("main");

        PipelineShaderStageCreateInfo vertSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _modVertSprite,
            PName = (byte*)mainName
        };

        PipelineShaderStageCreateInfo fragEmSt = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragEmissive,
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

        PipelineInputAssemblyStateCreateInfo ia = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false
        };

        PipelineViewportStateCreateInfo vp = new()
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

        var blendSprite = VulkanGraphicsPipelineHelpers.BlendAttachmentPresets.SpriteEmissiveAdditive;
        PipelineColorBlendStateCreateInfo cbAdd = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 1,
            PAttachments = &blendSprite
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

            var stEm = stackalloc PipelineShaderStageCreateInfo[2];
            stEm[0] = vertSt;
            stEm[1] = fragEmSt;

            GraphicsPipelineCreateInfo gpEm = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stEm,
                PVertexInputState = &vi,
                PInputAssemblyState = &ia,
                PViewportState = &vp,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cbAdd,
                PDynamicState = &ds,
                Layout = _plSpriteEmissive,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };

            if (_vk!.CreateGraphicsPipelines(_device, default, 1, in gpEm, null, out _pipeEmissive) != Result.Success)
                throw new GraphicsInitializationException("pipe emissive failed.");
        }

        Marshal.FreeHGlobal(mainName);
        CreateDeferredAndTransparencyPipelines();
    }

    private void CreateBloomPipelineLayoutsAndPipelines()
    {
        var pushEx = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)sizeof(BloomExtractPush)
        };

        var pushG = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)sizeof(BloomGaussianPush)
        };
        var pushResample = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)sizeof(BloomResamplePush)
        };

        var pcrEx = stackalloc PushConstantRange[1];
        pcrEx[0] = pushEx;
        var pcrG = stackalloc PushConstantRange[1];
        pcrG[0] = pushG;
        var pcrResample = stackalloc PushConstantRange[1];
        pcrResample[0] = pushResample;

        var dslEx = stackalloc DescriptorSetLayout[1];
        dslEx[0] = _dslBloomExtract;
        PipelineLayoutCreateInfo plEx = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = dslEx,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcrEx
        };

        if (_vk!.CreatePipelineLayout(_device, in plEx, null, out _plBloomExtract) != Result.Success)
            throw new GraphicsInitializationException("pl bloom extract failed.");

        var dslSimple = stackalloc DescriptorSetLayout[1];
        dslSimple[0] = _dslTexture;

        PipelineLayoutCreateInfo plDn = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = dslSimple,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcrResample
        };

        if (_vk.CreatePipelineLayout(_device, in plDn, null, out _plBloomDownsample) != Result.Success)
            throw new GraphicsInitializationException("pl bloom downsample failed.");

        var dslG = stackalloc DescriptorSetLayout[1];
        dslG[0] = _dslTexture;
        PipelineLayoutCreateInfo plG = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = dslG,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcrG
        };

        if (_vk.CreatePipelineLayout(_device, in plG, null, out _plBloomGaussian) != Result.Success)
            throw new GraphicsInitializationException("pl bloom gaussian failed.");

        var dslUpsample = stackalloc DescriptorSetLayout[1];
        dslUpsample[0] = _dslBloomDual;
        PipelineLayoutCreateInfo plUp = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = dslUpsample,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pcrResample
        };

        if (_vk.CreatePipelineLayout(_device, in plUp, null, out _plBloomUpsample) != Result.Success)
            throw new GraphicsInitializationException("pl bloom upsample failed.");

        PipelineLayoutCreateInfo plCopy = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = dslSimple,
            PushConstantRangeCount = 0,
            PPushConstantRanges = null
        };

        if (_vk.CreatePipelineLayout(_device, in plCopy, null, out _plBloomCopy) != Result.Success)
            throw new GraphicsInitializationException("pl bloom copy failed.");

        var mainName = Marshal.StringToHGlobalAnsi("main");

        PipelineShaderStageCreateInfo vs = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _modVertComposite,
            PName = (byte*)mainName
        };

        PipelineShaderStageCreateInfo fsEx = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragBloomExtract,
            PName = (byte*)mainName
        };

        PipelineShaderStageCreateInfo fsG = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragBloomGaussian,
            PName = (byte*)mainName
        };

        PipelineShaderStageCreateInfo fsDn = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragBloomDownsample,
            PName = (byte*)mainName
        };

        PipelineShaderStageCreateInfo fsUp = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragBloomUpsample,
            PName = (byte*)mainName
        };

        PipelineShaderStageCreateInfo fsCopy = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragBloomCopy,
            PName = (byte*)mainName
        };

        VulkanGraphicsPipelineHelpers.CreateFullscreenTrianglePostProcessPipeline(
            _vk!, _device, _plBloomExtract, _rpOffscreenInitialUndefined, 0, vs, fsEx, out _pipeBloomExtract, "pipe bloom extract failed.");
        VulkanGraphicsPipelineHelpers.CreateFullscreenTrianglePostProcessPipeline(
            _vk!, _device, _plBloomDownsample, _rpOffscreenInitialUndefined, 0, vs, fsDn, out _pipeBloomDownsample, "pipe bloom downsample failed.");
        VulkanGraphicsPipelineHelpers.CreateFullscreenTrianglePostProcessPipeline(
            _vk!, _device, _plBloomGaussian, _rpOffscreenInitialUndefined, 0, vs, fsG, out _pipeBloomGaussian, "pipe bloom gaussian failed.");
        VulkanGraphicsPipelineHelpers.CreateFullscreenTrianglePostProcessPipeline(
            _vk!, _device, _plBloomUpsample, _rpOffscreenInitialUndefined, 0, vs, fsUp, out _pipeBloomUpsample, "pipe bloom upsample failed.");
        VulkanGraphicsPipelineHelpers.CreateFullscreenTrianglePostProcessPipeline(
            _vk!, _device, _plBloomCopy, _rpOffscreenInitialUndefined, 0, vs, fsCopy, out _pipeBloomCopy, "pipe bloom copy failed.");

        Marshal.FreeHGlobal(mainName);
    }

    private void CreateCompositePipeline()
    {
        var pushComp = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)sizeof(CompositePush)
        };

        var dslC = stackalloc DescriptorSetLayout[1];
        dslC[0] = _dslComposite;

        PipelineLayoutCreateInfo plc = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = dslC,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushComp
        };

        if (_vk!.CreatePipelineLayout(_device, in plc, null, out _plComposite) != Result.Success)
            throw new GraphicsInitializationException("pl composite failed.");

        var mainName = Marshal.StringToHGlobalAnsi("main");

        PipelineShaderStageCreateInfo vs = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _modVertComposite,
            PName = (byte*)mainName
        };

        PipelineShaderStageCreateInfo fs = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragComposite,
            PName = (byte*)mainName
        };

        VulkanGraphicsPipelineHelpers.CreateFullscreenTrianglePostProcessPipeline(
            _vk!, _device, _plComposite, _rpComposite, 0, vs, fs, out _pipeComposite, "pipe composite failed.");

        Marshal.FreeHGlobal(mainName);
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
            Size = (uint)(sizeof(float) * 8)
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

        var premul = VulkanGraphicsPipelineHelpers.BlendAttachmentPresets.PremultipliedAlpha;
        var off = VulkanGraphicsPipelineHelpers.BlendAttachmentPresets.Off;
        var hdrAdd = VulkanGraphicsPipelineHelpers.BlendAttachmentPresets.HdrRgbAdditive;
        var wAccum = VulkanGraphicsPipelineHelpers.BlendAttachmentPresets.WboitAccum;
        var wReveal = VulkanGraphicsPipelineHelpers.BlendAttachmentPresets.WboitReveal;

        var cbGb = stackalloc PipelineColorBlendAttachmentState[2];
        cbGb[0] = premul;
        cbGb[1] = off;

        var cbHdrBase = stackalloc PipelineColorBlendAttachmentState[1];
        cbHdrBase[0] = off;

        var cbHdrAdd = stackalloc PipelineColorBlendAttachmentState[1];
        cbHdrAdd[0] = hdrAdd;

        var cbW = stackalloc PipelineColorBlendAttachmentState[2];
        cbW[0] = wAccum;
        cbW[1] = wReveal;

        var cbRes = stackalloc PipelineColorBlendAttachmentState[1];
        cbRes[0] = off;

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
}
