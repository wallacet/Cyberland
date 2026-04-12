using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Glslang.NET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;
// Purpose: GPU resource fields, push-constant layouts, and render-pass selection state for the HDR deferred path.
// Data flow: host init calls pipeline partials; frame recording reads fields and updates _offsWritten* after each subpass.
// Invariants: _offsWritten* must match Vulkan image layouts (Undefined first write, ShaderReadOnly thereafter per attachment).

/// <summary>HDR: emissive prepass, deferred G-buffer + lighting, WBOIT transparency, bloom, composite (scene-linear).</summary>
public sealed unsafe partial class VulkanRenderer
{
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
    private readonly bool[] _offsWrittenBloomDown = new bool[DeferredRenderingConstants.BloomDownsampleLevels];

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
    /// <summary>Persistent host mapping for <see cref="_pointLightSsbo"/>; unmapped on teardown.</summary>
    private void* _pointLightSsboMapped;

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
    private readonly uint[] _bloomDownW = new uint[DeferredRenderingConstants.BloomDownsampleLevels];
    private readonly uint[] _bloomDownH = new uint[DeferredRenderingConstants.BloomDownsampleLevels];
    private readonly Image[] _imgBloomDown = new Image[DeferredRenderingConstants.BloomDownsampleLevels];
    private readonly DeviceMemory[] _memBloomDown = new DeviceMemory[DeferredRenderingConstants.BloomDownsampleLevels];
    private readonly ImageView[] _viewBloomDown = new ImageView[DeferredRenderingConstants.BloomDownsampleLevels];
    private readonly Framebuffer[] _fbBloomDown = new Framebuffer[DeferredRenderingConstants.BloomDownsampleLevels];

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
    private readonly DescriptorSet[] _dsBloomDownSrc = new DescriptorSet[DeferredRenderingConstants.BloomDownsampleLevels + 1];

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
    /// <summary>Persistent host mapping for <see cref="_lightingBuffer"/>; unmapped on teardown.</summary>
    private void* _lightingBufferMapped;

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
}
