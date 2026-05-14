using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InteropMarshal = System.Runtime.InteropServices.CollectionsMarshal;
using Glslang.NET;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Cyberland.Engine;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Scene;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Production <see cref="IRenderer"/> implementation: Vulkan swapchain, HDR offscreen targets, deferred lighting, emissive prepass,
/// weighted-blended transparency, bloom, and tonemapped composite to sRGB.
/// </summary>
/// <remarks>
/// <para>
/// <b>CPU-side frame lifecycle:</b> parallel ECS systems enqueue into thread-safe <see cref="ConcurrentQueue{T}"/> fields below.
/// Nothing hits the GPU until <see cref="DrawFrame"/> runs the frame-plan builder (<c>VulkanRenderer.FrameExecution.cs</c>), which <see cref="ConcurrentQueueDrain"/>
/// drains each queue exactly once per successful frame (grow-only scratch buffers hold the snapshot).
/// If <see cref="DrawFrame"/> aborts before that drain, callers must drop queues — see <see cref="IRenderer.ResetPendingSubmissionsForNewTick"/>.
/// </para>
/// <para>
/// <b>Partial layout:</b> <c>VulkanRenderer.cs</c> (swapchain, present, queues, <see cref="DrawFrame"/>);
/// <c>DeferredRenderingConstants</c> (HDR/bloom topology constants);
/// <c>VulkanRenderer.Deferred.State.cs</c> (GPU handles, push layouts, <c>_offsWritten*</c>);
/// <c>VulkanRenderer.Deferred.RenderPasses.cs</c> (render pass objects; Undefined vs ShaderRead variants);
/// <c>VulkanRenderer.Deferred.Pipelines.*.cs</c> (split: Init, Descriptors, GraphicsPipelines, Lighting, Teardown) and <c>VulkanGraphicsPipelineHelpers.cs</c>;
/// <c>VulkanRenderer.Deferred.Recording.cs</c> (per-frame command recording);
/// <c>VulkanRenderer.FrameExecution.cs</c> (<see cref="FramePlan"/> build, bloom/composite graph);
/// <c>VulkanRenderer.BloomPipeline.cs</c> (bloom pyramid); <c>VulkanRenderer.OffscreenTargets.cs</c>, <c>DescriptorManager</c>, <c>PipelineFactory</c>, <c>TextureUpload</c>.
/// </para>
/// <para>
/// <b>Frame order (recording):</b> emissive sprites → G-buffer (opaque) → deferred lighting → WBOIT (transparent) → resolve → bloom → composite to swapchain.
/// </para>
/// <para>
/// <b>HUD text:</b> ECS submits viewport UI into <c>_viewportUiOverlayQueue</c> (see <see cref="IsViewportUiOverlaySprite"/>).
/// CPU-side glyph caches live in <see cref="Scene.Systems.TextRenderSystem"/>; GPU blending cannot erase “extra” quads —
/// if RenderDoc shows duplicate draws, diagnosis stays on the CPU submit path first.
/// </para>
/// <para>
/// <b>Threading:</b> <see cref="IRenderer"/> submit APIs are synchronized for parallel ECS; Vulkan recording and <see cref="DrawFrame"/> run on the window thread.
/// </para>
/// <para>Mod code should depend on <see cref="IRenderer"/> only.</para>
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Requires a Vulkan-capable GPU and window surface.")]
public sealed unsafe partial class VulkanRenderer : IRenderer, IDisposable
{
    /// <summary>
    /// Concurrent recording/submit slots (fences, semaphores, command buffers per slot). Values &gt; 2 improve GPU overlap
    /// but add end-to-end latency (input → present); keep at 2 for responsive feel on desktop.
    /// </summary>
    private const int MaxFramesInFlight = 2;
    // Descriptor pool reserves combined-image-sampler entries for frame-global sets (gbuffer/bloom/composite/etc).
    // Keep a hard texture cap below the raw pool size so RegisterTexture* fails deterministically instead of
    // tripping descriptor-allocation failures after partial initialization.
    private const int MaxRegisteredTextures = 512;

    private static readonly string[] DeviceExtensions = ["VK_KHR_swapchain"];

    private readonly IWindow _window;
    private bool _resizePending;
    private readonly bool _logInitializationStages = ReadInitializationStageLoggingEnabled();
    private readonly bool _forceEngineGlslFallback = IsTruthy(Environment.GetEnvironmentVariable("CYBERLAND_FORCE_GLSL_SHADER_FALLBACK"));

    private Vk? _vk;
    private Instance _instance;
    private KhrSurface? _khrSurface;
    private SurfaceKHR _surface;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private KhrSwapchain? _khrSwapchain;
    private Queue _graphicsQueue;
    private Queue _presentQueue;

    private SwapchainKHR _swapchain;
    private Format _swapchainImageFormat;
    private Extent2D _swapchainExtent;
    private Image[]? _swapchainImages;
    private ImageView[]? _swapchainImageViews;
    private Framebuffer[]? _swapchainFramebuffers;
    private Framebuffer[]? _swapchainUiOverlayFramebuffers;

    private VkBuffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;
    private VkBuffer _indexBuffer;
    private DeviceMemory _indexBufferMemory;

    private CommandPool _commandPool;
    private CommandPool _uploadCommandPool;
    private CommandBuffer[]? _commandBuffers;

    private readonly ConcurrentQueue<SpriteDrawRequest> _spriteQueue = new();
    private readonly ConcurrentQueue<SpriteDrawRequest> _viewportUiOverlayQueue = new();
    private readonly object _textGlyphGate = new();
    private TextGlyphDrawRequest[]? _pendingTextGlyphs;
    private int _pendingTextGlyphCount;
    private readonly ConcurrentQueue<PointLight> _pointLightQueue = new();
    private readonly ConcurrentQueue<SpotLight> _spotLightQueue = new();
    private readonly ConcurrentQueue<DirectionalLight> _directionalLightQueue = new();
    private readonly ConcurrentQueue<AmbientLight> _ambientLightQueue = new();
    private readonly ConcurrentQueue<PostProcessVolumeSubmission> _volumeQueue = new();
    private readonly ConcurrentQueue<CameraViewRequest> _cameraQueue = new();

    // Grow-only scratch filled by ConcurrentQueueDrain during FramePlanBuilder.Build. Length often exceeds this frame's
    // SpriteCount / ViewportUiOverlaySpriteCount — sorters must use the counts (see SpriteDrawSorter.SortByLayerOrder(..., count)).
    private SpriteDrawRequest[]? _frameScratchSprites;
    private PointLight[]? _frameScratchPointLights;
    private SpotLight[]? _frameScratchSpotLights;
    private DirectionalLight[]? _frameScratchDirectionalLights;
    private AmbientLight[]? _frameScratchAmbientLights;
    private PostProcessVolumeSubmission[]? _frameScratchVolumes;
    private CameraViewRequest[]? _frameScratchCameras;
    private int[]? _frameScratchSortIndices;
    private SpriteDrawRequest[]? _frameScratchViewportUiOverlay;
    private int[]? _frameScratchViewportUiSortIndices;
    private TextGlyphDrawRequest[]? _frameScratchTextGlyphs;
    private int[]? _frameScratchTextSortIndices;

    // Selected camera viewport size for the NEXT frame; resolved under _cameraStateLock so mod systems can read
    // ActiveCameraViewportSize safely from parallel workers and layout against a stable value even if multiple
    // cameras are submitted concurrently. Initialized to swapchain size so the first frame's viewport anchors
    // behave like the pre-camera default.
    private Vector2D<int> _activeCameraViewportSize;
    private CameraViewRequest _activeCameraView;
    private readonly object _cameraStateLock = new();
    private readonly object _globalPostLock = new();

