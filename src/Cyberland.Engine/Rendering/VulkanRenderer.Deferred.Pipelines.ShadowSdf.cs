using System.Buffers;
using System.Runtime.InteropServices;
using Cyberland.Engine.Diagnostics;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

// Purpose: GPU JFA-based SDF shadow pipeline. Replaces the CPU SDF + upload path with four GPU render passes:
//   1) OccluderMask — rasterize CastsShadow sprites into an R8 mask at SDF resolution.
//   2) JfaInit — seed the JFA with texel coordinates of filled mask pixels.
//   3) JfaStep — log2(N) ping-pong jumps to propagate nearest-seed information.
//   4) JfaToSdf — convert the converged seed map + mask into signed distance (R16F).
//
// The descriptor set bound at set=2 of the deferred lighting pipelines contains the SDF sampler + params UBO.
// Shaders included from shadow_sdf_sampling.glsl read these slots.

/// <summary>SDF-based shadow pipeline (partial).</summary>
/// <remarks>All methods in this partial run on the render thread; not safe to call concurrently with frame recording.</remarks>
public sealed unsafe partial class VulkanRenderer
{
    private Image _imgShadowSdf = default;
    private DeviceMemory _memShadowSdf = default;
    private ImageView _viewShadowSdf = default;
    private uint _imgShadowSdfWidth;
    private uint _imgShadowSdfHeight;
    private bool _imgShadowSdfHasContent;
    private bool _jfaFinalInSeedA;

    private Image _imgShadowOccluderMask = default;
    private DeviceMemory _memShadowOccluderMask = default;
    private ImageView _viewShadowOccluderMask = default;
    private Framebuffer _fbShadowOccluderMask = default;

    private Image _imgShadowJfaSeedA = default;
    private DeviceMemory _memShadowJfaSeedA = default;
    private ImageView _viewShadowJfaSeedA = default;
    private Framebuffer _fbShadowJfaSeedA = default;

    private Image _imgShadowJfaSeedB = default;
    private DeviceMemory _memShadowJfaSeedB = default;
    private ImageView _viewShadowJfaSeedB = default;
    private Framebuffer _fbShadowJfaSeedB = default;

    private Framebuffer _fbShadowSdfFinal = default;

    private DescriptorSet _dsJfaSrcMask = default;
    private DescriptorSet _dsJfaSrcSeedA = default;
    private DescriptorSet _dsJfaSrcSeedB = default;
    private DescriptorSet _dsJfaToSdfSeedA = default;
    private DescriptorSet _dsJfaToSdfSeedB = default;

    private VkBuffer _shadowSdfParamsUbo = default;
    private DeviceMemory _shadowSdfParamsUboMemory = default;
    private void* _shadowSdfParamsUboMapped;

    private DescriptorSetLayout _dslShadowSdf = default;
    private DescriptorSet _dsShadowSdf = default;

    [StructLayout(LayoutKind.Sequential)]
    private struct ShadowSdfParamsGpu
    {
        /// <summary>
        /// <c>.x</c> — reserved (set to 0, not read by <c>shadow_sdf_sampling.glsl</c>).<br/>
        /// <c>.y</c> — reserved (set to 0, not read by <c>shadow_sdf_sampling.glsl</c>).<br/>
        /// <c>.z</c> — <c>sdfScale</c>: SDF texels per swapchain pixel (active).<br/>
        /// <c>.w</c> — <c>kSoft</c>: soft shadow penumbra factor (active).
        /// </summary>
        /// <remarks>
        /// Reserved <c>.xy</c> slots are available for future use (e.g. SDF size, edge-aware sampling)
        /// without requiring a GPU struct layout change or shader recompilation.
        /// </remarks>
        public Vector4D<float> SdfSizeScale_KSoftDepthBias;

        /// <summary>
        /// <c>.x</c> — shadow enabled flag (1.0 or 0.0, active).<br/>
        /// <c>.y</c> — <c>maxSamples</c>: cone-trace sample count (active).<br/>
        /// <c>.z</c> — <c>directionalTraceWorldDist</c>: trace distance in world pixels (active).<br/>
        /// <c>.w</c> — <c>depthBias</c>: self-shadow bias in world pixels (active).
        /// </summary>
        public Vector4D<float> EnabledSamples_DirDistDepthBias;
    }

