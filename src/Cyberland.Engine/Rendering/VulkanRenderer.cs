using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Full swapchain path: instance, KHR surface (via GLFW/<see cref="IVkSurface"/>), device, swapchain,
/// render pass (clear-only), framebuffers, command buffers, acquire → submit → present.
/// </summary>
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
    private CommandPool _commandPool;
    private CommandBuffer[]? _commandBuffers;

    private Semaphore[]? _imageAvailableSemaphores;
    private Semaphore[]? _renderFinishedSemaphores;
    private Fence[]? _inFlightFences;
    private Fence[]? _imagesInFlight;
    private int _currentFrame;

    public VulkanRenderer(IWindow window) => _window = window;

    public void Initialize()
    {
        if (!_window.IsInitialized)
            _window.Initialize();

        if (((IVkSurfaceSource)_window).VkSurface is null)
            throw new InvalidOperationException("Window does not expose a Vulkan surface (wrong window backend or Initialize order).");

        _vk = Vk.GetApi();
        CreateInstance();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapchain();
        CreateImageViews();
        CreateRenderPass();
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
        var buffer = _commandBuffers![imageIndex];
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

        _vk.DeviceWaitIdle(_device);

        CleanupSwapchain();

        if (_renderPass.Handle != default)
            _vk.DestroyRenderPass(_device, _renderPass, null);

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            _vk.DestroySemaphore(_device, _renderFinishedSemaphores![i], null);
            _vk.DestroySemaphore(_device, _imageAvailableSemaphores![i], null);
            _vk.DestroyFence(_device, _inFlightFences![i], null);
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

        if (_vk!.CreateInstance(in createInfo, null, out _instance) != Result.Success)
            throw new InvalidOperationException("vkCreateInstance failed.");

        Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
            throw new InvalidOperationException("VK_KHR_surface not available.");
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

        throw new InvalidOperationException("No suitable Vulkan physical device found.");
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
                throw new InvalidOperationException("vkCreateDevice failed.");

            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
        }

        _vk.GetDeviceQueue(_device, indices.GraphicsFamily!.Value, 0, out _graphicsQueue);
        _vk.GetDeviceQueue(_device, indices.PresentFamily!.Value, 0, out _presentQueue);

        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
            throw new InvalidOperationException("VK_KHR_swapchain not available.");
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
            throw new InvalidOperationException("vkCreateSwapchainKHR failed.");

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
                throw new InvalidOperationException("vkCreateImageView failed.");
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
            throw new InvalidOperationException("vkCreateRenderPass failed.");
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
                throw new InvalidOperationException("vkCreateFramebuffer failed.");
        }
    }

    private void CreateCommandPool()
    {
        var queueFamilyIndex = FindQueueFamilies(_physicalDevice).GraphicsFamily!.Value;

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queueFamilyIndex
        };

        if (_vk!.CreateCommandPool(_device, in poolInfo, null, out _commandPool) != Result.Success)
            throw new InvalidOperationException("vkCreateCommandPool failed.");
    }

    private void CreateCommandBuffers()
    {
        _commandBuffers = new CommandBuffer[_swapchainFramebuffers!.Length];

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
                throw new InvalidOperationException("vkAllocateCommandBuffers failed.");
        }

        for (var i = 0; i < _commandBuffers.Length; i++)
            RecordCommandBuffer(_commandBuffers[i], _swapchainFramebuffers[i]);
    }

    private void RecordCommandBuffer(CommandBuffer commandBuffer, Framebuffer framebuffer)
    {
        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };

        if (_vk!.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success)
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
        _vk.CmdEndRenderPass(commandBuffer);

        if (_vk.EndCommandBuffer(commandBuffer) != Result.Success)
            throw new InvalidOperationException("vkEndCommandBuffer failed.");
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
                throw new InvalidOperationException("Failed to create synchronization objects.");
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
