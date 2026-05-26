using System.Text;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VulkanSemaphore = Silk.NET.Vulkan.Semaphore;

namespace Cyberland.Engine.Rendering;

// Purpose: VK_EXT_debug_utils command-buffer labels + vkSetDebugUtilsObjectNameEXT wiring for RenderDoc.
// Gated to Debug builds by default; set CYBERLAND_VULKAN_GPU_LABELS=1 to enable in Release captures.

/// <summary>Partial <see cref="VulkanRenderer"/>: GPU debug markers and Vulkan object naming.</summary>
public sealed unsafe partial class VulkanRenderer
{
    internal const string VkExtDebugUtilsName = "VK_EXT_debug_utils";

    /// <summary>UTF-8 byte budget for one marker label (excluding mandatory NUL); longer names truncate.</summary>
    internal const int MaxGpuDebugLabelUtf8Bytes = 256;

    private ExtDebugUtils? _extDebugUtils;
    private bool _gpuDebugMarkersEnabled;

    private static bool GpuDebugMarkersRequestedByConfiguration =>
#if DEBUG
        true;
#else
        string.Equals(Environment.GetEnvironmentVariable("CYBERLAND_VULKAN_GPU_LABELS"), "1", StringComparison.Ordinal);
#endif

    private void RefreshGpuDebugMarkersEnabled() =>
        _gpuDebugMarkersEnabled = _extDebugUtils is not null && GpuDebugMarkersRequestedByConfiguration;

    private static string[] AppendDebugUtilsInstanceExtension(string[] extensions)
    {
        foreach (var e in extensions)
        {
            if (e == VkExtDebugUtilsName)
                return extensions;
        }

        var appended = new string[extensions.Length + 1];
        extensions.AsSpan().CopyTo(appended);
        appended[^1] = VkExtDebugUtilsName;
        return appended;
    }

    private void TryInitializeExtDebugUtilsExtension()
    {
        if (!_vk!.TryGetInstanceExtension(_instance, out ExtDebugUtils ext))
            _extDebugUtils = null;
        else
            _extDebugUtils = ext;
    }

    private void DisposeExtDebugUtils()
    {
        _extDebugUtils?.Dispose();
        _extDebugUtils = null;
        _gpuDebugMarkersEnabled = false;
    }

    /// <summary>Begins a nested command-buffer debug region when debug utils + policy allow it.</summary>
    internal void BeginGpuLabel(CommandBuffer cmd, ReadOnlySpan<char> name, float r = 1f, float g = 1f, float b = 1f, float a = 1f)
    {
        if (!_gpuDebugMarkersEnabled || name.Length == 0 || cmd.Handle == default)
            return;

        Span<byte> utf8 = stackalloc byte[MaxGpuDebugLabelUtf8Bytes];
        var written = Encoding.UTF8.GetBytes(name, utf8);
        if (written >= utf8.Length)
            written = utf8.Length - 1;
        utf8[written] = 0;

        fixed (byte* plabel = utf8)
        {
            var label = new DebugUtilsLabelEXT
            {
                SType = StructureType.DebugUtilsLabelExt,
                PLabelName = plabel
            };
            label.Color[0] = r;
            label.Color[1] = g;
            label.Color[2] = b;
            label.Color[3] = a;
            _extDebugUtils!.CmdBeginDebugUtilsLabel(cmd, in label);
        }
    }

    /// <summary>Ends the innermost command-buffer debug region started with <see cref="BeginGpuLabel"/>.</summary>
    internal void EndGpuLabel(CommandBuffer cmd)
    {
        if (!_gpuDebugMarkersEnabled || cmd.Handle == default)
            return;
        _extDebugUtils!.CmdEndDebugUtilsLabel(cmd);
    }