    /// <summary>Push constants for the shadow occluder mask vertex shader (<c>shadow_occluder.vert.glsl</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct OccluderMaskPush
    {
        /// <summary>.xy viewport size (world px). .z cos(-camRot). .w sin(-camRot).</summary>
        public Vector4D<float> ViewportSize_CameraRotCosSin;
        /// <summary>.xy camera pos world. .zw physical offset (swapchain px).</summary>
        public Vector4D<float> CameraPos_PhysicalOffset;
        /// <summary>.x physical scale. .y sdf scale. .zw screen (sdf px = swapchain*sdfScale).</summary>
        public Vector4D<float> PhysicalScale_SdfScale_Screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeferredLightingPush
    {
        public Vector4D<float> ScreenSizePad;
        public Vector4D<float> CameraPosRot;
        public Vector4D<float> ViewportSizeScale;
        public Vector4D<float> PhysicalRect;
        /// <summary>
        /// .X = shadow enabled (1 or 0). .Y/.Z/.W reserved for future use (e.g. shadow quality, bias overrides).
        /// Push constant layout is ABI-sensitive with the GPU shader — do not shrink.
        /// </summary>
        public Vector4D<float> ShadowSettings;
    }


    // ─── Resource lifecycle ───────────────────────────────────────────────────

    private void CreateShadowSdfTargets()
    {
        var w = System.Math.Min(System.Math.Max(_swapchainExtent.Width, 1u), DeferredRenderingConstants.MaxShadowSdfDim);
        var h = System.Math.Min(System.Math.Max(_swapchainExtent.Height, 1u), DeferredRenderingConstants.MaxShadowSdfDim);
        CreateShadowSdfTargets(w, h);
    }

    private void CreateShadowSdfTargets(uint w, uint h)
    {
        w = System.Math.Min(System.Math.Max(w, 1u), DeferredRenderingConstants.MaxShadowSdfDim);
        h = System.Math.Min(System.Math.Max(h, 1u), DeferredRenderingConstants.MaxShadowSdfDim);

        CreateDeviceLocalImage(w, h, DeferredRenderingConstants.ShadowSdfFormat,
            ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.ColorAttachmentBit,
            out _imgShadowSdf, out _memShadowSdf, out _viewShadowSdf);
        _imgShadowSdfWidth = w;
        _imgShadowSdfHeight = h;
        _imgShadowSdfHasContent = false;

        CreateDeviceLocalImage(w, h, DeferredRenderingConstants.ShadowOccluderMaskFormat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
            out _imgShadowOccluderMask, out _memShadowOccluderMask, out _viewShadowOccluderMask);

        CreateDeviceLocalImage(w, h, DeferredRenderingConstants.ShadowJfaSeedFormat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
            out _imgShadowJfaSeedA, out _memShadowJfaSeedA, out _viewShadowJfaSeedA);
        CreateDeviceLocalImage(w, h, DeferredRenderingConstants.ShadowJfaSeedFormat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
            out _imgShadowJfaSeedB, out _memShadowJfaSeedB, out _viewShadowJfaSeedB);

        CreateShadowSdfFramebuffers(w, h);
    }

    /// <summary>
    /// Ensures shadow SDF images match the desired dimensions. Recreates GPU resources only when the
    /// size actually changes — prevents JFA from sampling undefined regions when <c>SdfScale &lt; 1</c>.
    /// </summary>
    private void EnsureShadowSdfTargetSize(uint desiredW, uint desiredH)
    {
        desiredW = System.Math.Min(System.Math.Max(desiredW, 1u), DeferredRenderingConstants.MaxShadowSdfDim);
        desiredH = System.Math.Min(System.Math.Max(desiredH, 1u), DeferredRenderingConstants.MaxShadowSdfDim);
        if (_imgShadowSdfWidth == desiredW && _imgShadowSdfHeight == desiredH)
            return;
        DestroyShadowSdfTargets();
        CreateShadowSdfTargets(desiredW, desiredH);
        UpdateShadowSdfDescriptorSet();
        UpdateJfaDescriptorSets();
    }

    private void CreateShadowSdfFramebuffers(uint w, uint h)
    {
        fixed (ImageView* pv = &_viewShadowOccluderMask)
        {
            FramebufferCreateInfo f = new() { SType = StructureType.FramebufferCreateInfo, RenderPass = _rpShadowOccluderMask, AttachmentCount = 1, PAttachments = pv, Width = w, Height = h, Layers = 1 };
            if (_vk!.CreateFramebuffer(_device, in f, null, out _fbShadowOccluderMask) != Result.Success)
                throw new GraphicsInitializationException("fb shadow occluder mask failed.");
        }
        fixed (ImageView* pv = &_viewShadowJfaSeedA)
        {
            FramebufferCreateInfo f = new() { SType = StructureType.FramebufferCreateInfo, RenderPass = _rpShadowJfaSeed, AttachmentCount = 1, PAttachments = pv, Width = w, Height = h, Layers = 1 };
            if (_vk.CreateFramebuffer(_device, in f, null, out _fbShadowJfaSeedA) != Result.Success)
                throw new GraphicsInitializationException("fb shadow JFA seed A failed.");
        }
        fixed (ImageView* pv = &_viewShadowJfaSeedB)
        {
            FramebufferCreateInfo f = new() { SType = StructureType.FramebufferCreateInfo, RenderPass = _rpShadowJfaSeed, AttachmentCount = 1, PAttachments = pv, Width = w, Height = h, Layers = 1 };
            if (_vk.CreateFramebuffer(_device, in f, null, out _fbShadowJfaSeedB) != Result.Success)
                throw new GraphicsInitializationException("fb shadow JFA seed B failed.");
        }
        fixed (ImageView* pv = &_viewShadowSdf)
        {
            FramebufferCreateInfo f = new() { SType = StructureType.FramebufferCreateInfo, RenderPass = _rpShadowSdfFinal, AttachmentCount = 1, PAttachments = pv, Width = w, Height = h, Layers = 1 };
            if (_vk.CreateFramebuffer(_device, in f, null, out _fbShadowSdfFinal) != Result.Success)
                throw new GraphicsInitializationException("fb shadow SDF final failed.");
        }
    }

    private void EnsureShadowSdfParamsUbo()
    {
        if (_shadowSdfParamsUbo.Handle != default) return;
        var bytes = (ulong)sizeof(ShadowSdfParamsGpu);
        CreateHostVisibleBuffer(bytes, BufferUsageFlags.UniformBufferBit, out _shadowSdfParamsUbo, out _shadowSdfParamsUboMemory);
        void* p;
        if (_vk!.MapMemory(_device, _shadowSdfParamsUboMemory, 0, bytes, 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map shadow sdf params ubo");
        _shadowSdfParamsUboMapped = p;
    }

    private void CreateShadowSdfDescriptorLayout()
    {
        Span<DescriptorSetLayoutBinding> bindings = stackalloc DescriptorSetLayoutBinding[2];
        bindings[0] = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        bindings[1] = new DescriptorSetLayoutBinding { Binding = 1, DescriptorType = DescriptorType.UniformBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, bindings, out _dslShadowSdf, "dsl shadow sdf failed");
    }

    private void AllocateShadowSdfDescriptorSet()
    {
        fixed (DescriptorSetLayout* dsl = &_dslShadowSdf)
        {
            DescriptorSetAllocateInfo ai = new() { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = 1, PSetLayouts = dsl };
            if (_vk!.AllocateDescriptorSets(_device, in ai, out _dsShadowSdf) != Result.Success)
                throw new GraphicsInitializationException("alloc ds shadow sdf");
        }
        UpdateShadowSdfDescriptorSet();
    }

    private void UpdateShadowSdfDescriptorSet()
    {
        if (_viewShadowSdf.Handle == default || _shadowSdfParamsUbo.Handle == default) return;
        DescriptorImageInfo img = new() { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _viewShadowSdf, Sampler = _samplerLinear };
        DescriptorBufferInfo buf = new() { Buffer = _shadowSdfParamsUbo, Offset = 0, Range = (ulong)sizeof(ShadowSdfParamsGpu) };
        var writes = stackalloc WriteDescriptorSet[2];
        writes[0] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsShadowSdf, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &img };
        writes[1] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsShadowSdf, DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.UniformBuffer, PBufferInfo = &buf };
        _vk!.UpdateDescriptorSets(_device, 2, writes, 0, null);
    }

    private void DestroyShadowSdfTargets()
    {
        DestroyShadowSdfFramebuffers();
        DestroyImageViewMemory(ref _viewShadowSdf, ref _imgShadowSdf, ref _memShadowSdf);
        DestroyImageViewMemory(ref _viewShadowOccluderMask, ref _imgShadowOccluderMask, ref _memShadowOccluderMask);
        DestroyImageViewMemory(ref _viewShadowJfaSeedA, ref _imgShadowJfaSeedA, ref _memShadowJfaSeedA);
        DestroyImageViewMemory(ref _viewShadowJfaSeedB, ref _imgShadowJfaSeedB, ref _memShadowJfaSeedB);
        _imgShadowSdfHasContent = false;
    }

    private void DestroyShadowSdfFramebuffers()
    {
        if (_fbShadowOccluderMask.Handle != default) { _vk!.DestroyFramebuffer(_device, _fbShadowOccluderMask, null); _fbShadowOccluderMask = default; }
        if (_fbShadowJfaSeedA.Handle != default) { _vk!.DestroyFramebuffer(_device, _fbShadowJfaSeedA, null); _fbShadowJfaSeedA = default; }
        if (_fbShadowJfaSeedB.Handle != default) { _vk!.DestroyFramebuffer(_device, _fbShadowJfaSeedB, null); _fbShadowJfaSeedB = default; }
        if (_fbShadowSdfFinal.Handle != default) { _vk!.DestroyFramebuffer(_device, _fbShadowSdfFinal, null); _fbShadowSdfFinal = default; }
    }

    private void DestroyImageViewMemory(ref ImageView view, ref Image img, ref DeviceMemory mem)
    {
        if (view.Handle != default) { _vk!.DestroyImageView(_device, view, null); view = default; }
        if (img.Handle != default) { _vk!.DestroyImage(_device, img, null); img = default; }
        if (mem.Handle != default) { _vk!.FreeMemory(_device, mem, null); mem = default; }
    }

    private void DestroyShadowSdfParamsUbo()
    {
        if (_shadowSdfParamsUboMemory.Handle != default && _shadowSdfParamsUboMapped != null) { _vk!.UnmapMemory(_device, _shadowSdfParamsUboMemory); _shadowSdfParamsUboMapped = null; }
        if (_shadowSdfParamsUbo.Handle != default) { _vk!.DestroyBuffer(_device, _shadowSdfParamsUbo, null); _shadowSdfParamsUbo = default; }
        if (_shadowSdfParamsUboMemory.Handle != default) { _vk!.FreeMemory(_device, _shadowSdfParamsUboMemory, null); _shadowSdfParamsUboMemory = default; }
    }

    private void DestroyShadowSdfDescriptorLayout()
    {
        if (_dslShadowSdf.Handle != default) { _vk!.DestroyDescriptorSetLayout(_device, _dslShadowSdf, null); _dslShadowSdf = default; }
    }

    // ─── JFA descriptor layout and sets ───────────────────────────────────────

    private void CreateJfaSrcDescriptorLayout()
    {
        Span<DescriptorSetLayoutBinding> bindings = stackalloc DescriptorSetLayoutBinding[1];
        bindings[0] = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(_vk!, _device, bindings, out _dslJfaSrc, "dsl jfa src failed");
    }

    private void AllocateJfaDescriptorSets()
    {
        AllocateJfaSingleSamplerDs(out _dsJfaSrcMask);
        AllocateJfaSingleSamplerDs(out _dsJfaSrcSeedA);
        AllocateJfaSingleSamplerDs(out _dsJfaSrcSeedB);
        AllocateJfaToSdfDs(out _dsJfaToSdfSeedA);
        AllocateJfaToSdfDs(out _dsJfaToSdfSeedB);
        UpdateJfaDescriptorSets();
    }

    private void AllocateJfaSingleSamplerDs(out DescriptorSet ds)
    {
        fixed (DescriptorSetLayout* dsl = &_dslJfaSrc)
        {
            DescriptorSetAllocateInfo ai = new() { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = 1, PSetLayouts = dsl };
            if (_vk!.AllocateDescriptorSets(_device, in ai, out ds) != Result.Success)
                throw new GraphicsInitializationException("alloc ds jfa src");
        }
    }

    private void AllocateJfaToSdfDs(out DescriptorSet ds)
    {
        // jfa_to_sdf needs 2 CIS bindings (seedFinal + occluderMask). Reuse _dslBloomDual which is 2 CIS at binding 0,1.
        fixed (DescriptorSetLayout* dsl = &_dslBloomDual)
        {
            DescriptorSetAllocateInfo ai = new() { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = 1, PSetLayouts = dsl };
            if (_vk!.AllocateDescriptorSets(_device, in ai, out ds) != Result.Success)
                throw new GraphicsInitializationException("alloc ds jfa to sdf");
        }
    }

    private void UpdateJfaDescriptorSets()
    {
        if (_viewShadowOccluderMask.Handle == default) return;
        DescriptorImageInfo maskInfo = new() { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _viewShadowOccluderMask, Sampler = _samplerLinear };
        DescriptorImageInfo seedAInfo = new() { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _viewShadowJfaSeedA, Sampler = _samplerNearest };
        DescriptorImageInfo seedBInfo = new() { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _viewShadowJfaSeedB, Sampler = _samplerNearest };
        var writes = stackalloc WriteDescriptorSet[7];
        writes[0] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsJfaSrcMask, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &maskInfo };
        writes[1] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsJfaSrcSeedA, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &seedAInfo };
        writes[2] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsJfaSrcSeedB, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &seedBInfo };
        writes[3] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsJfaToSdfSeedA, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &seedAInfo };
        writes[4] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsJfaToSdfSeedA, DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &maskInfo };
        writes[5] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsJfaToSdfSeedB, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &seedBInfo };
        writes[6] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsJfaToSdfSeedB, DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &maskInfo };
        _vk!.UpdateDescriptorSets(_device, 7, writes, 0, null);
    }

    // ─── GPU recording ────────────────────────────────────────────────────────

    private void RecordShadowSdfPass(CommandBuffer cmd, in FramePlan framePlan, int[] sortIdx, SpriteDrawRequest[] sprites, int nSprite)
    {
        var shadowSettings = framePlan.ResolvedPost.Shadows;
        if (!shadowSettings.Enabled)
        {
            UpdateShadowSdfParamsUbo(in framePlan, in shadowSettings, Vector2D<int>.Zero);
            EnsureShadowSdfFullyLit(cmd);
            return;
        }

        BeginGpuLabel(cmd, "Pass.ShadowSdf");
        try
        {
            var cam = framePlan.ShadowCamera;
            var sdfSize = cam.SdfSizePx;
            if (sdfSize.X <= 0 || sdfSize.Y <= 0)
            {
                UpdateShadowSdfParamsUbo(in framePlan, in shadowSettings, Vector2D<int>.Zero);
                EnsureShadowSdfFullyLit(cmd);
                return;
            }

            // Images are sized to exactly SdfSizePx so the JFA fullscreen triangle covers the
            // entire texture (no undefined regions). Recreate only when dimensions change.
            EnsureShadowSdfTargetSize((uint)sdfSize.X, (uint)sdfSize.Y);

            // Use the actual allocated dimensions (MaxShadowSdfDim may clamp).
            var sdfW = _imgShadowSdfWidth;
            var sdfH = _imgShadowSdfHeight;
            sdfSize = new Vector2D<int>((int)sdfW, (int)sdfH);
            UpdateShadowSdfParamsUbo(in framePlan, in shadowSettings, sdfSize);

            // Single-pass occluder scan: RecordOccluderMaskPass uploads instance data and
            // issues draws in one scan, returning false when zero occluders exist so we
            // can skip the render pass and all JFA passes entirely.
            bool hasOccluders;
            {
#if DEBUG
                using var __occMask = FrameProfilerScope.Enter("Record.OccluderMask");
#endif
                hasOccluders = RecordOccluderMaskPass(cmd, in framePlan, sortIdx, sprites, nSprite, sdfW, sdfH, cam.SdfScale);
            }
            if (!hasOccluders)
            {
                EnsureShadowSdfFullyLit(cmd);
                return;
            }
            {
#if DEBUG
                using var __jfaInit = FrameProfilerScope.Enter("Record.JfaInit");
#endif
                RecordJfaInitPass(cmd, sdfW, sdfH);
            }
            {
#if DEBUG
                using var __jfaSteps = FrameProfilerScope.Enter("Record.JfaSteps");
#endif
                RecordJfaStepPasses(cmd, sdfW, sdfH);
            }
            {
#if DEBUG
                using var __jfaToSdf = FrameProfilerScope.Enter("Record.JfaToSdf");
#endif
                RecordJfaToSdfPass(cmd, sdfW, sdfH);
            }
            _imgShadowSdfHasContent = true;
        }
        finally { EndGpuLabel(cmd); }
    }

    /// <returns><see langword="true"/> if at least one occluder was drawn;
    /// <see langword="false"/> when zero occluders exist and the render pass was skipped.</returns>
    private bool RecordOccluderMaskPass(CommandBuffer cmd, in FramePlan framePlan, int[] sortIdx, SpriteDrawRequest[] sprites, int nSprite, uint sdfW, uint sdfH, float sdfScale)
    {
        BeginGpuLabel(cmd, "ShadowSdf.OccluderMask");
        try
        {
            if (nSprite == 0) return false;

            // Single scan: upload occluder instance data and build the draw-order
            // array in one pass, using a pessimistic rent so no pre-count is needed.
            var shadowBase = nSprite * 3;
            var upload = new Span<SpriteInstanceGpu>((SpriteInstanceGpu*)_spriteInstanceBufferMapped! + shadowBase, nSprite);
            var valid = 0;
            var rented = ArrayPool<int>.Shared.Rent(nSprite);
            try
            {
                for (var si = 0; si < nSprite; si++)
                {
                    var idx = sortIdx[si];
                    ref readonly var s = ref sprites[idx];
                    if (s.Space != Scene.CoordinateSpace.WorldSpace || s.Transparent || !s.CastsShadow) continue;
                    if (TryGetTextureSlot(s.AlbedoTextureId) is null) continue;
                    var spriteUvRect = s.UvRect;
                    if (spriteUvRect.X == 0f && spriteUvRect.Y == 0f && spriteUvRect.Z == 0f && spriteUvRect.W == 0f) spriteUvRect = new Vector4D<float>(0f, 0f, 1f, 1f);
                    upload[valid] = new SpriteInstanceGpu
                    {
                        CenterHalfPx = new Vector4D<float>(s.CenterWorld.X, s.CenterWorld.Y, s.HalfExtentsWorld.X, s.HalfExtentsWorld.Y),
                        UvRect = spriteUvRect,
                        ColorAlpha = new Vector4D<float>(1f, 1f, 1f, s.Alpha),
                        EmissiveRgbIntensity = new Vector4D<float>(0f, 0f, 0f, 0f),
                        RotAndFlags = new Vector4D<float>(0f, 0f, s.RotationRadians, 0f)
                    };
                    rented[valid++] = idx;
                }
                if (valid == 0) return false;

                // Occluder mask writes a constant 1.0 — order is irrelevant, so sorting by texture ID
                // maximizes draw-call batching (contiguous same-texture runs).
                Array.Sort(rented, 0, valid, Comparer<int>.Create((a, b) =>
                    sprites[a].AlbedoTextureId.CompareTo(sprites[b].AlbedoTextureId)));

                // Occluders exist — start the render pass and issue batched draws.
                ClearValue cv = new() { Color = new ClearColorValue { Float32_0 = 0f } };
                Rect2D area = new() { Offset = default, Extent = new Extent2D { Width = sdfW, Height = sdfH } };
                RenderPassBeginInfo rpbi = new() { SType = StructureType.RenderPassBeginInfo, RenderPass = _rpShadowOccluderMask, Framebuffer = _fbShadowOccluderMask, RenderArea = area, ClearValueCount = 1, PClearValues = &cv };
                _vk!.CmdBeginRenderPass(cmd, &rpbi, SubpassContents.Inline);

                Viewport vp = new() { X = 0f, Y = 0f, Width = sdfW, Height = sdfH, MinDepth = 0f, MaxDepth = 1f };
                _vk.CmdSetViewport(cmd, 0, 1, &vp);
                _vk.CmdSetScissor(cmd, 0, 1, &area);
                _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeShadowOccluder);

                var camRotCos = MathF.Cos(-framePlan.Camera.RotationRadians);
                var camRotSin = MathF.Sin(-framePlan.Camera.RotationRadians);
                var push = new OccluderMaskPush
                {
                    ViewportSize_CameraRotCosSin = new Vector4D<float>(
                        framePlan.Camera.ViewportSizeWorld.X, framePlan.Camera.ViewportSizeWorld.Y,
                        camRotCos, camRotSin),
                    CameraPos_PhysicalOffset = new Vector4D<float>(
                        framePlan.Camera.PositionWorld.X, framePlan.Camera.PositionWorld.Y,
                        framePlan.Physical.OffsetPixels.X, framePlan.Physical.OffsetPixels.Y),
                    PhysicalScale_SdfScale_Screen = new Vector4D<float>(
                        framePlan.Physical.Scale, sdfScale, sdfW, sdfH),
                };
                _vk.CmdPushConstants(cmd, _plShadowOccluder, ShaderStageFlags.VertexBit, 0,
                    (uint)sizeof(OccluderMaskPush), System.Runtime.CompilerServices.Unsafe.AsPointer(ref push));

                var vb = stackalloc[] { _vertexBuffer };
                var off = stackalloc ulong[] { 0 };
                _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
                _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

                var instBind = stackalloc[] { _spriteInstanceBuffer };
                var instOff = stackalloc ulong[] { 0 };
                _vk.CmdBindVertexBuffers(cmd, 1, 1, instBind, instOff);

                var first = 0;
                var setsOcc = stackalloc DescriptorSet[1];
                while (first < valid)
                {
                    ref readonly var startS = ref sprites[rented[first]];
                    var al = TryGetTextureSlot(startS.AlbedoTextureId);
                    if (al is null) { first++; continue; }
                    var run = 1;
                    while (first + run < valid)
                    {
                        ref readonly var n = ref sprites[rented[first + run]];
                        if (n.AlbedoTextureId != startS.AlbedoTextureId) break;
                        run++;
                    }
                    setsOcc[0] = al.DescriptorSet;
                    _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plShadowOccluder, 0, 1, setsOcc, 0, null);
                    _vk.CmdDrawIndexed(cmd, 6, (uint)run, 0, 0, (uint)(shadowBase + first));
                    first += run;
                }

                _vk.CmdEndRenderPass(cmd);
                return true;
            }
            finally { ArrayPool<int>.Shared.Return(rented); }
        }
        finally { EndGpuLabel(cmd); }
    }

    private void RecordJfaInitPass(CommandBuffer cmd, uint sdfW, uint sdfH)
    {
        BeginGpuLabel(cmd, "ShadowSdf.JfaInit");
        try
        {
            ClearValue cv = new() { Color = new ClearColorValue { Float32_0 = -1f, Float32_1 = -1f } };
            Rect2D area = new() { Offset = default, Extent = new Extent2D { Width = sdfW, Height = sdfH } };
            RenderPassBeginInfo rpbi = new() { SType = StructureType.RenderPassBeginInfo, RenderPass = _rpShadowJfaSeed, Framebuffer = _fbShadowJfaSeedA, RenderArea = area, ClearValueCount = 1, PClearValues = &cv };
            _vk!.CmdBeginRenderPass(cmd, &rpbi, SubpassContents.Inline);
            Viewport vp = new() { X = 0f, Y = 0f, Width = sdfW, Height = sdfH, MinDepth = 0f, MaxDepth = 1f };
            _vk.CmdSetViewport(cmd, 0, 1, &vp);
            _vk.CmdSetScissor(cmd, 0, 1, &area);
            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeJfaInit);
            fixed (DescriptorSet* ds = &_dsJfaSrcMask)
                _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plJfaInit, 0, 1, ds, 0, null);
            var p = stackalloc float[4];
            p[0] = sdfW; p[1] = sdfH; p[2] = 1f / MathF.Max(sdfW, 1f); p[3] = 1f / MathF.Max(sdfH, 1f);
            _vk.CmdPushConstants(cmd, _plJfaInit, ShaderStageFlags.FragmentBit, 0, 16, p);
            _vk.CmdDraw(cmd, 3, 1, 0, 0);
            _vk.CmdEndRenderPass(cmd);
        }
        finally { EndGpuLabel(cmd); }
    }

    private void RecordJfaStepPasses(CommandBuffer cmd, uint sdfW, uint sdfH)
    {
        BeginGpuLabel(cmd, "ShadowSdf.JfaSteps");
        try
        {
            var maxDim = System.Math.Max(sdfW, sdfH);
            var step = 1; while (step < maxDim) step <<= 1; step >>= 1;
            var readA = true;
            Rect2D area = new() { Offset = default, Extent = new Extent2D { Width = sdfW, Height = sdfH } };
            Viewport vp = new() { X = 0f, Y = 0f, Width = sdfW, Height = sdfH, MinDepth = 0f, MaxDepth = 1f };
            ClearValue cv = new() { Color = new ClearColorValue { Float32_0 = -1f, Float32_1 = -1f } };
            var p = stackalloc float[4];
            while (step >= 1)
            {
                var srcDs = readA ? _dsJfaSrcSeedA : _dsJfaSrcSeedB;
                var dstFb = readA ? _fbShadowJfaSeedB : _fbShadowJfaSeedA;
                RenderPassBeginInfo rpbi = new() { SType = StructureType.RenderPassBeginInfo, RenderPass = _rpShadowJfaSeed, Framebuffer = dstFb, RenderArea = area, ClearValueCount = 1, PClearValues = &cv };
                _vk!.CmdBeginRenderPass(cmd, &rpbi, SubpassContents.Inline);
                _vk.CmdSetViewport(cmd, 0, 1, &vp);
                _vk.CmdSetScissor(cmd, 0, 1, &area);
                _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeJfaStep);
                _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plJfaStep, 0, 1, &srcDs, 0, null);
                p[0] = sdfW; p[1] = sdfH; p[2] = step; p[3] = 0f;
                _vk.CmdPushConstants(cmd, _plJfaStep, ShaderStageFlags.FragmentBit, 0, 16, p);
                _vk.CmdDraw(cmd, 3, 1, 0, 0);
                _vk.CmdEndRenderPass(cmd);
                readA = !readA;
                step >>= 1;
            }
            _jfaFinalInSeedA = readA;
        }
        finally { EndGpuLabel(cmd); }
    }

    private void RecordJfaToSdfPass(CommandBuffer cmd, uint sdfW, uint sdfH)
    {
        BeginGpuLabel(cmd, "ShadowSdf.JfaToSdf");
        try
        {
            // Transition SDF image so the render pass Load op is valid.
            var oldLayout = _imgShadowSdfHasContent ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined;
            var srcStage = _imgShadowSdfHasContent ? PipelineStageFlags.FragmentShaderBit : PipelineStageFlags.TopOfPipeBit;
            var srcAccess = _imgShadowSdfHasContent ? AccessFlags.ShaderReadBit : AccessFlags.None;
            ImageMemoryBarrier bar = new()
            {
                SType = StructureType.ImageMemoryBarrier, OldLayout = oldLayout, NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored, Image = _imgShadowSdf,
                SubresourceRange = new ImageSubresourceRange { AspectMask = ImageAspectFlags.ColorBit, BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1 },
                SrcAccessMask = srcAccess, DstAccessMask = AccessFlags.ColorAttachmentWriteBit
            };
            _vk!.CmdPipelineBarrier(cmd, srcStage, PipelineStageFlags.ColorAttachmentOutputBit, 0, 0, null, 0, null, 1, &bar);

            Rect2D area = new() { Offset = default, Extent = new Extent2D { Width = sdfW, Height = sdfH } };
            Viewport vp = new() { X = 0f, Y = 0f, Width = sdfW, Height = sdfH, MinDepth = 0f, MaxDepth = 1f };
            RenderPassBeginInfo rpbi = new() { SType = StructureType.RenderPassBeginInfo, RenderPass = _rpShadowSdfFinal, Framebuffer = _fbShadowSdfFinal, RenderArea = area, ClearValueCount = 0, PClearValues = null };
            _vk.CmdBeginRenderPass(cmd, &rpbi, SubpassContents.Inline);
            _vk.CmdSetViewport(cmd, 0, 1, &vp);
            _vk.CmdSetScissor(cmd, 0, 1, &area);
            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeJfaToSdf);

            var dsFinal = _jfaFinalInSeedA ? _dsJfaToSdfSeedA : _dsJfaToSdfSeedB;
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plJfaToSdf, 0, 1, &dsFinal, 0, null);

            var p = stackalloc float[4]; p[0] = sdfW; p[1] = sdfH; p[2] = 0f; p[3] = 0f;
            _vk.CmdPushConstants(cmd, _plJfaToSdf, ShaderStageFlags.FragmentBit, 0, 16, p);
            _vk.CmdDraw(cmd, 3, 1, 0, 0);
            _vk.CmdEndRenderPass(cmd);
        }
        finally { EndGpuLabel(cmd); }
    }

    private void EnsureShadowSdfFullyLit(CommandBuffer cmd)
    {
        if (_imgShadowSdfHasContent || _imgShadowSdf.Handle == default) return;
        ImageMemoryBarrier toTransfer = new()
        {
            SType = StructureType.ImageMemoryBarrier, OldLayout = ImageLayout.Undefined, NewLayout = ImageLayout.TransferDstOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored, Image = _imgShadowSdf,
            SubresourceRange = new ImageSubresourceRange { AspectMask = ImageAspectFlags.ColorBit, BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1 },
            SrcAccessMask = AccessFlags.None, DstAccessMask = AccessFlags.TransferWriteBit
        };
        _vk!.CmdPipelineBarrier(cmd, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &toTransfer);
        var clearValue = new ClearColorValue { Float32_0 = DeferredRenderingConstants.ShadowSdfFullyLitSentinelTexels };
        var range = new ImageSubresourceRange { AspectMask = ImageAspectFlags.ColorBit, BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1 };
        _vk.CmdClearColorImage(cmd, _imgShadowSdf, ImageLayout.TransferDstOptimal, &clearValue, 1, &range);
        ImageMemoryBarrier toSample = toTransfer;
        toSample.OldLayout = ImageLayout.TransferDstOptimal; toSample.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        toSample.SrcAccessMask = AccessFlags.TransferWriteBit; toSample.DstAccessMask = AccessFlags.ShaderReadBit;
        _vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &toSample);
        _imgShadowSdfHasContent = true;
    }

    private void UpdateShadowSdfParamsUbo(in FramePlan framePlan, in ShadowSettings shadowSettings, Vector2D<int> rectifiedSdfSize)
    {
        if (_shadowSdfParamsUboMapped == null) return;
        var sdfScale = shadowSettings.SdfScale <= 0f ? 1f : shadowSettings.SdfScale;
        var enabled = shadowSettings.Enabled && rectifiedSdfSize.X > 0 && rectifiedSdfSize.Y > 0;
        var directionalTraceWorldDist = shadowSettings.DirectionalTraceWorldDist;
        if (directionalTraceWorldDist <= 0f)
        {
            var diag = MathF.Sqrt(framePlan.Camera.ViewportSizeWorld.X * framePlan.Camera.ViewportSizeWorld.X + framePlan.Camera.ViewportSizeWorld.Y * framePlan.Camera.ViewportSizeWorld.Y);
            directionalTraceWorldDist = diag;
        }
        var samples = MathF.Min(MathF.Max(1f, shadowSettings.ConeTraceSamples), DeferredRenderingConstants.MaxConeTraceSamples);
        // SdfSizeScale_KSoftDepthBias.xy are reserved (zeroed). Only .z (sdfScale) and .w (kSoft)
        // are read by shadow_sdf_sampling.glsl. EnabledSamples_DirDistDepthBias: all four components active.
        var ubo = new ShadowSdfParamsGpu
        {
            SdfSizeScale_KSoftDepthBias = new Vector4D<float>(0f, 0f, sdfScale, shadowSettings.SoftShadowK),
            EnabledSamples_DirDistDepthBias = new Vector4D<float>(enabled ? 1f : 0f, samples, directionalTraceWorldDist, shadowSettings.DepthBias)
        };
        System.Runtime.CompilerServices.Unsafe.Write(_shadowSdfParamsUboMapped, ubo);
    }
}