    private GlobalPostProcessSettings _globalPost = EngineDefaultGlobalPostProcess.DefaultSettings;

    private readonly List<GpuTexture> _textureSlots = new();
    private readonly object _textureSlotsLock = new();
    /// <summary>Append-only slot snapshot for lock-free reads on the render thread.</summary>
    private readonly GpuTexture?[] _textureSlotsSnapshot = new GpuTexture?[MaxRegisteredTextures];
    private int _textureSlotsSnapshotCount;
    private readonly object _uploadCommandLock = new();
    private readonly object _customShaderModulesLock = new();
    private readonly List<ShaderModule> _customShaderModules = new();
    private readonly HashSet<string> _shaderFallbackWarnings = new(StringComparer.Ordinal);
    private readonly float _bloomResolutionScale = ReadBloomResolutionScale();
    private int _deferredSpriteOverflowWarningIssued;
    private int _overlaySpriteOverflowWarningIssued;
    private int _textGlyphOverflowWarningIssued;
    private TextureId _whiteTextureId = TextureId.MaxValue;
    private TextureId _blackTextureId = TextureId.MaxValue;
    private TextureId _defaultNormalTextureId = TextureId.MaxValue;

    /// <summary>True when swapchain uses an sRGB image format so the composite shader outputs linear and avoids double gamma.</summary>
    private bool _swapchainUsesSrgbFramebuffer;

    /// <summary>Optional hook assigned by the host; mods may invoke to request a clean window close.</summary>
    public Action? RequestClose { get; set; }

    private FramePacing _framePacing = FramePacing.VSync;
    private readonly Stopwatch _limitedFrameTimer = new();
    private bool _minimalInitializationCompleted;
    private bool _fullInitializationCompleted;
    private bool _bootstrapPresentedAtLeastOnce;

    /// <inheritdoc />
    public FramePacing FramePacing
    {
        get => _framePacing;
        set
        {
            if (_framePacing == value)
                return;
            if (_vk is null)
            {
                _framePacing = value;
                return;
            }

            var support = QuerySwapChainSupport(_physicalDevice);
            var previousPresent = FramePacingPresentMode.SelectPresentMode(support.PresentModes, _framePacing);
            var nextPresent = FramePacingPresentMode.SelectPresentMode(support.PresentModes, value);
            _framePacing = value;
            if (previousPresent != nextPresent)
                RecreateSwapchain();
        }
    }

    private Semaphore[]? _imageAvailableSemaphores;
    private Semaphore[]? _renderFinishedSemaphores;
    private Fence[]? _inFlightFences;
    private Fence[]? _imagesInFlight;
    private int _currentFrame;

    /// <summary>Creates an uninitialized renderer; call <see cref="Initialize"/> after construction.</summary>
    /// <param name="window">Silk.NET window that provides a Vulkan surface.</param>
    public VulkanRenderer(IWindow window) => _window = window;

    /// <summary>Current swapchain size in pixels — physical window extent (matches shader <c>screenSize</c>).</summary>
    public Vector2D<int> SwapchainPixelSize => new((int)_swapchainExtent.Width, (int)_swapchainExtent.Height);

    /// <inheritdoc />
    public Vector2D<int> ActiveCameraViewportSize
    {
        get
        {
            lock (_cameraStateLock)
                return _activeCameraViewportSize.X > 0 && _activeCameraViewportSize.Y > 0 ? _activeCameraViewportSize : SwapchainPixelSize;
        }
    }

    /// <inheritdoc />
    public CameraViewRequest ActiveCameraView
    {
        get
        {
            lock (_cameraStateLock)
                return _activeCameraView.Enabled &&
                       _activeCameraView.ViewportSizeWorld.X > 0 &&
                       _activeCameraView.ViewportSizeWorld.Y > 0
                    ? _activeCameraView
                    : CameraSelection.Default(SwapchainPixelSize);
        }
    }

    /// <summary>Instrumentation: text glyph instance count written by the most recent frame encode.</summary>
    public int LastFrameTextGlyphInstances => _lastFrameTextGlyphInstances;

    /// <summary>Instrumentation: text batch groups (texture + clip) emitted by the most recent frame encode.</summary>
    public int LastFrameTextBatchCount => _lastFrameTextBatchCount;

    /// <summary>Instrumentation: text draw calls emitted by the most recent frame encode.</summary>
    public int LastFrameTextDrawCalls => _lastFrameTextDrawCalls;

    /// <summary>Instrumentation: viewport UI overlay sprite instances packed for the most recent successful frame encode.</summary>
    public int LastFrameOverlaySpriteInstances => _lastFrameOverlaySpriteInstances;

    /// <summary>Instrumentation: overlay sprite batch runs (descriptor + scissor groups) for the most recent frame encode.</summary>
    public int LastFrameOverlaySpriteBatchCount => _lastFrameOverlaySpriteBatchCount;

    /// <summary>Instrumentation: overlay sprite instanced draw calls for the most recent frame encode.</summary>
    public int LastFrameOverlaySpriteDrawCalls => _lastFrameOverlaySpriteDrawCalls;

    /// <summary>Instrumentation: deferred emissive sprite instances for the most recent frame encode.</summary>
    public int LastFrameDeferredEmissiveSpriteInstances => _lastFrameDeferredEmissiveSpriteInstances;

    /// <summary>Instrumentation: deferred emissive batch runs for the most recent frame encode.</summary>
    public int LastFrameDeferredEmissiveSpriteBatchCount => _lastFrameDeferredEmissiveSpriteBatchCount;

    /// <summary>Instrumentation: deferred emissive instanced draw calls for the most recent frame encode.</summary>
    public int LastFrameDeferredEmissiveSpriteDrawCalls => _lastFrameDeferredEmissiveSpriteDrawCalls;

    /// <summary>Instrumentation: deferred opaque (G-buffer) sprite instances for the most recent frame encode.</summary>
    public int LastFrameDeferredOpaqueSpriteInstances => _lastFrameDeferredOpaqueSpriteInstances;

    /// <summary>Instrumentation: deferred opaque batch runs for the most recent frame encode.</summary>
    public int LastFrameDeferredOpaqueSpriteBatchCount => _lastFrameDeferredOpaqueSpriteBatchCount;

    /// <summary>Instrumentation: deferred opaque instanced draw calls for the most recent frame encode.</summary>
    public int LastFrameDeferredOpaqueSpriteDrawCalls => _lastFrameDeferredOpaqueSpriteDrawCalls;

    /// <summary>Instrumentation: deferred transparent (WBOIT) sprite instances for the most recent frame encode.</summary>
    public int LastFrameDeferredTransparentSpriteInstances => _lastFrameDeferredTransparentSpriteInstances;

    /// <summary>Instrumentation: deferred transparent batch runs for the most recent frame encode.</summary>
    public int LastFrameDeferredTransparentSpriteBatchCount => _lastFrameDeferredTransparentSpriteBatchCount;

    /// <summary>Instrumentation: deferred transparent instanced draw calls for the most recent frame encode.</summary>
    public int LastFrameDeferredTransparentSpriteDrawCalls => _lastFrameDeferredTransparentSpriteDrawCalls;

    /// <summary>Instrumentation: point lights submitted in the most recent frame plan before cap/drop policy.</summary>
    public int LastFrameSubmittedPointLights => _lastFrameSubmittedPointLights;

