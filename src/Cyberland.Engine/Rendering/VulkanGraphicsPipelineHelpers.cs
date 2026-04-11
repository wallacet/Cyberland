using System;
using System.Diagnostics.CodeAnalysis;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Shared blend presets and Vulkan helpers for graphics pipeline construction.
/// Fullscreen post-process paths intentionally match the fixed-function fields used by bloom/composite passes.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Vulkan-only helpers; exercised through VulkanRenderer (GPU/surface).")]
internal static unsafe class VulkanGraphicsPipelineHelpers
{
    /// <summary>Standard color attachment blend factors used across sprite and deferred paths.</summary>
    internal static class BlendAttachmentPresets
    {
        internal static PipelineColorBlendAttachmentState PremultipliedAlpha => new()
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

        /// <summary>Sprite emissive path: additive RGBA (differs from HDR RGB-only add).</summary>
        internal static PipelineColorBlendAttachmentState SpriteEmissiveAdditive => new()
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

        internal static PipelineColorBlendAttachmentState Off => new()
        {
            BlendEnable = false,
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
        };

        /// <summary>Deferred point lights / emissive bleed: add RGB into HDR, replace alpha.</summary>
        internal static PipelineColorBlendAttachmentState HdrRgbAdditive => new()
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

        internal static PipelineColorBlendAttachmentState WboitAccum => new()
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

        internal static PipelineColorBlendAttachmentState WboitReveal => new()
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
    }

    internal static void CreateDescriptorSetLayoutOrThrow(
        Vk vk,
        Device device,
        ReadOnlySpan<DescriptorSetLayoutBinding> bindings,
        out DescriptorSetLayout layout,
        string failureMessage)
    {
        fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        {
            DescriptorSetLayoutCreateInfo info = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = pBindings
            };
            if (vk.CreateDescriptorSetLayout(device, in info, null, out layout) != Result.Success)
                throw new GraphicsInitializationException(failureMessage);
        }
    }

    /// <summary>
    /// Empty vertex input, triangle list, dynamic viewport/scissor, blending disabled — used by bloom stages and composite.
    /// </summary>
    internal static void CreateFullscreenTrianglePostProcessPipeline(
        Vk vk,
        Device device,
        PipelineLayout layout,
        RenderPass renderPass,
        uint subpass,
        in PipelineShaderStageCreateInfo vertStage,
        in PipelineShaderStageCreateInfo fragStage,
        out Pipeline pipeline,
        string failureMessage)
    {
        PipelineVertexInputStateCreateInfo viEmpty = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 0,
            VertexAttributeDescriptionCount = 0
        };

        PipelineInputAssemblyStateCreateInfo ia = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList
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
            CullMode = CullModeFlags.None
        };

        PipelineMultisampleStateCreateInfo ms = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };

        var blendOff = BlendAttachmentPresets.Off;
        PipelineColorBlendStateCreateInfo cb = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 1,
            PAttachments = &blendOff
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

            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = vertStage;
            stages[1] = fragStage;

            GraphicsPipelineCreateInfo gp = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vp,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cb,
                PDynamicState = &ds,
                Layout = layout,
                RenderPass = renderPass,
                Subpass = subpass
            };

            if (vk.CreateGraphicsPipelines(device, default, 1, in gp, null, out pipeline) != Result.Success)
                throw new GraphicsInitializationException(failureMessage);
        }
    }

    internal static void DestroyPipelineIfValid(Vk vk, Device device, ref Pipeline pipeline)
    {
        if (pipeline.Handle != default)
        {
            vk.DestroyPipeline(device, pipeline, null);
            pipeline = default;
        }
    }

    internal static void DestroyPipelineLayoutIfValid(Vk vk, Device device, ref PipelineLayout layout)
    {
        if (layout.Handle != default)
        {
            vk.DestroyPipelineLayout(device, layout, null);
            layout = default;
        }
    }

    internal static void DestroyRenderPassIfValid(Vk vk, Device device, ref RenderPass renderPass)
    {
        if (renderPass.Handle != default)
        {
            vk.DestroyRenderPass(device, renderPass, null);
            renderPass = default;
        }
    }

    internal static void DestroyDescriptorSetLayoutIfValid(Vk vk, Device device, ref DescriptorSetLayout dsl)
    {
        if (dsl.Handle != default)
        {
            vk.DestroyDescriptorSetLayout(device, dsl, null);
            dsl = default;
        }
    }

    internal static void DestroySamplerIfValid(Vk vk, Device device, ref Sampler sampler)
    {
        if (sampler.Handle != default)
        {
            vk.DestroySampler(device, sampler, null);
            sampler = default;
        }
    }
}
