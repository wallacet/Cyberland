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
    private const float TextMsdfEdgeSharpness = Text.TextMsdfDefaults.EdgeSharpness;

    /// <summary>Offscreen color passes: first write after image (re)alloc uses Undefined; subsequent passes must use ShaderReadOnly initial layout.</summary>
    private RenderPass _rpOffscreenInitialUndefined = default;

    private RenderPass _rpOffscreenInitialShaderRead = default;
    private RenderPass _rpComposite = default;
    private RenderPass _rpSwapchainUiOverlay = default;
    private RenderPass _rpGbufferUndefined = default;
    private RenderPass _rpGbufferShaderRead = default;
    private RenderPass _rpWboitUndefined = default;
    private RenderPass _rpWboitShaderRead = default;
    private RenderPass _rpShadowOccluderMask = default;
    private RenderPass _rpShadowJfaSeed = default;
    private RenderPass _rpShadowSdfFinal = default;

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

    private VkBuffer _directionalLightSsbo = default;
    private DeviceMemory _directionalLightSsboMemory = default;
    private void* _directionalLightSsboMapped;

    private VkBuffer _spotLightSsbo = default;
    private DeviceMemory _spotLightSsboMemory = default;
    private void* _spotLightSsboMapped;

    private VkBuffer _ssboTileBins = default;
    private DeviceMemory _memTileBins = default;
    private void* _tileBinsMapped;
    private VkBuffer _ssboTileIndices = default;
    private DeviceMemory _memTileIndices = default;
    private void* _tileIndicesMapped;

    private VkBuffer _ssboSpotTileBins = default;
    private DeviceMemory _memSpotTileBins = default;
    private void* _spotTileBinsMapped;
    private VkBuffer _ssboSpotTileIndices = default;
    private DeviceMemory _memSpotTileIndices = default;
    private void* _spotTileIndicesMapped;

    private DescriptorSetLayout _dslTiledLighting = default;
    private DescriptorSet _dsTiledLighting = default;

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
    /// <summary>Nearest-neighbor sampler bound to JFA seed descriptors (<c>_dsJfaSrcSeedA</c>, <c>_dsJfaSrcSeedB</c>) in <see cref="UpdateJfaDescriptorSets"/>.</summary>
    private Sampler _samplerNearest = default;

    private DescriptorPool _descriptorPool = default;
    private DescriptorSetLayout _dslTexture = default;
    private DescriptorSetLayout _dslComposite = default;
    private DescriptorSetLayout _dslBloomExtract = default;
    /// <summary>Two combined samplers (coarse + fine) for dual-filter bloom upsample.</summary>
    private DescriptorSetLayout _dslBloomDual = default;
    private DescriptorSetLayout _dslEmissiveScene = default;
    private DescriptorSetLayout _dslGbufferRead = default;
    private DescriptorSetLayout _dslTransparentResolve = default;
    private DescriptorSetLayout _dslJfaSrc = default;

    private DescriptorSet _dsEmissiveScene = default;
    private DescriptorSet _dsBloomExtract = default;
    /// <summary>Coarse + fine pyramid views for dual-filter bloom upsample (dual-sampler layout).</summary>
    private DescriptorSet _dsBloomUpsample = default;
    /// <summary>Static bindings for Gaussian pass: sample half-res bloom0 / bloom1 without mid-buffer descriptor overwrites.</summary>
    private DescriptorSet _dsBloomGaussianSrcBloom0 = default;

    private DescriptorSet _dsBloomGaussianSrcBloom1 = default;
    private DescriptorSet _dsGbufferRead = default;
    private DescriptorSet _dsTransparentResolve = default;
    private DescriptorSet _dsHdrOpaqueForTransparent = default;
    // Pyramid level descriptors: [0]=half-res bloom0, [1..N]=bloomDown[i-1]
    private readonly DescriptorSet[] _dsBloomDownSrc = new DescriptorSet[DeferredRenderingConstants.BloomDownsampleLevels + 1];

    private PipelineLayout _plSpriteTwoTexture = default;
    private PipelineLayout _plComposite = default;
    private PipelineLayout _plBloomExtract = default;
    private PipelineLayout _plBloomDownsample = default;
    private PipelineLayout _plBloomGaussian = default;
    private PipelineLayout _plBloomUpsample = default;
    private PipelineLayout _plBloomCopy = default;
    private PipelineLayout _plTiledDeferredLighting = default;
    private PipelineLayout _plDeferredBleed = default;
    private PipelineLayout _plTransparentResolve = default;
    private PipelineLayout _plTextMsdf = default;
    private PipelineLayout _plShadowOccluder = default;
    private PipelineLayout _plJfaInit = default;
    private PipelineLayout _plJfaStep = default;
    private PipelineLayout _plJfaToSdf = default;

    private ShaderModule _modVertSprite = default;
    private ShaderModule _modFragEmissive = default;
    private ShaderModule _modFragGbuffer = default;
    private ShaderModule _modFragSwapchainUi = default;
    private ShaderModule _modFragTiledDeferredLighting = default;
    private ShaderModule _modFragDeferredBleed = default;
    private ShaderModule _modFragTransparentWboit = default;
    private ShaderModule _modFragTransparentResolve = default;
    private ShaderModule _modFragComposite = default;
    private ShaderModule _modFragBloomExtract = default;
    private ShaderModule _modFragBloomDownsample = default;
    private ShaderModule _modFragBloomGaussian = default;
    private ShaderModule _modFragBloomUpsample = default;
    private ShaderModule _modFragBloomCopy = default;
    private ShaderModule _modVertTextMsdf = default;
    private ShaderModule _modFragTextMsdf = default;
    private ShaderModule _modVertShadowOccluder = default;
    private ShaderModule _modFragShadowOccluder = default;
    private ShaderModule _modVertFullscreenTriangle = default;
    private ShaderModule _modFragJfaInit = default;
    private ShaderModule _modFragJfaStep = default;
    private ShaderModule _modFragJfaToSdf = default;

    private Pipeline _pipeEmissive = default;
    private Pipeline _pipeSpriteGbuffer = default;
    private Pipeline _pipeSwapchainUiOverlay = default;
    private Pipeline _pipeTiledDeferredLighting = default;
    private Pipeline _pipeDeferredBleed = default;
    private Pipeline _pipeTransparentWboit = default;
    private Pipeline _pipeTransparentResolve = default;
    private Pipeline _pipeComposite = default;
    private Pipeline _pipeBloomExtract = default;
    private Pipeline _pipeBloomDownsample = default;
    private Pipeline _pipeBloomGaussian = default;
    private Pipeline _pipeBloomUpsample = default;
    private Pipeline _pipeBloomCopy = default;
    private Pipeline _pipeTextMsdf = default;
    private Pipeline _pipeShadowOccluder = default;
    private Pipeline _pipeJfaInit = default;
    private Pipeline _pipeJfaStep = default;
    private Pipeline _pipeJfaToSdf = default;
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
    private VkBuffer _textInstanceBuffer = default;
    private DeviceMemory _textInstanceBufferMemory = default;
    private void* _textInstanceBufferMapped;
    private int _textInstanceCapacity;
    private int _lastFrameTextGlyphInstances;
    private int _lastFrameTextBatchCount;
    private int _lastFrameTextDrawCalls;

    private VkBuffer _spriteInstanceBuffer;
    private DeviceMemory _spriteInstanceBufferMemory;
    private void* _spriteInstanceBufferMapped;
    private int _spriteInstanceCapacity;

    private int _lastFrameOverlaySpriteInstances;
    private int _lastFrameOverlaySpriteBatchCount;
    private int _lastFrameOverlaySpriteDrawCalls;

    private int _lastFrameDeferredEmissiveSpriteInstances;
    private int _lastFrameDeferredEmissiveSpriteBatchCount;
    private int _lastFrameDeferredEmissiveSpriteDrawCalls;

    private int _lastFrameDeferredOpaqueSpriteInstances;
    private int _lastFrameDeferredOpaqueSpriteBatchCount;
    private int _lastFrameDeferredOpaqueSpriteDrawCalls;

    private int _lastFrameDeferredTransparentSpriteInstances;
    private int _lastFrameDeferredTransparentSpriteBatchCount;
    private int _lastFrameDeferredTransparentSpriteDrawCalls;

    private int _lastFrameSubmittedPointLights;
    private int _lastFrameSubmittedDirectionalLights;
    private int _lastFrameSubmittedSpotLights;
    private int _lastFrameDroppedPointLights;
    private int _lastFrameDroppedDirectionalLights;
    private int _lastFrameDroppedSpotLights;
    private long _pointLightOverflowWarningTick;
    private long _spotLightOverflowWarningTick;
    private long _directionalLightOverflowWarningTick;
    private long _ambientLightOverflowWarningTick;

    /// <summary>One composite descriptor set per in-flight frame so updating bloom binding cannot race overlapping GPU work.</summary>
    private DescriptorSet[] _dsCompositeSlots = new DescriptorSet[MaxFramesInFlight];

    /// <summary>Per-frame push for all instanced sprite pipelines (viewport letterbox only).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SpriteInstancingPush
    {
        public Vector4D<float> ViewportPhysical;
    }

    /// <summary>Per-instance sprite quad data (binding 1, instance rate).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SpriteInstanceGpu
    {
        public Vector4D<float> CenterHalfPx;
        public Vector4D<float> UvRect;
        public Vector4D<float> ColorAlpha;
        public Vector4D<float> EmissiveRgbIntensity;
        /// <summary>XY unused; Z = rotation radians; W = 1 when emissive map samples set 1, else 0.</summary>
        public Vector4D<float> RotAndFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextMsdfPushData
    {
        /// <summary>Letterboxed viewport rectangle in swapchain pixels (x,y,w,h).</summary>
        public Vector4D<float> ViewportPhysical;
        /// <summary>XY = full swapchain size.</summary>
        public Vector2D<float> Screen;
        /// <summary>Scales screen-space edge slope reconstruction for crispness tuning.</summary>
        public float EdgeSharpness;
        public float _pad0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextGlyphInstanceGpu
    {
        public Vector4D<float> CenterHalfPx;
        public Vector4D<float> UvRect;
        public Vector4D<float> Color;
        public Vector4D<float> MsdfParams;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositePush
    {
        public float Bloom;
        public float Exposure;
        public float Saturation;
        public float EmissiveHdrGain;
        /// <summary>1 = apply pow(1/2.2) for UNORM swapchain; 0 = linear out (sRGB swapchain encodes on write).</summary>
        public float ApplyManualDisplayGamma;
        /// <summary>1 = Reinhard tonemap; 0 = skip (linear debug output).</summary>
        public float TonemapEnabled;
        public float Pad0;
        public float Pad1;
        /// <summary>RGB shadow-band tint (W unused, padding for vec4 alignment).</summary>
        public Vector4D<float> ColorGradingShadows;
        /// <summary>RGB midtone-band tint.</summary>
        public Vector4D<float> ColorGradingMidtones;
        /// <summary>RGB highlight-band tint.</summary>
        public Vector4D<float> ColorGradingHighlights;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BloomExtractPush
    {
        public float Threshold;
        public float Knee;
        public float BloomSourceGain;
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
    }

    /// <summary>Push constants for the tiled deferred lighting fullscreen pass.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TiledLightingPush
    {
        public Vector4D<float> ScreenSizeSwapchainPx_Pad;
        public Vector4D<float> CameraPosWorld_CameraRotRad;
        public Vector4D<float> ViewportSizeWorld_PhysicalScale;
        public Vector4D<float> PhysicalRectSwapchainPx;
        /// <summary>
        /// .X = shadow enabled (1 or 0). .Y/.Z/.W reserved for future use (e.g. shadow quality, bias overrides).
        /// Push constant layout is ABI-sensitive with the GPU shader — do not shrink.
        /// </summary>
        public Vector4D<float> ShadowSettings;
        /// <summary>
        /// .X = tileSizeSwapchainPx, .Y = tilesX (clamped), .Z = tilesY (clamped),
        /// .W = maxLightsPerTile — shared loop cap for both point and spot iteration in the shader;
        /// CPU binning caps spot lists at <see cref="DeferredRenderingConstants.MaxSpotLightsPerTile"/> (≤ .W).
        /// </summary>
        public Vector4D<float> TileSizeAndCounts;
    }

    /// <summary>
    /// Fullscreen deferred base pass: summed ambient and light counts. Per-light data is in SSBOs
    /// (<see cref="_directionalLightSsbo"/>, <see cref="_spotLightSsbo"/>).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct LightingUbo
    {
        /// <summary>Linear RGB in <c>XYZ</c>, combined intensity in <c>W</c> (sum of active ambients on CPU).</summary>
        public Vector4D<float> Ambient;
        /// <summary><c>X</c> = directional count, <c>Y</c> = spot count (clamped to max), <c>ZW</c> unused.</summary>
        public Vector4D<float> Counts;
    }
}