    /// <summary>Instrumentation: directional lights submitted in the most recent frame plan before cap/drop policy.</summary>
    public int LastFrameSubmittedDirectionalLights => _lastFrameSubmittedDirectionalLights;

    /// <summary>Instrumentation: spot lights submitted in the most recent frame plan before cap/drop policy.</summary>
    public int LastFrameSubmittedSpotLights => _lastFrameSubmittedSpotLights;

    /// <summary>Instrumentation: point lights dropped by cap policy in the most recent frame encode.</summary>
    public int LastFrameDroppedPointLights => _lastFrameDroppedPointLights;

    /// <summary>Instrumentation: directional lights dropped by cap policy in the most recent frame encode.</summary>
    public int LastFrameDroppedDirectionalLights => _lastFrameDroppedDirectionalLights;

    /// <summary>Instrumentation: spot lights dropped by cap policy in the most recent frame encode.</summary>
    public int LastFrameDroppedSpotLights => _lastFrameDroppedSpotLights;

    TextureId IRenderer.RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height) =>
        RegisterTextureRgbaInternal(rgba, width, height, Format.R8G8B8A8Srgb);

    TextureId IRenderer.RegisterTextureRgbaLinear(ReadOnlySpan<byte> rgba, int width, int height) =>
        RegisterTextureRgbaInternal(rgba, width, height, Format.R8G8B8A8Unorm);

    bool IRenderer.TryUploadTextureRgbaSubregion(TextureId textureId, int dstX, int dstY, int width, int height,
        ReadOnlySpan<byte> rgba) =>
        TryUploadTextureRgbaSubregionInternal(textureId, dstX, dstY, width, height, rgba);

    TextureId IRenderer.DefaultNormalTextureId => _defaultNormalTextureId;

    TextureId IRenderer.WhiteTextureId => _whiteTextureId;

    IShaderModuleHandle IRenderer.CreateShaderModuleFromSpirv(ReadOnlySpan<byte> spirvBytes, string? debugName)
    {
        if (!SpirvBinary.TryDecodeWords(spirvBytes, out var words, out var failureReason))
            throw new InvalidOperationException($"SPIR-V decode failed: {failureReason}");

        var module = CreateShaderModule(words, debugName);
        RegisterCustomShaderModule(module);
        return new VulkanShaderModuleHandle(this, module);
    }

    IShaderModuleHandle IRenderer.CreateShaderModuleFromGlsl(
        string glsl,
        ShaderModuleStage stage,
        string? debugName,
        string? sourceDescription)
    {
        var shaderId = string.IsNullOrWhiteSpace(sourceDescription)
            ? (debugName ?? $"mod.shader.{stage}")
            : sourceDescription!;
        WarnShaderFallbackOnce(shaderId, "runtime GLSL compile requested for mod shader module");
        var spirv = GlslSpirvCompiler.CompileGlslToSpirv(glsl, MapShaderStage(stage));
        var module = CreateShaderModule(spirv, debugName);
        RegisterCustomShaderModule(module);
        return new VulkanShaderModuleHandle(this, module);
    }

    void IRenderer.SubmitSprite(in SpriteDrawRequest draw)
    {
        if (IsViewportUiOverlaySprite(in draw))
            _viewportUiOverlayQueue.Enqueue(draw);
        else
            _spriteQueue.Enqueue(draw);
    }

    void IRenderer.SubmitSprites(ReadOnlySpan<SpriteDrawRequest> draws)
    {
        if (draws.Length == 0)
            return;
        foreach (ref readonly var d in draws)
        {
            if (IsViewportUiOverlaySprite(in d))
                _viewportUiOverlayQueue.Enqueue(d);
            else
                _spriteQueue.Enqueue(d);
        }
    }

    void IRenderer.SubmitTextGlyph(in TextGlyphDrawRequest draw)
    {
        lock (_textGlyphGate)
        {
            EnsurePendingTextGlyphCapacityForAppend(1);
            _pendingTextGlyphs![_pendingTextGlyphCount++] = draw;
        }
    }

    void IRenderer.SubmitTextGlyphs(ReadOnlySpan<TextGlyphDrawRequest> draws)
    {
        if (draws.Length == 0)
            return;
        lock (_textGlyphGate)
        {
            EnsurePendingTextGlyphCapacityForAppend(draws.Length);
            draws.CopyTo(_pendingTextGlyphs.AsSpan(_pendingTextGlyphCount));
            _pendingTextGlyphCount += draws.Length;
        }
    }

    /// <inheritdoc />
    public void ResetPendingSubmissionsForNewTick() => DiscardAllPendingSubmissions();

    /// <summary>
    /// Drops every pending enqueue across sprite/light/camera queues — used when <see cref="DrawFrame"/> fails before
    /// the frame plan is built (queues drained into <see cref="FramePlan"/>), and proactively at the start of each render tick
    /// via <see cref="IRenderer.ResetPendingSubmissionsForNewTick"/> so undrained work cannot merge with the next ECS submit batch.
    /// </summary>
    private void DiscardAllPendingSubmissions()
    {
        ConcurrentQueueDrain.DiscardAll(_spriteQueue);
        ConcurrentQueueDrain.DiscardAll(_viewportUiOverlayQueue);
        lock (_textGlyphGate)
            _pendingTextGlyphCount = 0;
        ConcurrentQueueDrain.DiscardAll(_pointLightQueue);
        ConcurrentQueueDrain.DiscardAll(_spotLightQueue);
        ConcurrentQueueDrain.DiscardAll(_directionalLightQueue);
        ConcurrentQueueDrain.DiscardAll(_ambientLightQueue);
        ConcurrentQueueDrain.DiscardAll(_volumeQueue);
        ConcurrentQueueDrain.DiscardAll(_cameraQueue);
    }

    private void EnsurePendingTextGlyphCapacityForAppend(int appendCount)
    {
        var needed = _pendingTextGlyphCount + appendCount;
        if (_pendingTextGlyphs is not null && _pendingTextGlyphs.Length >= needed)
            return;
        var next = Math.Max(256, _pendingTextGlyphs?.Length ?? 0);
        while (next < needed)
            next *= 2;
        var grown = new TextGlyphDrawRequest[next];
        if (_pendingTextGlyphCount > 0 && _pendingTextGlyphs is not null)
            Array.Copy(_pendingTextGlyphs, grown, _pendingTextGlyphCount);
        _pendingTextGlyphs = grown;
    }

    internal int DrainPendingTextGlyphs(ref TextGlyphDrawRequest[]? scratch, out TextGlyphDrawRequest[] result)
    {
        lock (_textGlyphGate)
        {
            if (_pendingTextGlyphCount == 0)
            {
                result = scratch ?? Array.Empty<TextGlyphDrawRequest>();
                return 0;
            }

            if (scratch is null || scratch.Length < _pendingTextGlyphCount)
                scratch = new TextGlyphDrawRequest[Math.Max(_pendingTextGlyphCount, (scratch?.Length ?? 0) * 2)];

            Array.Copy(_pendingTextGlyphs!, scratch, _pendingTextGlyphCount);
            var count = _pendingTextGlyphCount;
            _pendingTextGlyphCount = 0;
            result = scratch;
            return count;
        }
    }

    /// <summary>
    /// Routes viewport/swapchain HUD to the post-composite overlay pass (straight-alpha on the swapchain image).
    /// Do not gate on <see cref="SpriteDrawRequest.Transparent"/>: misclassified viewport UI used to land in weighted OIT,
    /// which smears semi-transparent glyph stacks and reads like persistent HUD tails after copy changes.
    /// </summary>
    internal static bool IsViewportUiOverlaySprite(in SpriteDrawRequest d) =>
        d.Layer >= (int)SpriteLayer.Ui &&
        (d.Space == CoordinateSpace.ViewportSpace || d.Space == CoordinateSpace.SwapchainSpace);

    void IRenderer.SubmitPointLight(in PointLight light)
    {
        _pointLightQueue.Enqueue(light);
    }

    void IRenderer.SubmitSpotLight(in SpotLight light)
    {
        _spotLightQueue.Enqueue(light);
    }

    void IRenderer.SubmitDirectionalLight(in DirectionalLight light)
    {
        _directionalLightQueue.Enqueue(light);
    }

    void IRenderer.SubmitAmbientLight(in AmbientLight light)
    {
        _ambientLightQueue.Enqueue(light);
    }

    void IRenderer.SubmitPostProcessVolume(in PostProcessVolume volume, Vector2D<float> worldPosition, float worldRotationRadians, Vector2D<float> worldScale)
    {
        _volumeQueue.Enqueue(new PostProcessVolumeSubmission
        {
            Volume = volume,
            WorldPosition = worldPosition,
            WorldRotationRadians = worldRotationRadians,
            WorldScale = worldScale
        });
    }

    void IRenderer.SetGlobalPostProcess(in GlobalPostProcessSettings settings)
    {
        lock (_globalPostLock)
            _globalPost = settings;
    }

    void IRenderer.SubmitCamera(in CameraViewRequest camera)
    {
        _cameraQueue.Enqueue(camera);
    }

    /// <summary>
    /// Creates the minimal Vulkan objects needed to acquire/present swapchain images.
    /// Use <see cref="CompleteDeferredInitialization"/> later to build full deferred pipelines/resources.
    /// Throws <see cref="GraphicsInitializationException"/> if the surface is unavailable.
    /// </summary>
    public void InitializeMinimal()
    {
        if (_minimalInitializationCompleted)
            return;

        RunInitializationStage("window.initialize", () =>
        {
            if (!_window.IsInitialized)
                _window.Initialize();
        });

        if (((IVkSurfaceSource)_window).VkSurface is null)
            throw new GraphicsInitializationException(
                "The window does not expose a Vulkan surface (wrong window backend or initialization order).");

        RunInitializationStage("vk.instance_to_sync_objects", () =>
        {
            _vk = Vk.GetApi();
            CreateInstance();
            CreateSurface();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateSwapchain();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateSyncObjects();
        });

        _window.FramebufferResize += OnFramebufferResize;
        LogInitializationStage("window.framebuffer_resize_hook", 0d);
        _minimalInitializationCompleted = true;
    }

    /// <summary>
    /// Completes full renderer setup (deferred render graph resources, pipelines, and default textures).
    /// Safe to call once after <see cref="InitializeMinimal"/>.
    /// </summary>
    public void CompleteDeferredInitialization()
    {
        if (_fullInitializationCompleted)
            return;
        if (!_minimalInitializationCompleted)
            throw new InvalidOperationException("InitializeMinimal must run before CompleteDeferredInitialization.");

        RunInitializationStage("vk.graphics_pipeline_and_surfaces", () =>
        {
            CreateSpriteQuadMesh();
            CreateImageViews();
            CreateGraphicsPipelineAndSurfaces();
        });
        RunInitializationStage("vk.default_textures", CreateDefaultTextures);
        _fullInitializationCompleted = true;
    }

    /// <summary>
    /// Backward-compatible one-shot initialization: minimal bootstrap followed by full deferred setup.
    /// </summary>
    public void Initialize()
    {
        InitializeMinimal();
        CompleteDeferredInitialization();
    }

    private static bool ReadInitializationStageLoggingEnabled() =>
        IsTruthy(Environment.GetEnvironmentVariable("CYBERLAND_LOG_VULKAN_INIT_STAGES"));

    private static bool IsTruthy(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("on", StringComparison.OrdinalIgnoreCase));

    private void RunInitializationStage(string stageName, Action action)
    {
        if (!_logInitializationStages)
        {
            action();
            return;
        }

        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        LogInitializationStage(stageName, sw.Elapsed.TotalMilliseconds);
    }

    private static void LogInitializationStage(string stageName, double milliseconds) =>
        Console.WriteLine(
            $"Vulkan init stage | {stageName}={milliseconds.ToString("0.###", CultureInfo.InvariantCulture)}ms");

    private ShaderModule CreateEngineShaderModule(string sourceFileName, ShaderStage stage, string debugName)
    {
        var precompiledFailureReason = string.Empty;
        if (!_forceEngineGlslFallback &&
            EngineShaderSources.TryLoadPrecompiledSpirv(sourceFileName, out var spirvBytes, out precompiledFailureReason))
        {
            if (SpirvBinary.TryDecodeWords(spirvBytes, out var words, out var decodeFailure))
                return CreateShaderModule(words, debugName);

            WarnShaderFallbackOnce(debugName, $"precompiled SPIR-V decode failed for '{sourceFileName}': {decodeFailure}");
        }
        else
        {
            var reason = _forceEngineGlslFallback
                ? "forced by CYBERLAND_FORCE_GLSL_SHADER_FALLBACK"
                : precompiledFailureReason;
            WarnShaderFallbackOnce(debugName, $"precompiled SPIR-V unavailable for '{sourceFileName}': {reason}");
        }

        var glsl = EngineShaderSources.Load(sourceFileName);
        return CreateShaderModule(GlslSpirvCompiler.CompileGlslToSpirv(glsl, stage), debugName);
    }

    private void WarnShaderFallbackOnce(string shaderId, string reason)
    {
        lock (_shaderFallbackWarnings)
        {
            if (!_shaderFallbackWarnings.Add(shaderId))
                return;
        }

        Console.Error.WriteLine(
            $"Cyberland WARNING: shader fallback compile | shader={shaderId} reason={reason}");
    }

    private static ShaderStage MapShaderStage(ShaderModuleStage stage) =>
        stage switch
        {
            ShaderModuleStage.Vertex => ShaderStage.Vertex,
            ShaderModuleStage.Fragment => ShaderStage.Fragment,
            ShaderModuleStage.Compute => ShaderStage.Compute,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported shader module stage.")
        };

    private void OnFramebufferResize(Vector2D<int> _) => _resizePending = true;

    /// <summary>Records and submits GPU work for the current pending sprite/light queues, then presents to the screen.</summary>
    public void DrawFrame()
    {
        if (_vk is null)
            return;
        if (!_fullInitializationCompleted)
        {
            DrawBootstrapFrame();
            return;
        }

        if (_resizePending)
        {
            _resizePending = false;
            RecreateSwapchain();
        }

        if (_framePacing.Mode == FramePacingMode.Limited)
            _limitedFrameTimer.Restart();

        try
        {
            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("DrawFrame.WaitForFences");
#endif
                _vk.WaitForFences(_device, 1, in _inFlightFences![_currentFrame], true, ulong.MaxValue);
            }

            uint imageIndex = 0;
            Result acquire;
            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("DrawFrame.AcquireNextImageKHR");
#endif
                acquire = _khrSwapchain!.AcquireNextImage(
                    _device,
                    _swapchain,
                    ulong.MaxValue,
                    _imageAvailableSemaphores![_currentFrame],
                    default,
                    ref imageIndex);
            }

            if (acquire == Result.ErrorOutOfDateKhr)
            {
                RecreateSwapchain();
                // RunFrame() already enqueued this tick's work, but we never reach FramePlanBuilder.Build() to drain
                // queues. Without discarding, the next successful DrawFrame would merge submissions from multiple ECS ticks
                // (extra vkCmdDraw vs current HUD copy — stale long-string sprites stacked with new short-string submits).
                DiscardAllPendingSubmissions();
                return;
            }

            if (acquire != Result.Success && acquire != Result.SuboptimalKhr)
                throw new InvalidOperationException($"AcquireNextImage failed: {acquire}");

            if (_imagesInFlight![imageIndex].Handle != default)
            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("DrawFrame.WaitForImageFence");
#endif
                _vk.WaitForFences(_device, 1, in _imagesInFlight[imageIndex], true, ulong.MaxValue);
            }

            _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];

            var waitSemaphores = stackalloc[] { _imageAvailableSemaphores[_currentFrame] };
            var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
            var buffer = _commandBuffers![_currentFrame];

            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("DrawFrame.RecordCommandBuffer");
