using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Glslang.NET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

/// <summary>HDR: emissive prepass, deferred G-buffer + lighting, WBOIT transparency, bloom, composite (scene-linear).</summary>
public sealed unsafe partial class VulkanRenderer
{
    private const Format HdrFormat = Format.R16G16B16A16Sfloat;
    private const Format WboitRevealFormat = Format.R16Sfloat;
    private const int BloomDownsampleLevels = 2;
    private const int BloomBlurPingPongs = 4;
    private const int MaxPointLights = 256;

    /// <summary>Offscreen color passes: first write after image (re)alloc uses Undefined; subsequent passes must use ShaderReadOnly initial layout.</summary>
    private RenderPass _rpOffscreenInitialUndefined = default;

    private RenderPass _rpOffscreenInitialShaderRead = default;
    private RenderPass _rpComposite = default;
    private RenderPass _rpGbufferUndefined = default;
    private RenderPass _rpGbufferShaderRead = default;
    private RenderPass _rpWboitUndefined = default;
    private RenderPass _rpWboitShaderRead = default;

    /// <summary>Since last image (re)alloc, this attachment has been rendered to at least once (layout is ShaderReadOnly until next Begin).</summary>
    private bool _offsWrittenEmissive;

    private bool _offsWrittenHdr;
    private bool _offsWrittenGbuffer;
    private bool _offsWrittenWboit;
    private bool _offsWrittenHdrComposite;
    private bool _offsWrittenBloom0;
    private bool _offsWrittenBloom1;
    private readonly bool[] _offsWrittenBloomDown = new bool[BloomDownsampleLevels];

    private Image _imgEmissive = default;
    private DeviceMemory _memEmissive = default;
    private ImageView _viewEmissive = default;
    private Framebuffer _fbEmissive = default;

    private Image _imgHdr = default;
    private DeviceMemory _memHdr = default;
    private ImageView _viewHdr = default;
    private Framebuffer _fbHdr = default;

    private Image _imgGbuf0 = default;
    private DeviceMemory _memGbuf0 = default;
    private ImageView _viewGbuf0 = default;
    private Image _imgGbuf1 = default;
    private DeviceMemory _memGbuf1 = default;
    private ImageView _viewGbuf1 = default;
    private Framebuffer _fbGbuffer = default;

    private Image _imgWAccum = default;
    private DeviceMemory _memWAccum = default;
    private ImageView _viewWAccum = default;
    private Image _imgWReveal = default;
    private DeviceMemory _memWReveal = default;
    private ImageView _viewWReveal = default;
    private Framebuffer _fbWboit = default;

    private Image _imgHdrComposite = default;
    private DeviceMemory _memHdrComposite = default;
    private ImageView _viewHdrComposite = default;
    private Framebuffer _fbHdrComposite = default;

    private VkBuffer _pointLightSsbo = default;
    private DeviceMemory _pointLightSsboMemory = default;

    private Image _imgBloom0 = default;
    private DeviceMemory _memBloom0 = default;
    private ImageView _viewBloom0 = default;
    private Framebuffer _fbBloom0 = default;

    private Image _imgBloom1 = default;
    private DeviceMemory _memBloom1 = default;
    private ImageView _viewBloom1 = default;
    private Framebuffer _fbBloom1 = default;

    private uint _bloomHalfW;
    private uint _bloomHalfH;
    private readonly uint[] _bloomDownW = new uint[BloomDownsampleLevels];
    private readonly uint[] _bloomDownH = new uint[BloomDownsampleLevels];
    private readonly Image[] _imgBloomDown = new Image[BloomDownsampleLevels];
    private readonly DeviceMemory[] _memBloomDown = new DeviceMemory[BloomDownsampleLevels];
    private readonly ImageView[] _viewBloomDown = new ImageView[BloomDownsampleLevels];
    private readonly Framebuffer[] _fbBloomDown = new Framebuffer[BloomDownsampleLevels];

    private Sampler _samplerLinear = default;

    private DescriptorPool _descriptorPool = default;
    private DescriptorSetLayout _dslTexture = default;
    private DescriptorSetLayout _dslComposite = default;
    private DescriptorSetLayout _dslBloomExtract = default;
    /// <summary>Two combined samplers (coarse + fine) for dual-filter bloom upsample.</summary>
    private DescriptorSetLayout _dslBloomDual = default;
    private DescriptorSetLayout _dslEmissiveScene = default;
    private DescriptorSetLayout _dslLighting = default;
    private DescriptorSetLayout _dslGbufferRead = default;
    private DescriptorSetLayout _dslPointSsbo = default;
    private DescriptorSetLayout _dslTransparentResolve = default;

    private DescriptorSet _dsEmissiveScene = default;
    private DescriptorSet _dsLighting = default;
    private DescriptorSet _dsBloomExtract = default;
    /// <summary>Coarse + fine pyramid views for dual-filter bloom upsample (dual-sampler layout).</summary>
    private DescriptorSet _dsBloomUpsample = default;
    /// <summary>Static bindings for Gaussian pass: sample half-res bloom0 / bloom1 without mid-buffer descriptor overwrites.</summary>
    private DescriptorSet _dsBloomGaussianSrcBloom0 = default;

