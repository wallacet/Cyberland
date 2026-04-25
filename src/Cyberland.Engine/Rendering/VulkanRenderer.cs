using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
using Semaphore = Silk.NET.Vulkan.Semaphore;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Production <see cref="IRenderer"/> implementation: Vulkan swapchain, HDR offscreen targets, deferred lighting, emissive prepass,
/// weighted-blended transparency, bloom, and tonemapped composite to sRGB.
/// </summary>
/// <remarks>
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

    private static readonly string[] DeviceExtensions = ["VK_KHR_swapchain"];

    private readonly IWindow _window;
    private bool _resizePending;

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

    private VkBuffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;
    private VkBuffer _indexBuffer;
    private DeviceMemory _indexBufferMemory;

    private CommandPool _commandPool;
    private CommandBuffer[]? _commandBuffers;

    private readonly ConcurrentQueue<SpriteDrawRequest> _spriteQueue = new();
    private readonly ConcurrentQueue<PointLight> _pointLightQueue = new();
    private readonly ConcurrentQueue<SpotLight> _spotLightQueue = new();
    private readonly ConcurrentQueue<DirectionalLight> _directionalLightQueue = new();
    private readonly ConcurrentQueue<AmbientLight> _ambientLightQueue = new();
    private readonly ConcurrentQueue<PostProcessVolumeSubmission> _volumeQueue = new();
    private readonly ConcurrentQueue<CameraViewRequest> _cameraQueue = new();

    // Grow-only snapshots for FramePlanBuilder.Build — reused each frame to avoid List.ToArray / per-frame int[] allocs.
    private SpriteDrawRequest[]? _frameScratchSprites;
    private PointLight[]? _frameScratchPointLights;
    private SpotLight[]? _frameScratchSpotLights;
    private DirectionalLight[]? _frameScratchDirectionalLights;
    private AmbientLight[]? _frameScratchAmbientLights;
    private PostProcessVolumeSubmission[]? _frameScratchVolumes;
    private CameraViewRequest[]? _frameScratchCameras;
    private int[]? _frameScratchSortIndices;

    // Selected camera viewport size for the NEXT frame; resolved under _recordLock so mod systems can read
    // ActiveCameraViewportSize safely from parallel workers and layout against a stable value even if multiple
    // cameras are submitted concurrently. Initialized to swapchain size so the first frame's viewport anchors
    // behave like the pre-camera default.
    private Vector2D<int> _activeCameraViewportSize;
    private CameraViewRequest _activeCameraView;
    private readonly object _cameraStateLock = new();
    private readonly object _globalPostLock = new();

    private GlobalPostProcessSettings _globalPost = new()
    {
        BloomEnabled = true,
        BloomRadius = 1.1f,
        BloomGain = 0.35f,
        BloomExtractThreshold = 0.32f,
        BloomExtractKnee = 0.5f,
        EmissiveToHdrGain = 0.45f,
        EmissiveToBloomGain = 0.45f,
        Exposure = 1f,
        Saturation = 1f,
        TonemapEnabled = true,
        ColorGradingShadows = new Silk.NET.Maths.Vector3D<float>(1f, 1f, 1f),
        ColorGradingMidtones = new Silk.NET.Maths.Vector3D<float>(1f, 1f, 1f),
        ColorGradingHighlights = new Silk.NET.Maths.Vector3D<float>(1f, 1f, 1f)
    };

    private readonly List<GpuTexture> _textureSlots = new();
    private readonly object _textureSlotsLock = new();
    private readonly object _uploadCommandLock = new();
    private TextureId _whiteTextureId = TextureId.MaxValue;
    private TextureId _blackTextureId = TextureId.MaxValue;
    private TextureId _defaultNormalTextureId = TextureId.MaxValue;

    /// <summary>True when swapchain uses an sRGB image format so the composite shader outputs linear and avoids double gamma.</summary>
    private bool _swapchainUsesSrgbFramebuffer;

    /// <summary>Optional hook assigned by the host; mods may invoke to request a clean window close.</summary>
    public Action? RequestClose { get; set; }

    private FramePacing _framePacing = FramePacing.VSync;
    private readonly Stopwatch _limitedFrameTimer = new();

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

    TextureId IRenderer.RegisterTextureRgba(ReadOnlySpan<byte> rgba, int width, int height) =>
        RegisterTextureRgbaInternal(rgba, width, height);

    bool IRenderer.TryUploadTextureRgbaSubregion(TextureId textureId, int dstX, int dstY, int width, int height,
        ReadOnlySpan<byte> rgba) =>
        TryUploadTextureRgbaSubregionInternal(textureId, dstX, dstY, width, height, rgba);

    TextureId IRenderer.DefaultNormalTextureId => _defaultNormalTextureId;

    TextureId IRenderer.WhiteTextureId => _whiteTextureId;

    void IRenderer.SubmitSprite(in SpriteDrawRequest draw)
    {
        _spriteQueue.Enqueue(draw);
    }

    void IRenderer.SubmitSprites(ReadOnlySpan<SpriteDrawRequest> draws)
    {
        if (draws.Length == 0)
            return;
        foreach (ref readonly var d in draws)
            _spriteQueue.Enqueue(d);
    }

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
    /// Creates instance, device, swapchain, render passes, pipelines, and default textures. Throws <see cref="GraphicsInitializationException"/> if the surface is unavailable.
    /// </summary>
    public void Initialize()
    {
        if (!_window.IsInitialized)
            _window.Initialize();

        if (((IVkSurfaceSource)_window).VkSurface is null)
            throw new GraphicsInitializationException(
                "The window does not expose a Vulkan surface (wrong window backend or initialization order).");

        _vk = Vk.GetApi();
        CreateInstance();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapchain();
        CreateImageViews();
        CreateSpriteQuadMesh();
        CreateCommandPool();
        CreateCommandBuffers();
        CreateSyncObjects();

        CreateGraphicsPipelineAndSurfaces();
        CreateDefaultTextures();

        _window.FramebufferResize += OnFramebufferResize;
    }

    private void OnFramebufferResize(Vector2D<int> _) => _resizePending = true;

    /// <summary>Records and submits GPU work for the current pending sprite/light queues, then presents to the screen.</summary>
    public void DrawFrame()
    {
        if (_vk is null)
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

        var waitSemaphores = stackalloc[] { _imageAvailableSemaphores[_currentFrame] };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        var buffer = _commandBuffers![_currentFrame];
        RecordCommandBuffer(buffer, _swapchainFramebuffers![imageIndex]);

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

        if (_vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]) != Result.Success)
            throw new InvalidOperationException("QueueSubmit failed.");

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

        if (_device.Handle != default)
            _vk.DestroyDevice(_device, null);

        if (_surface.Handle != default)
            _khrSurface?.DestroySurface(_instance, _surface, null);

        if (_instance.Handle != default)
            _vk.DestroyInstance(_instance, null);

        _vk.Dispose();
        _vk = null;
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
    }

    private static string[] GetInstanceExtensions(IVkSurface vkSurface)
    {
        var glfwExtensions = vkSurface.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);
        return extensions;
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
        foreach (var device in devices)
        {
            if (IsDeviceSuitable(device))
            {
                _physicalDevice = device;
                return;
            }
        }

        throw new GraphicsInitializationException(
            "No suitable Vulkan GPU was found (graphics + present queues, swapchain support, and required device extensions).");
    }

    private bool IsDeviceSuitable(PhysicalDevice device)
    {
        var indices = FindQueueFamilies(device);
        if (!indices.IsComplete())
            return false;

        if (!CheckDeviceExtensionSupport(device))
            return false;

        var swapChainSupport = QuerySwapChainSupport(device);
        return swapChainSupport.Formats.Length > 0 && swapChainSupport.PresentModes.Length > 0;
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
    }

    private void RecordCommandBuffer(CommandBuffer commandBuffer, Framebuffer framebuffer) =>
        RecordFullFrame(commandBuffer, framebuffer);

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
    }

    private ShaderModule CreateShaderModule(uint[] code)
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
        CreateImageViews();
        RecreateSwapchainDependent();
        CreateCommandBuffers();

        _imagesInFlight = new Fence[_swapchainImages!.Length];
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

    private GpuTexture? TryGetTextureSlot(TextureId id)
    {
        lock (_textureSlotsLock)
            return id < (TextureId)_textureSlots.Count ? _textureSlots[(int)id] : null;
    }
}