#endif
                RecordCommandBuffer(buffer, _swapchainFramebuffers![imageIndex], _swapchainUiOverlayFramebuffers![imageIndex]);
            }

            var signalSemaphores = stackalloc[] { _renderFinishedSemaphores![_currentFrame] };

            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphores,
                PWaitDstStageMask = waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = &buffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphores
            };

            _vk.ResetFences(_device, 1, in _inFlightFences[_currentFrame]);

            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("DrawFrame.QueueSubmit");
#endif
                BeginGpuQueueLabel("Queue.FrameSubmit");
                try
                {
                    if (_vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]) != Result.Success)
                        throw new InvalidOperationException("QueueSubmit failed.");
                }
                finally
                {
                    EndGpuQueueLabel();
                }
            }

            var swapChains = stackalloc[] { _swapchain };
            PresentInfoKHR presentInfo = new()
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,
                SwapchainCount = 1,
                PSwapchains = swapChains,
                PImageIndices = &imageIndex
            };

            Result present;
            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("DrawFrame.QueuePresentKHR");
#endif
                present = _khrSwapchain.QueuePresent(_presentQueue, &presentInfo);
            }

            if (present == Result.ErrorOutOfDateKhr || present == Result.SuboptimalKhr)
                _resizePending = true;
            else if (present != Result.Success)
                throw new InvalidOperationException($"QueuePresent failed: {present}");

            {
#if DEBUG
                using var __ = FrameProfilerScope.Enter("DrawFrame.ApplyLimitedCpuPacingIfNeeded");
#endif
                ApplyLimitedCpuPacingIfNeeded();
            }

            _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
        }
        catch
        {
            // RunFrame already submitted sprites/lights to ConcurrentQueues. FramePlanBuilder.Build drains them only after
            // RecordCommandBuffer starts. Any failure before that (WaitForFences, AcquireNextImage, invalid acquire result)
            // or during encode/submit/present without draining leaves queued work — drop it so the next DrawFrame cannot merge
            // multiple ECS ticks (extra vkCmdDraw / stale HUD glyphs).
            DiscardAllPendingSubmissions();
            throw;
        }
    }

    /// <summary>
    /// Presents a minimal clear-only frame while deferred pipelines are still initializing.
    /// </summary>
    public void DrawBootstrapFrame()
    {
        if (_vk is null || !_minimalInitializationCompleted)
            return;

        if (_resizePending)
        {
            _resizePending = false;
            RecreateSwapchain();
        }

        if (_framePacing.Mode == FramePacingMode.Limited)
            _limitedFrameTimer.Restart();

        _vk.WaitForFences(_device, 1, in _inFlightFences![_currentFrame], true, ulong.MaxValue);

        uint imageIndex = 0;
        var acquire = _khrSwapchain!.AcquireNextImage(
            _device,
            _swapchain,
            ulong.MaxValue,
            _imageAvailableSemaphores![_currentFrame],
            default,
            ref imageIndex);

        if (acquire == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }

        if (acquire != Result.Success && acquire != Result.SuboptimalKhr)
            throw new InvalidOperationException($"AcquireNextImage failed: {acquire}");

        if (_imagesInFlight![imageIndex].Handle != default)
            _vk.WaitForFences(_device, 1, in _imagesInFlight[imageIndex], true, ulong.MaxValue);
        _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];

        var commandBuffer = _commandBuffers![_currentFrame];
        RecordBootstrapClearCommandBuffer(commandBuffer, _swapchainImages![imageIndex]);

        var waitSemaphores = stackalloc[] { _imageAvailableSemaphores[_currentFrame] };
        var waitStages = stackalloc[] { PipelineStageFlags.TransferBit };
        var signalSemaphores = stackalloc[] { _renderFinishedSemaphores![_currentFrame] };
        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores
        };

        _vk.ResetFences(_device, 1, in _inFlightFences[_currentFrame]);
        if (_vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]) != Result.Success)
            throw new InvalidOperationException("QueueSubmit failed for bootstrap frame.");

        var swapChains = stackalloc[] { _swapchain };
        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1,
            PSwapchains = swapChains,
            PImageIndices = &imageIndex
        };

        var present = _khrSwapchain.QueuePresent(_presentQueue, &presentInfo);
        if (present == Result.ErrorOutOfDateKhr || present == Result.SuboptimalKhr)
            _resizePending = true;
        else if (present != Result.Success)
            throw new InvalidOperationException($"QueuePresent failed: {present}");

        ApplyLimitedCpuPacingIfNeeded();
        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
        _bootstrapPresentedAtLeastOnce = true;
    }

    private void RecordBootstrapClearCommandBuffer(CommandBuffer commandBuffer, Image swapchainImage)
    {
        _vk!.ResetCommandBuffer(commandBuffer, 0);
        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo
        };
        _vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        var oldLayout = _bootstrapPresentedAtLeastOnce ? ImageLayout.PresentSrcKhr : ImageLayout.Undefined;
        ImageMemoryBarrier toTransfer = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = ImageLayout.TransferDstOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = swapchainImage,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.TransferWriteBit,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };
        _vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.TransferBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in toTransfer);

        // Bootstrap clear is true black so pre-game startup never flashes tinted colors.
        ClearColorValue clearColor = new();
        clearColor.Float32_0 = 0f;
        clearColor.Float32_1 = 0f;
        clearColor.Float32_2 = 0f;
        clearColor.Float32_3 = 1f;
        ImageSubresourceRange range = new()
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1
        };
        _vk.CmdClearColorImage(commandBuffer, swapchainImage, ImageLayout.TransferDstOptimal, in clearColor, 1, in range);

        ImageMemoryBarrier toPresent = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = swapchainImage,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = 0,
            SubresourceRange = range
        };
        _vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.TransferBit,
            PipelineStageFlags.BottomOfPipeBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in toPresent);

        _vk.EndCommandBuffer(commandBuffer);
    }

    private void ApplyLimitedCpuPacingIfNeeded()
    {
        if (_framePacing.Mode != FramePacingMode.Limited)
            return;

        var delay = FramePacingCpu.GetRemainingDelaySeconds(
            _limitedFrameTimer.Elapsed.TotalSeconds,
            _framePacing.TargetFps);
        if (delay <= 0)
            return;

        var waited = Stopwatch.StartNew();
        while (waited.Elapsed.TotalSeconds < delay)
        {
            var left = delay - waited.Elapsed.TotalSeconds;
            if (left > 0.003)
                Thread.Sleep(1);
            else
                Thread.SpinWait(64);
        }
    }

    /// <summary>Waits for idle GPU then tears down swapchain and all Vulkan objects.</summary>
    public void Dispose()
    {
        _window.FramebufferResize -= OnFramebufferResize;

        if (_vk is null)
            return;

        if (_device.Handle != default)
            _vk.DeviceWaitIdle(_device);
        DestroyAllCustomShaderModules();
        _textureUpload?.Dispose();
        _textureUpload = null;

        CleanupSwapchain();

        DestroyGraphicsResources();
        DestroySpriteQuadMesh();

        if (_imageAvailableSemaphores is not null && _renderFinishedSemaphores is not null && _inFlightFences is not null)
        {
            for (var i = 0; i < MaxFramesInFlight; i++)
            {
                _vk.DestroySemaphore(_device, _renderFinishedSemaphores[i], null);
                _vk.DestroySemaphore(_device, _imageAvailableSemaphores[i], null);
                _vk.DestroyFence(_device, _inFlightFences[i], null);
            }
        }

        if (_commandPool.Handle != default)
            _vk.DestroyCommandPool(_device, _commandPool, null);
        if (_uploadCommandPool.Handle != default)
            _vk.DestroyCommandPool(_device, _uploadCommandPool, null);

        if (_device.Handle != default)
            _vk.DestroyDevice(_device, null);

        if (_surface.Handle != default)
            _khrSurface?.DestroySurface(_instance, _surface, null);

        if (_instance.Handle != default)
        {
            DisposeExtDebugUtils();
            _vk.DestroyInstance(_instance, null);
        }

        _vk.Dispose();
        _vk = null;
    }

    private void RegisterCustomShaderModule(ShaderModule module)
    {
        lock (_customShaderModulesLock)
            _customShaderModules.Add(module);
    }

    private void DestroyAllCustomShaderModules()
    {
        lock (_customShaderModulesLock)
        {
            if (_vk is not null && _device.Handle != default)
            {
                foreach (var module in _customShaderModules)
                {
                    if (module.Handle != default)
                        _vk.DestroyShaderModule(_device, module, null);
                }
            }

            _customShaderModules.Clear();
        }
    }

    private void DestroyCustomShaderModule(ref ShaderModule module)
    {
        if (module.Handle == default)
            return;

        lock (_customShaderModulesLock)
        {
            for (var i = 0; i < _customShaderModules.Count; i++)
            {
                if (_customShaderModules[i].Handle != module.Handle)
                    continue;
                _customShaderModules.RemoveAt(i);
                break;
            }

            if (_vk is not null && _device.Handle != default)
                _vk.DestroyShaderModule(_device, module, null);
            module = default;
        }
    }

    private sealed class VulkanShaderModuleHandle : IShaderModuleHandle
    {
        private VulkanRenderer? _owner;
        private ShaderModule _module;

        public VulkanShaderModuleHandle(VulkanRenderer owner, ShaderModule module)
        {
            _owner = owner;
            _module = module;
        }

        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
                return;

            owner.DestroyCustomShaderModule(ref _module);
            _owner = null;
        }
    }

    private void CreateInstance()
    {
        var vkSurface = ((IVkSurfaceSource)_window).VkSurface!;
        var extensionNames = GetInstanceExtensions(vkSurface);

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Cyberland"),
            ApplicationVersion = MakeVkVersion(0, 1, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("Cyberland.Engine"),
            EngineVersion = MakeVkVersion(0, 1, 0),
            ApiVersion = Vk.Version11
        };

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extensionNames.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensionNames)
        };

        var createResult = _vk!.CreateInstance(in createInfo, null, out _instance);

        Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        if (createResult != Result.Success)
            throw new GraphicsInitializationException($"vkCreateInstance failed with VkResult {createResult}.");

        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
        {
            _vk.DestroyInstance(_instance, null);
            _instance = default;
            throw new GraphicsInitializationException("VK_KHR_surface extension is not available on this Vulkan instance.");
        }

        TryInitializeExtDebugUtilsExtension();
    }

    private static string[] GetInstanceExtensions(IVkSurface vkSurface)
    {
        var glfwExtensions = vkSurface.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);
        return AppendDebugUtilsInstanceExtension(extensions);
    }

    private void CreateSurface()
    {
        var vkSurface = ((IVkSurfaceSource)_window).VkSurface!;
        var surfaceHandle = vkSurface.Create<AllocationCallbacks>(new VkHandle(_instance.Handle), null);
        _surface = Unsafe.BitCast<VkNonDispatchableHandle, SurfaceKHR>(surfaceHandle);
    }

    private void PickPhysicalDevice()
    {
        var devices = _vk!.GetPhysicalDevices(_instance);
        var hasCandidate = false;
        var bestScore = int.MinValue;
        PhysicalDevice bestDevice = default;
        foreach (var device in devices)
        {
            if (!TryScorePhysicalDevice(device, out var score))
                continue;
            if (!hasCandidate || score > bestScore)
            {
                hasCandidate = true;
                bestScore = score;
                bestDevice = device;
            }
        }
        if (hasCandidate)
        {
            _physicalDevice = bestDevice;
            return;
        }

        throw new GraphicsInitializationException(
            "No suitable Vulkan GPU was found (graphics + present queues, swapchain support, and required device extensions).");
    }

    private bool TryScorePhysicalDevice(PhysicalDevice device, out int score)
    {
        score = 0;
        var indices = FindQueueFamilies(device);
        if (!indices.IsComplete())
            return false;

        if (!CheckDeviceExtensionSupport(device))
            return false;

        var swapChainSupport = QuerySwapChainSupport(device);
        if (swapChainSupport.Formats.Length == 0 || swapChainSupport.PresentModes.Length == 0)
            return false;

        _vk!.GetPhysicalDeviceProperties(device, out var properties);
        score += properties.DeviceType switch
        {
            PhysicalDeviceType.DiscreteGpu => 10_000,
            PhysicalDeviceType.IntegratedGpu => 6_000,
            PhysicalDeviceType.VirtualGpu => 4_000,
            PhysicalDeviceType.Cpu => 1_000,
            _ => 2_000
        };
        score += (int)Math.Min(properties.Limits.MaxImageDimension2D, 8192);
        return true;
    }

    private bool CheckDeviceExtensionSupport(PhysicalDevice device)
    {
        uint extensionCount = 0;
        _vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, null);

        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
            _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, availableExtensionsPtr);

        var availableExtensionNames = availableExtensions
            .Select(static e => Marshal.PtrToStringAnsi((nint)e.ExtensionName))
            .ToHashSet();

        return DeviceExtensions.All(availableExtensionNames.Contains);
    }

    private void CreateLogicalDevice()
    {
        var indices = FindQueueFamilies(_physicalDevice);
        var uniqueQueueFamilies = new[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value }.Distinct().ToArray();

        var queueCreateInfos = new DeviceQueueCreateInfo[uniqueQueueFamilies.Length];
        var queuePriority = 1.0f;

        fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        {
            for (var i = 0; i < uniqueQueueFamilies.Length; i++)
            {
                queueCreateInfos[i] = new DeviceQueueCreateInfo
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = uniqueQueueFamilies[i],
                    QueueCount = 1,
                    PQueuePriorities = &queuePriority
                };
            }

            PhysicalDeviceFeatures deviceFeatures = new();

            DeviceCreateInfo createInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
                PQueueCreateInfos = pQueueCreateInfos,
                PEnabledFeatures = &deviceFeatures,
                EnabledExtensionCount = (uint)DeviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(DeviceExtensions)
            };

            if (_vk!.CreateDevice(_physicalDevice, in createInfo, null, out _device) != Result.Success)
                throw new GraphicsInitializationException("vkCreateDevice failed.");

            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
        }

        _vk.GetDeviceQueue(_device, indices.GraphicsFamily!.Value, 0, out _graphicsQueue);
        _vk.GetDeviceQueue(_device, indices.PresentFamily!.Value, 0, out _presentQueue);

        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
            throw new GraphicsInitializationException("VK_KHR_swapchain extension is not available on this device.");

        RefreshGpuDebugMarkersEnabled();
    }

    private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
    {
        var indices = new QueueFamilyIndices();

        uint queueFamilyCount = 0;
        _vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);

        uint i = 0;
        foreach (var queueFamily in queueFamilies)
        {
            if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                indices.GraphicsFamily = i;

            _khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, _surface, out var presentSupport);
            if (presentSupport)
                indices.PresentFamily = i;

            if (indices.IsComplete())
                break;

            i++;
        }

        return indices;
    }

    private SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device)
    {
        var details = new SwapChainSupportDetails();

        _khrSurface!.GetPhysicalDeviceSurfaceCapabilities(device, _surface, out details.Capabilities);

        uint formatCount = 0;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(device, _surface, ref formatCount, null);

        if (formatCount != 0)
        {
            details.Formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
                _khrSurface.GetPhysicalDeviceSurfaceFormats(device, _surface, ref formatCount, formatsPtr);
        }
        else
        {
            details.Formats = [];
        }

        uint presentModeCount = 0;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(device, _surface, ref presentModeCount, null);

        if (presentModeCount != 0)
        {
            details.PresentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* presentModesPtr = details.PresentModes)
                _khrSurface.GetPhysicalDeviceSurfacePresentModes(device, _surface, ref presentModeCount, presentModesPtr);
        }
        else
        {
            details.PresentModes = [];
        }

        return details;
    }

    private void CreateSwapchain()
    {
        var swapChainSupport = QuerySwapChainSupport(_physicalDevice);

        var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
        var presentMode = FramePacingPresentMode.SelectPresentMode(swapChainSupport.PresentModes, _framePacing);
        var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

        var imageCount = FramePacingPresentMode.AdjustMinImageCount(
            swapChainSupport.Capabilities.MinImageCount,
            swapChainSupport.Capabilities.MaxImageCount,
            presentMode);

        var indices = FindQueueFamilies(_physicalDevice);
        var queueFamilyIndices = stackalloc[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

        SwapchainCreateInfoKHR creatInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = swapChainSupport.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default
        };

        if (indices.GraphicsFamily != indices.PresentFamily)
        {
            creatInfo.ImageSharingMode = SharingMode.Concurrent;
            creatInfo.QueueFamilyIndexCount = 2;
            creatInfo.PQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            creatInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        if (_khrSwapchain!.CreateSwapchain(_device, in creatInfo, null, out _swapchain) != Result.Success)
            throw new GraphicsInitializationException("vkCreateSwapchainKHR failed.");

        SetGpuObjectName(ObjectType.SwapchainKhr, VkHandle(_swapchain), "swapchain.Present");

        _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, null);
        _swapchainImages = new Image[imageCount];
        fixed (Image* swapChainImagesPtr = _swapchainImages)
            _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, swapChainImagesPtr);

        _swapchainImageFormat = surfaceFormat.Format;
        _swapchainExtent = extent;
        _swapchainUsesSrgbFramebuffer = surfaceFormat.Format is Format.B8G8R8A8Srgb or Format.R8G8B8A8Srgb;
    }

    private void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages!.Length];

        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainImageFormat,
                Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (_vk!.CreateImageView(_device, in createInfo, null, out _swapchainImageViews[i]) != Result.Success)
                throw new GraphicsInitializationException("vkCreateImageView failed.");
        }

        NameSwapchainImagesAndViewsForRenderDoc();
    }

    private void CreateCommandPool()
    {
        var queueFamilyIndex = FindQueueFamilies(_physicalDevice).GraphicsFamily!.Value;

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndex
        };

        if (_vk!.CreateCommandPool(_device, in poolInfo, null, out _commandPool) != Result.Success)
            throw new GraphicsInitializationException("vkCreateCommandPool failed.");
        SetGpuObjectName(ObjectType.CommandPool, VkHandle(_commandPool), "pool.FrameCommands");

        CommandPoolCreateInfo uploadPoolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndex
        };
        if (_vk.CreateCommandPool(_device, in uploadPoolInfo, null, out _uploadCommandPool) != Result.Success)
            throw new GraphicsInitializationException("vkCreateCommandPool (upload) failed.");
        SetGpuObjectName(ObjectType.CommandPool, VkHandle(_uploadCommandPool), "pool.Upload");
    }

    private void CreateCommandBuffers()
    {
        _commandBuffers = new CommandBuffer[MaxFramesInFlight];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)_commandBuffers.Length
        };

        fixed (CommandBuffer* commandBuffersPtr = _commandBuffers)
        {
            if (_vk!.AllocateCommandBuffers(_device, in allocInfo, commandBuffersPtr) != Result.Success)
                throw new GraphicsInitializationException("vkAllocateCommandBuffers failed.");
        }

        for (var i = 0; i < _commandBuffers.Length; i++)
            SetGpuObjectName(ObjectType.CommandBuffer, VkHandle(_commandBuffers[i]), $"cmd.Frame[{i}]");
    }

    private void RecordCommandBuffer(CommandBuffer commandBuffer, Framebuffer framebuffer, Framebuffer swapchainUiOverlayFramebuffer) =>
        RecordFullFrame(commandBuffer, framebuffer, swapchainUiOverlayFramebuffer);

    private void CreateSpriteQuadMesh()
    {
        Span<float> vertices = stackalloc float[8]
        {
            -1f, -1f,
            1f, -1f,
            1f, 1f,
            -1f, 1f
        };

        Span<ushort> indices = stackalloc ushort[6] { 0, 1, 2, 2, 3, 0 };

        CreateHostVisibleBuffer(
            (ulong)(vertices.Length * sizeof(float)),
            BufferUsageFlags.VertexBufferBit,
            out _vertexBuffer,
            out _vertexBufferMemory);

        CreateHostVisibleBuffer(
            (ulong)(indices.Length * sizeof(ushort)),
            BufferUsageFlags.IndexBufferBit,
            out _indexBuffer,
            out _indexBufferMemory);

        void* data = null;
        if (_vk!.MapMemory(_device, _vertexBufferMemory, 0, (ulong)(vertices.Length * sizeof(float)), 0, &data) != Result.Success)
            throw new GraphicsInitializationException("vkMapMemory (vertex) failed.");

        vertices.CopyTo(new Span<float>((float*)data, vertices.Length));
        _vk.UnmapMemory(_device, _vertexBufferMemory);

        if (_vk.MapMemory(_device, _indexBufferMemory, 0, (ulong)(indices.Length * sizeof(ushort)), 0, &data) != Result.Success)
            throw new GraphicsInitializationException("vkMapMemory (index) failed.");

        indices.CopyTo(new Span<ushort>((ushort*)data, indices.Length));
        _vk.UnmapMemory(_device, _indexBufferMemory);

        SetGpuObjectName(ObjectType.Buffer, VkHandle(_vertexBuffer), "buf.SpriteQuad.Vertices");
        SetGpuObjectName(ObjectType.DeviceMemory, VkHandle(_vertexBufferMemory), "mem.SpriteQuad.Vertices");
        SetGpuObjectName(ObjectType.Buffer, VkHandle(_indexBuffer), "buf.SpriteQuad.Indices");
        SetGpuObjectName(ObjectType.DeviceMemory, VkHandle(_indexBufferMemory), "mem.SpriteQuad.Indices");
    }

    private ShaderModule CreateShaderModule(uint[] code, string? debugName = null)
    {
        fixed (uint* codePtr = code)
        {
            ShaderModuleCreateInfo createInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)(code.Length * sizeof(uint)),
                PCode = codePtr
            };

            if (_vk!.CreateShaderModule(_device, in createInfo, null, out var module) != Result.Success)
                throw new GraphicsInitializationException("vkCreateShaderModule failed.");

            if (debugName is not null)
                SetGpuObjectName(ObjectType.ShaderModule, VkHandle(module), debugName);
            return module;
        }
    }

    private void CreateHostVisibleBuffer(ulong size, BufferUsageFlags usage, out VkBuffer buffer, out DeviceMemory memory)
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (_vk!.CreateBuffer(_device, in bufferInfo, null, out buffer) != Result.Success)
            throw new GraphicsInitializationException("vkCreateBuffer failed.");

        _vk.GetBufferMemoryRequirements(_device, buffer, out var memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
        };

        if (_vk.AllocateMemory(_device, in allocInfo, null, out memory) != Result.Success)
            throw new GraphicsInitializationException("vkAllocateMemory failed.");

        _vk.BindBufferMemory(_device, buffer, memory, 0);

        // Caller assigns descriptive names at stable creation sites (sprite/text/light buffers).
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _vk!.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);

        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << (int)i)) != 0 &&
                (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        throw new GraphicsInitializationException("Failed to find a host-visible memory type for GPU buffers.");
    }

    private void DestroySpriteQuadMesh()
    {
        if (_vk is null)
            return;

        if (_indexBuffer.Handle != default)
        {
            _vk.DestroyBuffer(_device, _indexBuffer, null);
            _indexBuffer = default;
        }

        if (_vertexBuffer.Handle != default)
        {
            _vk.DestroyBuffer(_device, _vertexBuffer, null);
            _vertexBuffer = default;
        }

        if (_indexBufferMemory.Handle != default)
        {
            _vk.FreeMemory(_device, _indexBufferMemory, null);
            _indexBufferMemory = default;
        }

        if (_vertexBufferMemory.Handle != default)
        {
            _vk.FreeMemory(_device, _vertexBufferMemory, null);
            _vertexBufferMemory = default;
        }
    }

    private void CreateSyncObjects()
    {
        var imageCount = _swapchainImages!.Length;

        _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        _renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
        _inFlightFences = new Fence[MaxFramesInFlight];
        _imagesInFlight = new Fence[imageCount];

        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            if (_vk!.CreateSemaphore(_device, in semaphoreInfo, null, out _imageAvailableSemaphores[i]) != Result.Success ||
                _vk.CreateSemaphore(_device, in semaphoreInfo, null, out _renderFinishedSemaphores[i]) != Result.Success ||
                _vk.CreateFence(_device, in fenceInfo, null, out _inFlightFences[i]) != Result.Success)
            {
                throw new GraphicsInitializationException("Failed to create Vulkan synchronization objects (semaphores/fences).");
            }

            SetGpuObjectName(ObjectType.Semaphore, VkHandle(_imageAvailableSemaphores[i]), $"sem.ImageAvailable[{i}]");
            SetGpuObjectName(ObjectType.Semaphore, VkHandle(_renderFinishedSemaphores[i]), $"sem.RenderFinished[{i}]");
            SetGpuObjectName(ObjectType.Fence, VkHandle(_inFlightFences[i]), $"fence.InFlight[{i}]");
        }
    }

    private void RecreateSwapchain()
    {
        var size = _window.FramebufferSize;
        if (size.X == 0 || size.Y == 0)
            return;

        _vk!.DeviceWaitIdle(_device);

        CleanupSwapchain();

        CreateSwapchain();
        if (_fullInitializationCompleted)
        {
            CreateImageViews();
            RecreateSwapchainDependent();
        }
        CreateCommandBuffers();

        _imagesInFlight = new Fence[_swapchainImages!.Length];
        _bootstrapPresentedAtLeastOnce = false;
    }

    private void CleanupSwapchain()
    {
        if (_vk is null)
            return;

        if (_commandBuffers is { Length: > 0 })
        {
            _vk.FreeCommandBuffers(_device, _commandPool, (uint)_commandBuffers.Length, _commandBuffers);
            _commandBuffers = null;
        }

        if (_swapchainFramebuffers is not null)
        {
            foreach (var fb in _swapchainFramebuffers)
                _vk.DestroyFramebuffer(_device, fb, null);

            _swapchainFramebuffers = null;
        }

        if (_swapchainUiOverlayFramebuffers is not null)
        {
            foreach (var fb in _swapchainUiOverlayFramebuffers)
                _vk.DestroyFramebuffer(_device, fb, null);

            _swapchainUiOverlayFramebuffers = null;
        }

        if (_swapchainImageViews is not null)
        {
            foreach (var view in _swapchainImageViews)
                _vk.DestroyImageView(_device, view, null);

            _swapchainImageViews = null;
        }

        if (_swapchain.Handle != default)
        {
            _khrSwapchain?.DestroySwapchain(_device, _swapchain, null);
            _swapchain = default;
        }

        _swapchainImages = null;
    }

    private static SurfaceFormatKHR ChooseSwapSurfaceFormat(IReadOnlyList<SurfaceFormatKHR> availableFormats)
    {
        foreach (var availableFormat in availableFormats)
        {
            if (availableFormat.Format == Format.B8G8R8A8Srgb &&
                availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return availableFormat;
            }
        }

        return availableFormats[0];
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        var framebufferSize = _window.FramebufferSize;

        Extent2D actualExtent = new()
        {
            Width = (uint)framebufferSize.X,
            Height = (uint)framebufferSize.Y
        };

        actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
        actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

        return actualExtent;
    }

    private struct QueueFamilyIndices
    {
        public uint? GraphicsFamily;
        public uint? PresentFamily;

        public bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;
    }

    private struct SwapChainSupportDetails
    {
        public SurfaceCapabilitiesKHR Capabilities;
        public SurfaceFormatKHR[] Formats;
        public PresentModeKHR[] PresentModes;
    }

    private static uint MakeVkVersion(uint major, uint minor, uint patch) =>
        (major << 22) | (minor << 12) | patch;

    private static float ReadBloomResolutionScale()
    {
        var raw = Environment.GetEnvironmentVariable("CYBERLAND_BLOOM_RESOLUTION_SCALE");
        if (string.IsNullOrWhiteSpace(raw))
            return 1f;
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return 1f;
        return Math.Clamp(parsed, 0.25f, 1f);
    }

    private GpuTexture? TryGetTextureSlot(TextureId id)
    {
        var snap = _textureSlotsSnapshot;
        var count = Volatile.Read(ref _textureSlotsSnapshotCount);
        var idx = (int)id;
        if (idx < 0 || idx >= count)
            return null;
        return snap[idx];
    }

    /// <summary>Rebuilds <see cref="_textureSlotsSnapshot"/> from <see cref="_textureSlots"/> under <see cref="_textureSlotsLock"/>.</summary>
    private void RefreshTextureSlotsSnapshot()
    {
        lock (_textureSlotsLock)
        {
            var n = _textureSlots.Count;
            for (var i = 0; i < n; i++)
                _textureSlotsSnapshot[i] = _textureSlots[i];
            for (var i = n; i < _textureSlotsSnapshotCount; i++)
                _textureSlotsSnapshot[i] = null;
            Volatile.Write(ref _textureSlotsSnapshotCount, n);
            if (n == 0)
            {
                return;
            }
        }
    }
}