    private DescriptorSet _dsBloomGaussianSrcBloom1 = default;
    private DescriptorSet _dsGbufferRead = default;
    private DescriptorSet _dsPointSsbo = default;
    private DescriptorSet _dsTransparentResolve = default;
    private DescriptorSet _dsHdrOpaqueForTransparent = default;
    // Pyramid level descriptors: [0]=half-res bloom0, [1..N]=bloomDown[i-1]
    private readonly DescriptorSet[] _dsBloomDownSrc = new DescriptorSet[BloomDownsampleLevels + 1];

    private PipelineLayout _plSpriteEmissive = default;
    private PipelineLayout _plComposite = default;
    private PipelineLayout _plBloomExtract = default;
    private PipelineLayout _plBloomDownsample = default;
    private PipelineLayout _plBloomGaussian = default;
    private PipelineLayout _plBloomUpsample = default;
    private PipelineLayout _plBloomCopy = default;
    private PipelineLayout _plDeferredBase = default;
    private PipelineLayout _plDeferredPoint = default;
    private PipelineLayout _plDeferredBleed = default;
    private PipelineLayout _plTransparentResolve = default;

    private ShaderModule _modVertSprite = default;
    private ShaderModule _modFragEmissive = default;
    private ShaderModule _modFragGbuffer = default;
    private ShaderModule _modFragDeferredBase = default;
    private ShaderModule _modVertDeferredPoint = default;
    private ShaderModule _modFragDeferredPoint = default;
    private ShaderModule _modFragDeferredBleed = default;
    private ShaderModule _modFragTransparentWboit = default;
    private ShaderModule _modFragTransparentResolve = default;
    private ShaderModule _modVertComposite = default;
    private ShaderModule _modFragComposite = default;
    private ShaderModule _modFragBloomExtract = default;
    private ShaderModule _modFragBloomDownsample = default;
    private ShaderModule _modFragBloomGaussian = default;
    private ShaderModule _modFragBloomUpsample = default;
    private ShaderModule _modFragBloomCopy = default;

    private Pipeline _pipeEmissive = default;
    private Pipeline _pipeSpriteGbuffer = default;
    private Pipeline _pipeDeferredBase = default;
    private Pipeline _pipeDeferredPoint = default;
    private Pipeline _pipeDeferredBleed = default;
    private Pipeline _pipeTransparentWboit = default;
    private Pipeline _pipeTransparentResolve = default;
    private Pipeline _pipeComposite = default;
    private Pipeline _pipeBloomExtract = default;
    private Pipeline _pipeBloomDownsample = default;
    private Pipeline _pipeBloomGaussian = default;
    private Pipeline _pipeBloomUpsample = default;
    private Pipeline _pipeBloomCopy = default;
    private BloomPipeline? _bloomPipeline;
    private DescriptorManager? _descriptorManager;
    private OffscreenTargets? _offscreenTargets;
    private PipelineFactory? _pipelineFactory;
    private TextureUpload? _textureUpload;
    private RenderFrameRecorder? _renderFrameRecorder;
    private IFramePlanBuilder? _framePlanBuilder;
    private IRenderBackendExecutor? _renderBackendExecutor;
    private PostProcessGraph? _postProcessGraph;
    private VkBuffer _lightingBuffer = default;
    private DeviceMemory _lightingBufferMemory = default;

    /// <summary>One composite descriptor set per in-flight frame so updating bloom binding cannot race overlapping GPU work.</summary>
    private DescriptorSet[] _dsCompositeSlots = new DescriptorSet[MaxFramesInFlight];