    /// <summary>Optional queue-level marker around submits (visible on some tools as queue timeline annotations).</summary>
    internal void BeginGpuQueueLabel(string name, float r = 1f, float g = 1f, float b = 1f, float a = 1f)
    {
        if (!_gpuDebugMarkersEnabled || string.IsNullOrEmpty(name) || _graphicsQueue.Handle == default)
            return;

        Span<byte> utf8 = stackalloc byte[MaxGpuDebugLabelUtf8Bytes];
        var written = Encoding.UTF8.GetBytes(name, utf8);
        if (written >= utf8.Length)
            written = utf8.Length - 1;
        utf8[written] = 0;

        fixed (byte* plabel = utf8)
        {
            var label = new DebugUtilsLabelEXT
            {
                SType = StructureType.DebugUtilsLabelExt,
                PLabelName = plabel
            };
            label.Color[0] = r;
            label.Color[1] = g;
            label.Color[2] = b;
            label.Color[3] = a;
            _extDebugUtils!.QueueBeginDebugUtilsLabel(_graphicsQueue, in label);
        }
    }

    internal void EndGpuQueueLabel()
    {
        if (!_gpuDebugMarkersEnabled || _graphicsQueue.Handle == default)
            return;
        _extDebugUtils!.QueueEndDebugUtilsLabel(_graphicsQueue);
    }

    /// <summary>Assigns a persistent debug name to a Vulkan handle (no-op if unavailable / invalid).</summary>
    internal void SetGpuObjectName(ObjectType objectType, ulong objectHandle, ReadOnlySpan<char> name)
    {
        if (!_gpuDebugMarkersEnabled || objectHandle == 0 || name.Length == 0)
            return;

        Span<byte> utf8 = stackalloc byte[MaxGpuDebugLabelUtf8Bytes];
        var written = Encoding.UTF8.GetBytes(name, utf8);
        if (written >= utf8.Length)
            written = utf8.Length - 1;
        utf8[written] = 0;

        fixed (byte* pname = utf8)
        {
            var info = new DebugUtilsObjectNameInfoEXT
            {
                SType = StructureType.DebugUtilsObjectNameInfoExt,
                ObjectType = objectType,
                ObjectHandle = objectHandle,
                PObjectName = pname
            };
            _ = _extDebugUtils!.SetDebugUtilsObjectName(_device, in info);
        }
    }

    internal static ulong VkHandle(RenderPass v) => v.Handle;

    internal static ulong VkHandle(Pipeline v) => v.Handle;

    internal static ulong VkHandle(PipelineLayout v) => v.Handle;

    internal static ulong VkHandle(Framebuffer v) => v.Handle;

    internal static ulong VkHandle(Image v) => v.Handle;

    internal static ulong VkHandle(ImageView v) => v.Handle;

    internal static ulong VkHandle(VkBuffer v) => v.Handle;

    internal static ulong VkHandle(DeviceMemory v) => v.Handle;

    internal static ulong VkHandle(CommandBuffer v) => (ulong)(nuint)v.Handle;

    internal static ulong VkHandle(CommandPool v) => v.Handle;

    internal static ulong VkHandle(DescriptorPool v) => v.Handle;

    internal static ulong VkHandle(DescriptorSetLayout v) => v.Handle;

    internal static ulong VkHandle(DescriptorSet v) => v.Handle;

    internal static ulong VkHandle(Sampler v) => v.Handle;

    internal static ulong VkHandle(ShaderModule v) => v.Handle;

    internal static ulong VkHandle(VulkanSemaphore v) => v.Handle;

    internal static ulong VkHandle(Fence v) => v.Handle;

    internal static ulong VkHandle(SwapchainKHR v) => v.Handle;

