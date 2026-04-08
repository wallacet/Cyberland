using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
/// Full swapchain path: instance, KHR surface (via GLFW/<see cref="IVkSurface"/>), device, swapchain,
/// render pass (clear-only), framebuffers, command buffers, acquire → submit → present.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Requires a Vulkan-capable GPU and window surface.")]
public sealed unsafe class VulkanRenderer : IDisposable
{
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

    private RenderPass _renderPass;
    private PipelineLayout _pipelineLayout;
    private Pipeline _graphicsPipeline;
    private ShaderModule _vertShaderModule;
    private ShaderModule _fragShaderModule;
    private VkBuffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;
    private VkBuffer _indexBuffer;
    private DeviceMemory _indexBufferMemory;

    private CommandPool _commandPool;
    private CommandBuffer[]? _commandBuffers;

    private float _spriteCenterX;
    private float _spriteCenterY;
    private float _spriteHalfExtent;

    private Semaphore[]? _imageAvailableSemaphores;
    private Semaphore[]? _renderFinishedSemaphores;
    private Fence[]? _inFlightFences;
    private Fence[]? _imagesInFlight;
    private int _currentFrame;

    public VulkanRenderer(IWindow window) => _window = window;

    /// <summary>Current swapchain size in pixels — use for world-space clamps (matches shader <c>screenSize</c>).</summary>
    public Vector2D<int> SwapchainPixelSize => new((int)_swapchainExtent.Width, (int)_swapchainExtent.Height);

    /// <summary>
    /// Sprite in <see cref="WorldScreenSpace"/> world units (bottom-left origin, +Y up, same pixel scale as the framebuffer).
    /// Converts to screen pixels internally for the draw path.
    /// </summary>
    public void SetSpriteWorld(float centerXWorld, float centerYWorld, float halfExtentWorld)
    {
        var size = SwapchainPixelSize;
        var screen = WorldScreenSpace.WorldCenterToScreenPixel(
            new Vector2D<float>(centerXWorld, centerYWorld),
            size);
        _spriteCenterX = screen.X;
        _spriteCenterY = screen.Y;
        _spriteHalfExtent = halfExtentWorld;
    }

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
        CreateRenderPass();
        CreateGraphicsPipelineAndMesh();
        CreateFramebuffers();
        CreateCommandPool();
        CreateCommandBuffers();
        CreateSyncObjects();