    [StructLayout(LayoutKind.Sequential)]
    private struct SpritePushData
    {
        public Vector4D<float> CenterHalfPx;
        public Vector4D<float> UvRect;
        public Vector4D<float> ColorAlpha;
        public Vector4D<float> EmissiveRgbIntensity;
        /// <summary>XY = framebuffer size, Z = rotation radians, W unused.</summary>
        public Vector4D<float> ScreenRot;
        public int Mode;
        /// <summary>Emissive prepass: 1 when <see cref="SpriteDrawRequest.EmissiveTextureId"/> is bound.</summary>
        public int UseEmissiveMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositePush
    {
        public float Bloom;
        public float Exposure;
        public float Saturation;
        public float EmissiveHdrGain;
        public float EmissiveBloomGain;
        /// <summary>1 = apply pow(1/2.2) for UNORM swapchain; 0 = linear out (sRGB swapchain encodes on write).</summary>
        public float ApplyManualDisplayGamma;
        public float Pad1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BloomExtractPush
    {
        public float Threshold;
        public float Knee;
        public float EmissiveBloomGain;
        public float Pad0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BloomGaussianPush
    {
        public float DirX;
        public float DirY;
        public float RadiusScale;
        public float Pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BloomResamplePush
    {
        public float SrcW;
        public float SrcH;
        public float DstW;
        public float DstH;
        /// <summary>1 = dual-filter upsample (tent + fine mip); 0 = final half-res pass (coarse tent only).</summary>
        public float FineBlend;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LightingUbo
    {
        public Vector4D<float> Ambient;
        public Vector4D<float> DirectionalDirIntensity;
        public Vector4D<float> DirectionalColor;
        public Vector4D<float> PointPosRadius;
        public Vector4D<float> PointColorIntensity;
        public Vector4D<float> PointFalloff;
        public Vector4D<float> SpotPosRadius;
        public Vector4D<float> SpotDirCosOuter;
        public Vector4D<float> SpotColorIntensity;
    }

    private void CreateGraphicsPipelineAndSurfaces()
    {
        CreateLinearSampler();
        CreateOffscreenRenderPasses();
        CreateGbufferAndWboitRenderPasses();
        CreateCompositeRenderPass();
        CreateOffscreenImagesAndFramebuffers();
        CreateSwapchainFramebuffers();
        CreateDescriptorLayoutsAndPool();
        _pipelineFactory ??= new PipelineFactory(this);
        _pipelineFactory.CreateAllPipelines();
        AllocateCompositeDescriptorSet();
        AllocateBloomDescriptorSets();
        AllocateEmissiveSceneDescriptorSet();
        AllocateLightingDescriptorSet();
        EnsurePointLightSsbo();
        AllocateDeferredDescriptorSets();
    }

    private void RecreateSwapchainDependent()
    {
        RecreateOffscreenTargets();
        CreateSwapchainFramebuffers();
    }

    private void CreateSwapchainFramebuffers()
    {
        _swapchainFramebuffers = new Framebuffer[_swapchainImageViews!.Length];

        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            var attachment = _swapchainImageViews[i];

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _rpComposite,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1
            };

            if (_vk!.CreateFramebuffer(_device, in framebufferInfo, null, out _swapchainFramebuffers[i]) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer (swapchain) failed.");
        }
    }

    private void CreateLinearSampler()
    {
        SamplerCreateInfo sci = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge
        };

        if (_vk!.CreateSampler(_device, in sci, null, out _samplerLinear) != Result.Success)
            throw new GraphicsInitializationException("vkCreateSampler failed.");
    }

    /// <summary>
    /// Two compatible passes: Vulkan requires <see cref="AttachmentDescription.InitialLayout"/> to match the image's
    /// actual layout at <c>CmdBeginRenderPass</c>. New images start Undefined; after EndRenderPass they are
    /// ShaderReadOnlyOptimal — the next Begin on that image must declare that layout, or sampling sees garbage (splotches, flicker).
    /// </summary>
    private void CreateOffscreenRenderPasses()
    {
        AttachmentDescription colorUndef = new()
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

        AttachmentDescription colorRead = colorUndef;
        colorRead.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

        AttachmentReference colorRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

        SubpassDescription sub = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorRef
        };

        SubpassDependency dep = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo rpci = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            SubpassCount = 1,
            PSubpasses = &sub,
            DependencyCount = 1,
            PDependencies = &dep
        };

        rpci.PAttachments = &colorUndef;
        if (_vk!.CreateRenderPass(_device, in rpci, null, out _rpOffscreenInitialUndefined) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass (offscreen, initial Undefined) failed.");

        rpci.PAttachments = &colorRead;
        if (_vk.CreateRenderPass(_device, in rpci, null, out _rpOffscreenInitialShaderRead) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass (offscreen, initial ShaderRead) failed.");
    }

    private RenderPass OffscreenRpFor(bool writtenSinceAlloc) =>
        writtenSinceAlloc ? _rpOffscreenInitialShaderRead : _rpOffscreenInitialUndefined;

    private void ResetOffscreenAttachmentWrittenFlags()
    {
        _offsWrittenEmissive = false;
        _offsWrittenHdr = false;
        _offsWrittenGbuffer = false;
        _offsWrittenWboit = false;
        _offsWrittenHdrComposite = false;
        _offsWrittenBloom0 = false;
        _offsWrittenBloom1 = false;
        Array.Fill(_offsWrittenBloomDown, false);
    }

    private void CreateCompositeRenderPass()
    {
        AttachmentDescription swapColor = new()
        {
            Format = _swapchainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.DontCare,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference swapRef = new() { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };

        SubpassDescription sub = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &swapRef
        };

        SubpassDependency dep = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo rpci = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &swapColor,
            SubpassCount = 1,
            PSubpasses = &sub,
            DependencyCount = 1,
            PDependencies = &dep
        };

        if (_vk!.CreateRenderPass(_device, in rpci, null, out _rpComposite) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass (composite) failed.");
    }

    private uint FindMemoryTypeDeviceLocal(uint typeFilter)
    {
        _vk!.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);
        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << (int)i)) != 0 &&
                (memProperties.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) ==
                MemoryPropertyFlags.DeviceLocalBit)
                return i;
        }

        throw new GraphicsInitializationException("No device-local memory type.");
    }

    private void CreateDeviceLocalImage(uint w, uint h, Format format, ImageUsageFlags usage, out Image img, out DeviceMemory mem, out ImageView view)
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.CreateDeviceLocalImage(w, h, format, usage, out img, out mem, out view);
    }

    private void CreateOffscreenImagesAndFramebuffers()
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.CreateOffscreenImagesAndFramebuffers();
    }

    private void CreateBloomHalfResTargets()
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.CreateBloomHalfResTargets();
    }

    private void RecreateOffscreenTargets()
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.RecreateOffscreenTargets();
    }

    private void DestroyOffscreenFramebuffer(ref Framebuffer fb, ref ImageView view, ref Image img, ref DeviceMemory mem)
    {
        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.DestroyOffscreenFramebuffer(ref fb, ref view, ref img, ref mem);
    }

    private void CreateDescriptorLayoutsAndPool()
    {
        DescriptorSetLayoutBinding texBind = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutCreateInfo dslTex = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &texBind
        };

        if (_vk!.CreateDescriptorSetLayout(_device, in dslTex, null, out _dslTexture) != Result.Success)
            throw new GraphicsInitializationException("dsl texture failed.");

        DescriptorSetLayoutBinding b0 = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b1 = new()
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b2 = new()
        {
            Binding = 2,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        var comps = stackalloc DescriptorSetLayoutBinding[3];
        comps[0] = b0;
        comps[1] = b1;
        comps[2] = b2;

        DescriptorSetLayoutCreateInfo dslC = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 3,
            PBindings = comps
        };

        if (_vk.CreateDescriptorSetLayout(_device, in dslC, null, out _dslComposite) != Result.Success)
            throw new GraphicsInitializationException("dsl composite failed.");

        DescriptorSetLayoutCreateInfo dslBe = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &b0
        };

        if (_vk.CreateDescriptorSetLayout(_device, in dslBe, null, out _dslBloomExtract) != Result.Success)
            throw new GraphicsInitializationException("dsl bloom extract failed.");

        var bloomDual = stackalloc DescriptorSetLayoutBinding[2];
        bloomDual[0] = b0;
        bloomDual[1] = b1;

        DescriptorSetLayoutCreateInfo dslBd = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 2,
            PBindings = bloomDual
        };

        if (_vk.CreateDescriptorSetLayout(_device, in dslBd, null, out _dslBloomDual) != Result.Success)
            throw new GraphicsInitializationException("dsl bloom dual failed.");

        DescriptorSetLayoutBinding emSceneBind = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutCreateInfo dslEm = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &emSceneBind
        };

        if (_vk.CreateDescriptorSetLayout(_device, in dslEm, null, out _dslEmissiveScene) != Result.Success)
            throw new GraphicsInitializationException("dsl emissive scene failed.");

        DescriptorSetLayoutBinding lightBind = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        DescriptorSetLayoutCreateInfo dslLight = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &lightBind
        };
        if (_vk.CreateDescriptorSetLayout(_device, in dslLight, null, out _dslLighting) != Result.Success)
            throw new GraphicsInitializationException("dsl lighting failed.");

        var gbufBinds = stackalloc DescriptorSetLayoutBinding[2];
        gbufBinds[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        gbufBinds[1] = new DescriptorSetLayoutBinding
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit | ShaderStageFlags.VertexBit
        };

        DescriptorSetLayoutCreateInfo dslGb = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 2,
            PBindings = gbufBinds
        };
        if (_vk.CreateDescriptorSetLayout(_device, in dslGb, null, out _dslGbufferRead) != Result.Success)
            throw new GraphicsInitializationException("dsl gbuffer read failed.");

        DescriptorSetLayoutBinding ssboBind = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit | ShaderStageFlags.VertexBit
        };
        DescriptorSetLayoutCreateInfo dslSsbo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &ssboBind
        };
        if (_vk.CreateDescriptorSetLayout(_device, in dslSsbo, null, out _dslPointSsbo) != Result.Success)
            throw new GraphicsInitializationException("dsl point ssbo failed.");

        var trBinds = stackalloc DescriptorSetLayoutBinding[3];
        trBinds[0] = b0;
        trBinds[1] = b1;
        trBinds[2] = b2;
        DescriptorSetLayoutCreateInfo dslTr = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 3,
            PBindings = trBinds
        };
        if (_vk.CreateDescriptorSetLayout(_device, in dslTr, null, out _dslTransparentResolve) != Result.Success)
            throw new GraphicsInitializationException("dsl transparent resolve failed.");

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

        if (_vk.CreateDescriptorPool(_device, in dpci, null, out _descriptorPool) != Result.Success)
            throw new GraphicsInitializationException("descriptor pool failed.");
    }

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

        PipelineColorBlendAttachmentState blendAdd = new()
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

        PipelineColorBlendStateCreateInfo cbPremul = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 1,
            PAttachments = &blendPremul
        };

        PipelineColorBlendStateCreateInfo cbAdd = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = 1,
            PAttachments = &blendAdd
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

        PipelineColorBlendAttachmentState blendOff = new()
        {
            BlendEnable = false,
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
        };

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

            var stEx = stackalloc PipelineShaderStageCreateInfo[2];
            stEx[0] = vs;
            stEx[1] = fsEx;

            GraphicsPipelineCreateInfo gpEx = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stEx,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vp,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cb,
                PDynamicState = &ds,
                Layout = _plBloomExtract,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };

            if (_vk!.CreateGraphicsPipelines(_device, default, 1, in gpEx, null, out _pipeBloomExtract) != Result.Success)
                throw new GraphicsInitializationException("pipe bloom extract failed.");

            var stDn = stackalloc PipelineShaderStageCreateInfo[2];
            stDn[0] = vs;
            stDn[1] = fsDn;

            GraphicsPipelineCreateInfo gpDn = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stDn,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vp,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cb,
                PDynamicState = &ds,
                Layout = _plBloomDownsample,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };

            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpDn, null, out _pipeBloomDownsample) != Result.Success)
                throw new GraphicsInitializationException("pipe bloom downsample failed.");

            var stG = stackalloc PipelineShaderStageCreateInfo[2];
            stG[0] = vs;
            stG[1] = fsG;

            GraphicsPipelineCreateInfo gpG = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stG,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vp,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cb,
                PDynamicState = &ds,
                Layout = _plBloomGaussian,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };

            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpG, null, out _pipeBloomGaussian) != Result.Success)
                throw new GraphicsInitializationException("pipe bloom gaussian failed.");

            var stUp = stackalloc PipelineShaderStageCreateInfo[2];
            stUp[0] = vs;
            stUp[1] = fsUp;

            GraphicsPipelineCreateInfo gpUp = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stUp,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vp,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cb,
                PDynamicState = &ds,
                Layout = _plBloomUpsample,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };

            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpUp, null, out _pipeBloomUpsample) != Result.Success)
                throw new GraphicsInitializationException("pipe bloom upsample failed.");

            var stCopy = stackalloc PipelineShaderStageCreateInfo[2];
            stCopy[0] = vs;
            stCopy[1] = fsCopy;

            GraphicsPipelineCreateInfo gpCopy = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stCopy,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vp,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cb,
                PDynamicState = &ds,
                Layout = _plBloomCopy,
                RenderPass = _rpOffscreenInitialUndefined,
                Subpass = 0
            };

            if (_vk.CreateGraphicsPipelines(_device, default, 1, in gpCopy, null, out _pipeBloomCopy) != Result.Success)
                throw new GraphicsInitializationException("pipe bloom copy failed.");
        }

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

        var st = stackalloc PipelineShaderStageCreateInfo[2];
        st[0] = vs;
        st[1] = fs;

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

        PipelineColorBlendAttachmentState blendOff = new()
        {
            BlendEnable = false,
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
        };

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

            GraphicsPipelineCreateInfo gp = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = st,
                PVertexInputState = &viEmpty,
                PInputAssemblyState = &ia,
                PViewportState = &vp,
                PRasterizationState = &rs,
                PMultisampleState = &ms,
                PColorBlendState = &cb,
                PDynamicState = &ds,
                Layout = _plComposite,
                RenderPass = _rpComposite,
                Subpass = 0
            };

            if (_vk!.CreateGraphicsPipelines(_device, default, 1, in gp, null, out _pipeComposite) != Result.Success)
                throw new GraphicsInitializationException("pipe composite failed.");
        }

        Marshal.FreeHGlobal(mainName);
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

    private void CreateLightingBuffer()
    {
        if (_lightingBuffer.Handle != default)
            return;
        CreateHostVisibleBuffer((ulong)sizeof(LightingUbo), BufferUsageFlags.UniformBufferBit, out _lightingBuffer, out _lightingBufferMemory);
    }

    private void DestroyLightingBuffer()
    {
        if (_lightingBuffer.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _lightingBuffer, null);
            _lightingBuffer = default;
        }
        if (_lightingBufferMemory.Handle != default)
        {
            _vk!.FreeMemory(_device, _lightingBufferMemory, null);
            _lightingBufferMemory = default;
        }
    }

    private void UpdateLightingFrameData(in FramePlan framePlan)
    {
        if (_vk is null || _lightingBufferMemory.Handle == default)
            return;

        var ambient = framePlan.AmbientLights.Length > 0 ? framePlan.AmbientLights[^1] : default;
        var directional = framePlan.DirectionalLights.Length > 0 ? framePlan.DirectionalLights[^1] : default;
        var spot = framePlan.SpotLights.Length > 0 ? framePlan.SpotLights[^1] : default;
        var dir = directional.DirectionWorld;
        var dirLen = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        if (dirLen < 1e-6f)
            dir = new Vector2D<float>(0.25f, 0.35f);
        else
            dir = new Vector2D<float>(dir.X / dirLen, dir.Y / dirLen);
        var sdir = spot.DirectionWorld;
        var sLen = MathF.Sqrt(sdir.X * sdir.X + sdir.Y * sdir.Y);
        if (sLen < 1e-6f)
            sdir = new Vector2D<float>(1f, 0f);
        else
            sdir = new Vector2D<float>(sdir.X / sLen, sdir.Y / sLen);

        var ubo = new LightingUbo
        {
            Ambient = new Vector4D<float>(ambient.Color.X, ambient.Color.Y, ambient.Color.Z, ambient.Intensity),
            DirectionalDirIntensity = new Vector4D<float>(dir.X, dir.Y, directional.Intensity, 0f),
            DirectionalColor = new Vector4D<float>(directional.Color.X, directional.Color.Y, directional.Color.Z, directional.CastsShadow ? 1f : 0f),
            PointPosRadius = default,
            PointColorIntensity = default,
            PointFalloff = default,
            SpotPosRadius = new Vector4D<float>(spot.PositionWorld.X, spot.PositionWorld.Y, spot.Radius, spot.CastsShadow ? 1f : 0f),
            SpotDirCosOuter = new Vector4D<float>(sdir.X, sdir.Y, MathF.Cos(spot.InnerConeRadians), MathF.Cos(spot.OuterConeRadians)),
            SpotColorIntensity = new Vector4D<float>(spot.Color.X, spot.Color.Y, spot.Color.Z, spot.Intensity)
        };

        void* p;
        if (_vk.MapMemory(_device, _lightingBufferMemory, 0, (ulong)sizeof(LightingUbo), 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map lighting ubo");
        Unsafe.Write(p, ubo);
        _vk.UnmapMemory(_device, _lightingBufferMemory);
    }

    private void DestroyGraphicsResources()
    {
        if (_vk is null)
            return;

        _pipelineFactory ??= new PipelineFactory(this);
        _pipelineFactory.DestroyPipelineAndShaderObjects();

        if (_descriptorPool.Handle != default)
        {
            _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
            _descriptorPool = default;
        }

        DestroyDsl2(ref _dslComposite);
        DestroyDsl2(ref _dslBloomExtract);
        DestroyDsl2(ref _dslBloomDual);
        DestroyDsl2(ref _dslEmissiveScene);
        DestroyDsl2(ref _dslLighting);
        DestroyDsl2(ref _dslGbufferRead);
        DestroyDsl2(ref _dslPointSsbo);
        DestroyDsl2(ref _dslTransparentResolve);
        DestroyDsl2(ref _dslTexture);
        DestroyLightingBuffer();
        DestroyPointLightSsboResources();

        if (_samplerLinear.Handle != default)
        {
            _vk.DestroySampler(_device, _samplerLinear, null);
            _samplerLinear = default;
        }

        _offscreenTargets ??= new OffscreenTargets(this);
        _offscreenTargets.DestroyGbufferAndWboitAndComposite();

        DestroyOffscreenFramebuffer(ref _fbEmissive, ref _viewEmissive, ref _imgEmissive, ref _memEmissive);
        DestroyOffscreenFramebuffer(ref _fbHdr, ref _viewHdr, ref _imgHdr, ref _memHdr);
        for (var i = 0; i < BloomDownsampleLevels; i++)
            DestroyOffscreenFramebuffer(ref _fbBloomDown[i], ref _viewBloomDown[i], ref _imgBloomDown[i], ref _memBloomDown[i]);
        DestroyOffscreenFramebuffer(ref _fbBloom0, ref _viewBloom0, ref _imgBloom0, ref _memBloom0);
        DestroyOffscreenFramebuffer(ref _fbBloom1, ref _viewBloom1, ref _imgBloom1, ref _memBloom1);

        if (_rpOffscreenInitialUndefined.Handle != default)
        {
            _vk.DestroyRenderPass(_device, _rpOffscreenInitialUndefined, null);
            _rpOffscreenInitialUndefined = default;
        }

        if (_rpOffscreenInitialShaderRead.Handle != default)
        {
            _vk.DestroyRenderPass(_device, _rpOffscreenInitialShaderRead, null);
            _rpOffscreenInitialShaderRead = default;
        }

        if (_rpComposite.Handle != default)
        {
            _vk.DestroyRenderPass(_device, _rpComposite, null);
            _rpComposite = default;
        }

        if (_rpGbufferUndefined.Handle != default)
        {
            _vk.DestroyRenderPass(_device, _rpGbufferUndefined, null);
            _rpGbufferUndefined = default;
        }

        if (_rpGbufferShaderRead.Handle != default)
        {
            _vk.DestroyRenderPass(_device, _rpGbufferShaderRead, null);
            _rpGbufferShaderRead = default;
        }

        if (_rpWboitUndefined.Handle != default)
        {
            _vk.DestroyRenderPass(_device, _rpWboitUndefined, null);
            _rpWboitUndefined = default;
        }

        if (_rpWboitShaderRead.Handle != default)
        {
            _vk.DestroyRenderPass(_device, _rpWboitShaderRead, null);
            _rpWboitShaderRead = default;
        }
    }

    private void DestroyShaderModule2(ref ShaderModule m)
    {
        if (m.Handle != default)
        {
            _vk!.DestroyShaderModule(_device, m, null);
            m = default;
        }
    }

    private void DestroyDsl2(ref DescriptorSetLayout dsl)
    {
        if (dsl.Handle != default)
        {
            _vk!.DestroyDescriptorSetLayout(_device, dsl, null);
            dsl = default;
        }
    }

    /// <summary>Scales separable Gaussian texel step from post-process bloom intensity (wider halos when brighter).</summary>
    private static float GetBloomGaussianRadiusScale(float bloomIntensity)
    {
        var t = Math.Clamp(bloomIntensity, 0.02f, 24f);
        return 0.85f + Math.Clamp(t * 0.55f, 0f, 4f);
    }

    private void RecordFullFrame(CommandBuffer cmd, Framebuffer swapFb)
    {
        _renderFrameRecorder ??= new RenderFrameRecorder(this);
        _renderFrameRecorder.Record(cmd, swapFb);
    }

    private void RecordFullFrameCore(CommandBuffer cmd, Framebuffer swapFb)
    {
        _framePlanBuilder ??= new FramePlanBuilder(this);
        _renderBackendExecutor ??= new RenderBackendExecutor(this);
        var framePlan = _framePlanBuilder.Build();
        _renderBackendExecutor.Record(cmd, swapFb, in framePlan);
    }

    private void ExecuteFramePlanCore(CommandBuffer cmd, Framebuffer swapFb, in FramePlan framePlan)
    {
        if (_vk!.ResetCommandBuffer(cmd, 0) != Result.Success)
            throw new InvalidOperationException("vkResetCommandBuffer failed.");

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };

        if (_vk.BeginCommandBuffer(cmd, in beginInfo) != Result.Success)
            throw new InvalidOperationException("vkBeginCommandBuffer failed.");

        var screen = framePlan.Screen;
        var sortIdx = framePlan.SortIndices;
        var sprites = framePlan.Sprites;
        var post = framePlan.ResolvedPost;
        UpdateLightingFrameData(in framePlan);
        UploadPointLightSsboData(in framePlan);

        Viewport vp = new()
        {
            X = 0f,
            Y = 0f,
            Width = _swapchainExtent.Width,
            Height = _swapchainExtent.Height,
            MinDepth = 0f,
            MaxDepth = 1f
        };

        Rect2D sci = new() { Offset = default, Extent = _swapchainExtent };

        ClearValue cEm = new()
        {
            Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
        };

        RenderPassBeginInfo rpEm = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = OffscreenRpFor(_offsWrittenEmissive),
            Framebuffer = _fbEmissive,
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 1,
            PClearValues = &cEm
        };

        _vk.CmdBeginRenderPass(cmd, &rpEm, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeEmissive);

        var vb = stackalloc[] { _vertexBuffer };
        var off = stackalloc ulong[] { 0 };
        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        foreach (var idx in sortIdx)
        {
            ref readonly var s = ref sprites[idx];
            DrawSprite(cmd, in s, screen, 0);
        }

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenEmissive = true;

        ClearValue cGb0 = cEm;
        ClearValue cGb1 = cEm;
        var cGbuf = stackalloc ClearValue[2];
        cGbuf[0] = cGb0;
        cGbuf[1] = cGb1;

        RenderPassBeginInfo rpGb = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = GbufferRpFor(_offsWrittenGbuffer),
            Framebuffer = _fbGbuffer,
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 2,
            PClearValues = cGbuf
        };

        _vk.CmdBeginRenderPass(cmd, &rpGb, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeSpriteGbuffer);
        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        foreach (var idx in sortIdx)
        {
            ref readonly var s = ref sprites[idx];
            if (!s.Transparent)
                DrawSprite(cmd, in s, screen, 1);
        }

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenGbuffer = true;

        ClearValue cHdr = new()
        {
            Color = new ClearColorValue { Float32_0 = 0.02f, Float32_1 = 0.02f, Float32_2 = 0.06f, Float32_3 = 1f }
        };

        RenderPassBeginInfo rpH = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = OffscreenRpFor(_offsWrittenHdr),
            Framebuffer = _fbHdr,
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 1,
            PClearValues = &cHdr
        };

        _vk.CmdBeginRenderPass(cmd, &rpH, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);

        var scrPush = stackalloc float[4];
        scrPush[0] = screen.X;
        scrPush[1] = screen.Y;
        scrPush[2] = 0f;
        scrPush[3] = 0f;

        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredBase);
        var setsBase = stackalloc DescriptorSet[2];
        setsBase[0] = _dsGbufferRead;
        setsBase[1] = _dsLighting;
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredBase, 0, 2, setsBase, 0, null);
        _vk.CmdPushConstants(cmd, _plDeferredBase, ShaderStageFlags.FragmentBit, 0, (uint)(sizeof(float) * 4), scrPush);
        _vk.CmdDraw(cmd, 3, 1, 0, 0);

        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredPoint);
        var setsPt = stackalloc DescriptorSet[2];
        setsPt[0] = _dsGbufferRead;
        setsPt[1] = _dsPointSsbo;
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredPoint, 0, 2, setsPt, 0, null);
        _vk.CmdPushConstants(cmd, _plDeferredPoint, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
            (uint)(sizeof(float) * 4), scrPush);
        var nPt = Math.Min(framePlan.PointLights.Length, MaxPointLights);
        if (nPt > 0)
        {
            _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
            _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);
            _vk.CmdDrawIndexed(cmd, 6, (uint)nPt, 0, 0, 0);
        }

        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeDeferredBleed);
        var setsBl = stackalloc DescriptorSet[2];
        setsBl[0] = _dsGbufferRead;
        setsBl[1] = _dsEmissiveScene;
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plDeferredBleed, 0, 2, setsBl, 0, null);
        _vk.CmdPushConstants(cmd, _plDeferredBleed, ShaderStageFlags.FragmentBit, 0, (uint)(sizeof(float) * 4), scrPush);
        _vk.CmdDraw(cmd, 3, 1, 0, 0);

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenHdr = true;

        ClearValue cWAccum = cEm;
        ClearValue cWReveal = new()
        {
            Color = new ClearColorValue { Float32_0 = 1f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
        };
        var cWboit = stackalloc ClearValue[2];
        cWboit[0] = cWAccum;
        cWboit[1] = cWReveal;

        RenderPassBeginInfo rpW = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = WboitRpFor(_offsWrittenWboit),
            Framebuffer = _fbWboit,
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 2,
            PClearValues = cWboit
        };

        _vk.CmdBeginRenderPass(cmd, &rpW, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeTransparentWboit);
        _vk.CmdBindVertexBuffers(cmd, 0, 1, vb, off);
        _vk.CmdBindIndexBuffer(cmd, _indexBuffer, 0, IndexType.Uint16);

        foreach (var idx in sortIdx)
        {
            ref readonly var s = ref sprites[idx];
            if (!s.Transparent)
                continue;
            DrawSprite(cmd, in s, screen, 2);
        }

        _vk.CmdEndRenderPass(cmd);
        _offsWrittenWboit = true;

        ClearValue cRes = cEm;
        RenderPassBeginInfo rpRes = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = OffscreenRpFor(_offsWrittenHdrComposite),
            Framebuffer = _fbHdrComposite,
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 1,
            PClearValues = &cRes
        };

        _vk.CmdBeginRenderPass(cmd, &rpRes, SubpassContents.Inline);
        _vk.CmdSetViewport(cmd, 0, 1, &vp);
        _vk.CmdSetScissor(cmd, 0, 1, &sci);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeTransparentResolve);
        fixed (DescriptorSet* dsTr = &_dsTransparentResolve)
        {
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plTransparentResolve, 0, 1, dsTr, 0, null);
        }
        _vk.CmdPushConstants(cmd, _plTransparentResolve, ShaderStageFlags.FragmentBit, 0, (uint)(sizeof(float) * 4), scrPush);
        _vk.CmdDraw(cmd, 3, 1, 0, 0);
        _vk.CmdEndRenderPass(cmd);
        _offsWrittenHdrComposite = true;

        var bloomGain = post.BloomEnabled ? post.BloomGain : 0f;
        var bloomOn = bloomGain > 0f;
        var bloomRadius = post.BloomRadius;

        Viewport vpHalf = new()
        {
            X = 0f,
            Y = 0f,
            Width = _bloomHalfW,
            Height = _bloomHalfH,
            MinDepth = 0f,
            MaxDepth = 1f
        };

        Rect2D sciHalf = new()
        {
            Offset = default,
            Extent = new Extent2D { Width = _bloomHalfW, Height = _bloomHalfH }
        };

        _postProcessGraph ??= new PostProcessGraph(this);
        var ppContext = new PostEffectContext(cmd, swapFb, framePlan, vp, sci, vpHalf, sciHalf);
        _postProcessGraph.Record(in ppContext, bloomOn, bloomGain, bloomRadius, post);

        if (_vk.EndCommandBuffer(cmd) != Result.Success)
            throw new InvalidOperationException("vkEndCommandBuffer failed.");
    }

    private void DrawSprite(CommandBuffer cmd, in SpriteDrawRequest s, Vector2D<float> screen, int mode)
    {
        var al = s.AlbedoTextureId >= 0 && s.AlbedoTextureId < _textureSlots.Count
            ? _textureSlots[s.AlbedoTextureId]
            : null;
        if (al is null)
            return;

        var px = WorldScreenSpace.WorldCenterToScreenPixel(s.CenterWorld, new Vector2D<int>((int)screen.X, (int)screen.Y));
        var uv = s.UvRect;
        if (uv.X == 0f && uv.Y == 0f && uv.Z == 0f && uv.W == 0f)
            uv = new Vector4D<float>(0f, 0f, 1f, 1f);

        var push = new SpritePushData
        {
            CenterHalfPx = new Vector4D<float>(px.X, px.Y, s.HalfExtentsWorld.X, s.HalfExtentsWorld.Y),
            UvRect = uv,
            ColorAlpha = new Vector4D<float>(s.ColorMultiply.X * s.Alpha, s.ColorMultiply.Y * s.Alpha, s.ColorMultiply.Z * s.Alpha, s.ColorMultiply.W * s.Alpha),
            EmissiveRgbIntensity = new Vector4D<float>(s.EmissiveTint.X, s.EmissiveTint.Y, s.EmissiveTint.Z, s.EmissiveIntensity),
            ScreenRot = new Vector4D<float>(screen.X, screen.Y, s.RotationRadians, 0f),
            Mode = mode,
            UseEmissiveMap = 0
        };

        if (mode == 0)
        {
            var useEm = s.EmissiveTextureId >= 0 && s.EmissiveTextureId < _textureSlots.Count ? 1 : 0;
            var emTexId = useEm != 0 ? s.EmissiveTextureId : _blackTextureId;
            var emSlot = emTexId >= 0 && emTexId < _textureSlots.Count ? _textureSlots[emTexId] : null;
            if (emSlot is null)
                return;
            push.UseEmissiveMap = useEm;

            var setsE = stackalloc DescriptorSet[2];
            setsE[0] = al.DescriptorSet;
            setsE[1] = emSlot.DescriptorSet;
            _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteEmissive, 0, 2, setsE, 0, null);
            _vk.CmdPushConstants(cmd, _plSpriteEmissive, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpritePushData), &push);
        }
        else if (mode == 1)
        {
            var nid = s.NormalTextureId >= 0 && s.NormalTextureId < _textureSlots.Count
                ? s.NormalTextureId
                : _defaultNormalTextureId;
            var nt = _textureSlots[nid];

            var setsG = stackalloc DescriptorSet[2];
            setsG[0] = al.DescriptorSet;
            setsG[1] = nt.DescriptorSet;
            _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteEmissive, 0, 2, setsG, 0, null);
            push.UseEmissiveMap = 0;
            _vk.CmdPushConstants(cmd, _plSpriteEmissive, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpritePushData), &push);
        }
        else
        {
            var setsW = stackalloc DescriptorSet[2];
            setsW[0] = al.DescriptorSet;
            setsW[1] = _dsHdrOpaqueForTransparent;
            push.UseEmissiveMap = 0;
            _vk!.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _plSpriteEmissive, 0, 2, setsW, 0, null);
            _vk.CmdPushConstants(cmd, _plSpriteEmissive, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0,
                (uint)sizeof(SpritePushData), &push);
        }

        _vk!.CmdDrawIndexed(cmd, 6, 1, 0, 0, 0);
    }

    private int RegisterTextureRgbaInternal(ReadOnlySpan<byte> rgba, int width, int height)
    {
        _textureUpload ??= new TextureUpload(this);
        return _textureUpload.RegisterTextureRgbaInternal(rgba, width, height);
    }

    private void UploadBuffer(ReadOnlySpan<byte> data, ulong size, out VkBuffer buf, out DeviceMemory bmem)
    {
        _textureUpload ??= new TextureUpload(this);
        _textureUpload.UploadBuffer(data, size, out buf, out bmem);
    }

    private void OneTimeCommands(Action<CommandBuffer> record)
    {
        _textureUpload ??= new TextureUpload(this);
        _textureUpload.OneTimeCommands(record);
    }

    private void CreateDefaultTextures()
    {
        _textureUpload ??= new TextureUpload(this);
        _textureUpload.CreateDefaultTextures();
    }
}