    /// <summary>
    /// Labels swapchain images/views after (re)creating them — safe whenever swapchain images are rebuilt.
    /// </summary>
    private void NameSwapchainImagesAndViewsForRenderDoc()
    {
        if (!_gpuDebugMarkersEnabled || _swapchainImages is null || _swapchainImageViews is null)
            return;
        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            SetGpuObjectName(ObjectType.Image, VkHandle(_swapchainImages[i]), $"img.Swapchain[{i}]");
            SetGpuObjectName(ObjectType.ImageView, VkHandle(_swapchainImageViews[i]), $"view.Swapchain[{i}]");
        }
    }

    /// <summary>
    /// Labels HDR offscreen color targets after allocation or swapchain resize recreation.
    /// </summary>
    private void NameHdrOffscreenTargetsForRenderDoc()
    {
        if (!_gpuDebugMarkersEnabled)
            return;

        void Quad(Image img, DeviceMemory mem, ImageView view, Framebuffer fb, string stem)
        {
            SetGpuObjectName(ObjectType.Image, VkHandle(img), $"img.{stem}");
            SetGpuObjectName(ObjectType.DeviceMemory, VkHandle(mem), $"mem.{stem}");
            SetGpuObjectName(ObjectType.ImageView, VkHandle(view), $"view.{stem}");
            if (fb.Handle != default)
                SetGpuObjectName(ObjectType.Framebuffer, VkHandle(fb), $"fb.{stem}");
        }

        Quad(_imgEmissive, _memEmissive, _viewEmissive, _fbEmissive, "Emissive");
        Quad(_imgHdr, _memHdr, _viewHdr, _fbHdr, "HdrScene");
        Quad(_imgGbuf0, _memGbuf0, _viewGbuf0, default, "Gbuffer0");
        Quad(_imgGbuf1, _memGbuf1, _viewGbuf1, default, "Gbuffer1");
        SetGpuObjectName(ObjectType.Framebuffer, VkHandle(_fbGbuffer), "fb.Gbuffer");
        Quad(_imgWAccum, _memWAccum, _viewWAccum, default, "WboitAccum");
        Quad(_imgWReveal, _memWReveal, _viewWReveal, default, "WboitReveal");
        SetGpuObjectName(ObjectType.Framebuffer, VkHandle(_fbWboit), "fb.Wboit");
        Quad(_imgHdrComposite, _memHdrComposite, _viewHdrComposite, _fbHdrComposite, "HdrComposite");
        Quad(_imgBloom0, _memBloom0, _viewBloom0, _fbBloom0, "Bloom0");
        Quad(_imgBloom1, _memBloom1, _viewBloom1, _fbBloom1, "Bloom1");
        for (var i = 0; i < DeferredRenderingConstants.BloomDownsampleLevels; i++)
            Quad(_imgBloomDown[i], _memBloomDown[i], _viewBloomDown[i], _fbBloomDown[i], $"BloomDown[{i}]");
    }

    /// <summary>
    /// Applies vkSetDebugUtilsObjectNameEXT once deferred bootstrap finished allocating layouts/pipelines/descriptor sets.
    /// </summary>
    private void ApplyDeferredGpuDebugNamesAfterBootstrap()
    {
        if (!_gpuDebugMarkersEnabled)
            return;

        SetGpuObjectName(ObjectType.RenderPass, VkHandle(_rpOffscreenInitialUndefined), "rp.Offscreen.InitialUndefined");
        SetGpuObjectName(ObjectType.RenderPass, VkHandle(_rpOffscreenInitialShaderRead), "rp.Offscreen.InitialShaderRead");
        SetGpuObjectName(ObjectType.RenderPass, VkHandle(_rpComposite), "rp.CompositeSwapchain");
        SetGpuObjectName(ObjectType.RenderPass, VkHandle(_rpSwapchainUiOverlay), "rp.SwapchainUiOverlay");
        SetGpuObjectName(ObjectType.RenderPass, VkHandle(_rpGbufferUndefined), "rp.Gbuffer.Undefined");
        SetGpuObjectName(ObjectType.RenderPass, VkHandle(_rpGbufferShaderRead), "rp.Gbuffer.ShaderRead");
        SetGpuObjectName(ObjectType.RenderPass, VkHandle(_rpWboitUndefined), "rp.Wboit.Undefined");
        SetGpuObjectName(ObjectType.RenderPass, VkHandle(_rpWboitShaderRead), "rp.Wboit.ShaderRead");

        SetGpuObjectName(ObjectType.Sampler, VkHandle(_samplerLinear), "sampler.LinearClamp");

        SetGpuObjectName(ObjectType.DescriptorPool, VkHandle(_descriptorPool), "descriptorPool.FrameGlobal");

        SetGpuObjectName(ObjectType.DescriptorSetLayout, VkHandle(_dslTexture), "dsl.Texture");
        SetGpuObjectName(ObjectType.DescriptorSetLayout, VkHandle(_dslComposite), "dsl.Composite");
        SetGpuObjectName(ObjectType.DescriptorSetLayout, VkHandle(_dslBloomExtract), "dsl.BloomExtract");
        SetGpuObjectName(ObjectType.DescriptorSetLayout, VkHandle(_dslBloomDual), "dsl.BloomDual");
        SetGpuObjectName(ObjectType.DescriptorSetLayout, VkHandle(_dslEmissiveScene), "dsl.EmissiveScene");
        SetGpuObjectName(ObjectType.DescriptorSetLayout, VkHandle(_dslGbufferRead), "dsl.GbufferRead");
        SetGpuObjectName(ObjectType.DescriptorSetLayout, VkHandle(_dslTransparentResolve), "dsl.TransparentResolve");

        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plSpriteTwoTexture), "pl.SpriteTwoTexture");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plTextMsdf), "pl.TextMsdf");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plComposite), "pl.Composite");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plBloomExtract), "pl.BloomExtract");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plBloomDownsample), "pl.BloomDownsample");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plBloomGaussian), "pl.BloomGaussian");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plBloomUpsample), "pl.BloomUpsample");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plBloomCopy), "pl.BloomCopy");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plDeferredBleed), "pl.DeferredBleed");
        SetGpuObjectName(ObjectType.PipelineLayout, VkHandle(_plTransparentResolve), "pl.TransparentResolve");

        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeEmissive), "pipe.SpriteEmissive");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeSpriteGbuffer), "pipe.SpriteGbuffer");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeSwapchainUiOverlay), "pipe.SwapchainUiOverlay");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeTextMsdf), "pipe.TextMsdf");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeDeferredBleed), "pipe.DeferredBleed");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeTransparentWboit), "pipe.TransparentWboit");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeTransparentResolve), "pipe.TransparentResolve");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeComposite), "pipe.Composite");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeBloomExtract), "pipe.BloomExtract");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeBloomDownsample), "pipe.BloomDownsample");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeBloomGaussian), "pipe.BloomGaussian");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeBloomUpsample), "pipe.BloomUpsample");
        SetGpuObjectName(ObjectType.Pipeline, VkHandle(_pipeBloomCopy), "pipe.BloomCopy");

        SetGpuObjectName(ObjectType.Buffer, VkHandle(_pointLightSsbo), "buf.PointLightSsbo");
        SetGpuObjectName(ObjectType.DeviceMemory, VkHandle(_pointLightSsboMemory), "mem.PointLightSsbo");
        SetGpuObjectName(ObjectType.Buffer, VkHandle(_directionalLightSsbo), "buf.DirectionalLightSsbo");
        SetGpuObjectName(ObjectType.DeviceMemory, VkHandle(_directionalLightSsboMemory), "mem.DirectionalLightSsbo");
        SetGpuObjectName(ObjectType.Buffer, VkHandle(_spotLightSsbo), "buf.SpotLightSsbo");
        SetGpuObjectName(ObjectType.DeviceMemory, VkHandle(_spotLightSsboMemory), "mem.SpotLightSsbo");
        SetGpuObjectName(ObjectType.Buffer, VkHandle(_lightingBuffer), "buf.LightingUbo");
        SetGpuObjectName(ObjectType.DeviceMemory, VkHandle(_lightingBufferMemory), "mem.LightingUbo");

        SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsEmissiveScene), "ds.EmissiveScene");
        SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsBloomExtract), "ds.BloomExtract");
        SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsBloomUpsample), "ds.BloomUpsample");
        SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsBloomGaussianSrcBloom0), "ds.BloomGaussianSrcBloom0");
        SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsBloomGaussianSrcBloom1), "ds.BloomGaussianSrcBloom1");
        SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsGbufferRead), "ds.GbufferRead");
        SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsTransparentResolve), "ds.TransparentResolve");
        SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsHdrOpaqueForTransparent), "ds.HdrOpaqueForTransparent");

        for (var i = 0; i < DeferredRenderingConstants.BloomDownsampleLevels + 1; i++)
            SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsBloomDownSrc[i]), $"ds.BloomDownSrc[{i}]");

        for (var fi = 0; fi < MaxFramesInFlight; fi++)
            SetGpuObjectName(ObjectType.DescriptorSet, VkHandle(_dsCompositeSlots[fi]), $"ds.Composite[{fi}]");
    }
}