        _window.FramebufferResize += OnFramebufferResize;
    }

    private void OnFramebufferResize(Vector2D<int> _) => _resizePending = true;

    /// <summary>One frame: acquire swapchain image, submit clear pass, present.</summary>
    public void DrawFrame()
    {
        if (_vk is null)
            return;

        if (_resizePending)
        {
            _resizePending = false;
            RecreateSwapchain();
        }

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

        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
    }

    public void Dispose()
    {
        _window.FramebufferResize -= OnFramebufferResize;

        if (_vk is null)
            return;

        if (_device.Handle != default)
            _vk.DeviceWaitIdle(_device);

        CleanupSwapchain();

        DestroyGraphicsPipelineAndMesh();

        if (_renderPass.Handle != default && _device.Handle != default)
            _vk.DestroyRenderPass(_device, _renderPass, null);

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
        var presentMode = ChoosePresentMode(swapChainSupport.PresentModes);
        var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

        var imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
        if (swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.Capabilities.MaxImageCount)
            imageCount = swapChainSupport.Capabilities.MaxImageCount;

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

    private void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = _swapchainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        if (_vk!.CreateRenderPass(_device, in renderPassInfo, null, out _renderPass) != Result.Success)
            throw new GraphicsInitializationException("vkCreateRenderPass failed.");
    }

    private void CreateFramebuffers()
    {
        _swapchainFramebuffers = new Framebuffer[_swapchainImageViews!.Length];

        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            var attachment = _swapchainImageViews[i];

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1
            };

            if (_vk!.CreateFramebuffer(_device, in framebufferInfo, null, out _swapchainFramebuffers[i]) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer failed.");
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

    [StructLayout(LayoutKind.Sequential)]
    private struct SpritePushConstants
    {
        public float CenterX;
        public float CenterY;
        public float HalfW;
        public float HalfH;
        public float ScreenW;
        public float ScreenH;
    }

    private void RecordCommandBuffer(CommandBuffer commandBuffer, Framebuffer framebuffer)
    {
        if (_vk!.ResetCommandBuffer(commandBuffer, 0) != Result.Success)
            throw new InvalidOperationException("vkResetCommandBuffer failed.");

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };

        if (_vk.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success)
            throw new InvalidOperationException("vkBeginCommandBuffer failed.");

        ClearValue clearColor = new()
        {
            Color = new ClearColorValue
            {
                Float32_0 = 0.04f,
                Float32_1 = 0.02f,
                Float32_2 = 0.08f,
                Float32_3 = 1f
            }
        };

        RenderPassBeginInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = framebuffer,
            RenderArea = new Rect2D { Offset = default, Extent = _swapchainExtent },
            ClearValueCount = 1,
            PClearValues = &clearColor
        };

        _vk.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);

        Viewport viewport = new()
        {
            X = 0f,
            Y = 0f,
            Width = _swapchainExtent.Width,
            Height = _swapchainExtent.Height,
            MinDepth = 0f,
            MaxDepth = 1f
        };

        Rect2D scissor = new() { Offset = default, Extent = _swapchainExtent };

        _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        _vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);

        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);

        var vertexBuffers = stackalloc[] { _vertexBuffer };
        var offsets = stackalloc ulong[] { 0 };
        _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffers, offsets);
        _vk.CmdBindIndexBuffer(commandBuffer, _indexBuffer, 0, IndexType.Uint16);

        SpritePushConstants push = new()
        {
            CenterX = _spriteCenterX,
            CenterY = _spriteCenterY,
            HalfW = _spriteHalfExtent,
            HalfH = _spriteHalfExtent,
            ScreenW = _swapchainExtent.Width,
            ScreenH = _swapchainExtent.Height
        };

        _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)sizeof(SpritePushConstants), &push);

        _vk.CmdDrawIndexed(commandBuffer, 6, 1, 0, 0, 0);

        _vk.CmdEndRenderPass(commandBuffer);

        if (_vk.EndCommandBuffer(commandBuffer) != Result.Success)
            throw new InvalidOperationException("vkEndCommandBuffer failed.");
    }

    private void CreateGraphicsPipelineAndMesh()
    {
        uint[] vertSpv;
        uint[] fragSpv;
        try
        {
            vertSpv = GlslSpirvCompiler.CompileGlslToSpirv(
                """
                #version 450
                layout(push_constant) uniform Push {
                    vec2 center;
                    vec2 halfSize;
                    vec2 screenSize;
                } push;
                layout(location = 0) in vec2 inPos;
                void main() {
                    vec2 pixel = push.center + inPos * push.halfSize;
                    float nx = pixel.x / push.screenSize.x * 2.0 - 1.0;
                    // Top-left pixels (y=0 top) → NDC for default viewport (positive height): small pixel y → ndc -1 (top of framebuffer).
                    float ny = pixel.y / push.screenSize.y * 2.0 - 1.0;
                    gl_Position = vec4(nx, ny, 0.0, 1.0);
                }
                """,
                ShaderStage.Vertex);

            fragSpv = GlslSpirvCompiler.CompileGlslToSpirv(
                """
                #version 450
                layout(location = 0) out vec4 outColor;
                void main() {
                    outColor = vec4(0.35, 0.85, 0.45, 1.0);
                }
                """,
                ShaderStage.Fragment);
        }
        catch (InvalidOperationException ex)
        {
            throw new GraphicsInitializationException("Shader compilation to SPIR-V failed.", ex);
        }

        _vertShaderModule = CreateShaderModule(vertSpv);
        _fragShaderModule = CreateShaderModule(fragSpv);

        PushConstantRange pushConstantRange = new()
        {
            StageFlags = ShaderStageFlags.VertexBit,
            Offset = 0,
            Size = (uint)sizeof(SpritePushConstants)
        };

        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstantRange
        };

        if (_vk!.CreatePipelineLayout(_device, in pipelineLayoutInfo, null, out _pipelineLayout) != Result.Success)
            throw new GraphicsInitializationException("vkCreatePipelineLayout failed.");

        var mainName = Marshal.StringToHGlobalAnsi("main");

        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _vertShaderModule,
            PName = (byte*)mainName
        };

        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _fragShaderModule,
            PName = (byte*)mainName
        };

        var shaderStages = stackalloc PipelineShaderStageCreateInfo[2];
        shaderStages[0] = vertShaderStageInfo;
        shaderStages[1] = fragShaderStageInfo;

        VertexInputBindingDescription bindingDescription = new()
        {
            Binding = 0,
            Stride = 2 * sizeof(float),
            InputRate = VertexInputRate.Vertex
        };

        VertexInputAttributeDescription attributeDescription = new()
        {
            Binding = 0,
            Location = 0,
            Format = Format.R32G32Sfloat,
            Offset = 0
        };

        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 1,
            PVertexBindingDescriptions = &bindingDescription,
            VertexAttributeDescriptionCount = 1,
            PVertexAttributeDescriptions = &attributeDescription
        };

        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false
        };

        PipelineViewportStateCreateInfo viewportState = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            ScissorCount = 1
        };

        PipelineRasterizationStateCreateInfo rasterizer = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1f,
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.CounterClockwise,
            DepthBiasEnable = false
        };

        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };

        PipelineColorBlendAttachmentState colorBlendAttachment = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            BlendEnable = false
        };

        PipelineColorBlendStateCreateInfo colorBlending = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment
        };

        DynamicState[] dynamicStates = [DynamicState.Viewport, DynamicState.Scissor];
        fixed (DynamicState* pDynamicStates = dynamicStates)
        {
            PipelineDynamicStateCreateInfo dynamicState = new()
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length,
                PDynamicStates = pDynamicStates
            };

            GraphicsPipelineCreateInfo pipelineInfo = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PTessellationState = null,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = null,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
                BasePipelineHandle = default,
                BasePipelineIndex = -1
            };

            if (_vk!.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out _graphicsPipeline) != Result.Success)
                throw new GraphicsInitializationException("vkCreateGraphicsPipelines failed.");
        }

        Marshal.FreeHGlobal(mainName);

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

        void* data;
        if (_vk.MapMemory(_device, _vertexBufferMemory, 0, (ulong)(vertices.Length * sizeof(float)), 0, &data) != Result.Success)
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

    private void DestroyGraphicsPipelineAndMesh()
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

        if (_graphicsPipeline.Handle != default)
        {
            _vk.DestroyPipeline(_device, _graphicsPipeline, null);
            _graphicsPipeline = default;
        }

        if (_pipelineLayout.Handle != default)
        {
            _vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
            _pipelineLayout = default;
        }

        if (_vertShaderModule.Handle != default)
        {
            _vk.DestroyShaderModule(_device, _vertShaderModule, null);
            _vertShaderModule = default;
        }

        if (_fragShaderModule.Handle != default)
        {
            _vk.DestroyShaderModule(_device, _fragShaderModule, null);
            _fragShaderModule = default;
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
        CreateFramebuffers();
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

    private static PresentModeKHR ChoosePresentMode(IReadOnlyList<PresentModeKHR> availablePresentModes)
    {
        foreach (var availablePresentMode in availablePresentModes)
        {
            if (availablePresentMode == PresentModeKHR.MailboxKhr)
                return availablePresentMode;
        }

        return PresentModeKHR.FifoKhr;
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
}
