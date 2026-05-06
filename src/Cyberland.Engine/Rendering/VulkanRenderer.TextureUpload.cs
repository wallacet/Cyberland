using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System.Threading;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

public sealed unsafe partial class VulkanRenderer
{
    /// <summary>
    /// Texture upload/staging utilities for renderer-owned texture slots.
    /// </summary>
    private sealed class TextureUpload : IDisposable
    {
        private readonly VulkanRenderer _r;
        private Fence _submitFence;
        private bool _submitFenceCreated;

        public TextureUpload(VulkanRenderer renderer) => _r = renderer;

        public TextureId RegisterTextureRgbaInternal(ReadOnlySpan<byte> rgba, int width, int height, Format format)
        {
            if (_r._vk is null || width <= 0 || height <= 0 || rgba.Length < width * height * 4)
                return TextureId.MaxValue;

            lock (_r._textureSlotsLock)
            {
                if (_r._textureSlots.Count >= VulkanRenderer.MaxRegisteredTextures)
                    return TextureId.MaxValue;

                Image img = default;
                DeviceMemory mem = default;
                ImageView view = default;
                VkBuffer staging = default;
                DeviceMemory stagingMem = default;
                var imageReady = false;
                var descriptorReady = false;

                try
                {
                    _r.CreateDeviceLocalImage((uint)width, (uint)height, format,
                        ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                        out img, out mem, out view);
                    imageReady = true;

                    UploadBuffer(rgba, (ulong)(width * height * 4), out staging, out stagingMem);

                    OneTimeCommands(cmd =>
                    {
                        ImageMemoryBarrier imb = new()
                        {
                            SType = StructureType.ImageMemoryBarrier,
                            OldLayout = ImageLayout.Undefined,
                            NewLayout = ImageLayout.TransferDstOptimal,
                            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                            Image = img,
                            SubresourceRange = new ImageSubresourceRange
                            {
                                AspectMask = ImageAspectFlags.ColorBit,
                                BaseMipLevel = 0,
                                LevelCount = 1,
                                BaseArrayLayer = 0,
                                LayerCount = 1
                            }
                        };

                        _r._vk!.CmdPipelineBarrier(cmd, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &imb);

                        BufferImageCopy region = new()
                        {
                            ImageSubresource = new ImageSubresourceLayers
                            {
                                AspectMask = ImageAspectFlags.ColorBit,
                                MipLevel = 0,
                                BaseArrayLayer = 0,
                                LayerCount = 1
                            },
                            ImageExtent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 }
                        };

                        _r._vk.CmdCopyBufferToImage(cmd, staging, img, ImageLayout.TransferDstOptimal, 1, &region);

                        ImageMemoryBarrier imb2 = imb;
                        imb2.OldLayout = ImageLayout.TransferDstOptimal;
                        imb2.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
                        imb2.SrcAccessMask = AccessFlags.TransferWriteBit;
                        imb2.DstAccessMask = AccessFlags.ShaderReadBit;

                        _r._vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &imb2);
                    });

                    DescriptorSet ds = default;
                    fixed (DescriptorSetLayout* dsl = &_r._dslTexture)
                    {
                        DescriptorSetAllocateInfo ai = new()
                        {
                            SType = StructureType.DescriptorSetAllocateInfo,
                            DescriptorPool = _r._descriptorPool,
                            DescriptorSetCount = 1,
                            PSetLayouts = dsl
                        };

                        if (_r._vk.AllocateDescriptorSets(_r._device, in ai, out ds) != Result.Success)
                            throw new GraphicsInitializationException("alloc tex ds");
                    }

                    DescriptorImageInfo dii = new()
                    {
                        ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                        ImageView = view,
                        Sampler = _r._samplerLinear
                    };

                    WriteDescriptorSet w = new()
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = ds,
                        DstBinding = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        PImageInfo = &dii
                    };

                    _r._vk.UpdateDescriptorSets(_r._device, 1, &w, 0, null);
                    descriptorReady = true;

                    var slot = new GpuTexture(
                        img,
                        mem,
                        view,
                        ds,
                        width,
                        height);
                    var id = (TextureId)_r._textureSlots.Count;
                    _r._textureSlots.Add(slot);
                    _r._textureSlotsSnapshot[(int)id] = slot;
                    Volatile.Write(ref _r._textureSlotsSnapshotCount, _r._textureSlots.Count);
                    return id;
                }
                finally
                {
                    if (staging.Handle != default)
                        _r._vk!.DestroyBuffer(_r._device, staging, null);
                    if (stagingMem.Handle != default)
                        _r._vk!.FreeMemory(_r._device, stagingMem, null);
                    if (imageReady && !descriptorReady)
                        DestroyTextureImage(img, mem, view);
                }
            }
        }

        /// <summary>
        /// Updates a sub-rectangle of an existing image (layout: ShaderReadOnly → TransferDst → copy → ShaderReadOnly).
        /// </summary>
        public bool TryUploadTextureRgbaSubregion(TextureId textureId, int dstX, int dstY, int width, int height,
            ReadOnlySpan<byte> rgba)
        {
            if (_r._vk is null)
                return false;
            if (width <= 0 || height <= 0 || rgba.Length < width * height * 4)
                return false;

            var gt = _r.TryGetTextureSlot(textureId);
            if (gt is null)
                return false;
            if (dstX < 0 || dstY < 0 || dstX + width > gt.Width || dstY + height > gt.Height)
                return false;

            VkBuffer staging = default;
            DeviceMemory stagingMem = default;
            try
            {
                UploadBuffer(rgba, (ulong)(width * height * 4), out staging, out stagingMem);
                var img = gt.Image;

                OneTimeCommands(cmd =>
                {
                    ImageMemoryBarrier toTransfer = new()
                    {
                        SType = StructureType.ImageMemoryBarrier,
                        OldLayout = ImageLayout.ShaderReadOnlyOptimal,
                        NewLayout = ImageLayout.TransferDstOptimal,
                        SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                        DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                        Image = img,
                        SubresourceRange = new ImageSubresourceRange
                        {
                            AspectMask = ImageAspectFlags.ColorBit,
                            BaseMipLevel = 0,
                            LevelCount = 1,
                            BaseArrayLayer = 0,
                            LayerCount = 1
                        },
                        SrcAccessMask = AccessFlags.ShaderReadBit,
                        DstAccessMask = AccessFlags.TransferWriteBit
                    };

                    _r._vk!.CmdPipelineBarrier(cmd, PipelineStageFlags.FragmentShaderBit, PipelineStageFlags.TransferBit, 0,
                        0, null, 0, null, 1, &toTransfer);

                    BufferImageCopy region = new()
                    {
                        BufferOffset = 0,
                        BufferRowLength = 0,
                        BufferImageHeight = 0,
                        ImageSubresource = new ImageSubresourceLayers
                        {
                            AspectMask = ImageAspectFlags.ColorBit,
                            MipLevel = 0,
                            BaseArrayLayer = 0,
                            LayerCount = 1
                        },
                        ImageOffset = new Offset3D { X = dstX, Y = dstY, Z = 0 },
                        ImageExtent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 }
                    };

                    _r._vk!.CmdCopyBufferToImage(cmd, staging, img, ImageLayout.TransferDstOptimal, 1, &region);

                    ImageMemoryBarrier toSample = toTransfer;
                    toSample.OldLayout = ImageLayout.TransferDstOptimal;
                    toSample.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
                    toSample.SrcAccessMask = AccessFlags.TransferWriteBit;
                    toSample.DstAccessMask = AccessFlags.ShaderReadBit;

                    _r._vk!.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0,
                        0, null, 0, null, 1, &toSample);
                });

                return true;
            }
            finally
            {
                if (staging.Handle != default)
                    _r._vk!.DestroyBuffer(_r._device, staging, null);
                if (stagingMem.Handle != default)
                    _r._vk!.FreeMemory(_r._device, stagingMem, null);
            }
        }

        public void UploadBuffer(ReadOnlySpan<byte> data, ulong size, out VkBuffer buf, out DeviceMemory bmem)
        {
            _r.CreateHostVisibleBuffer(size, BufferUsageFlags.TransferSrcBit, out buf, out bmem);
            void* p;
            if (_r._vk!.MapMemory(_r._device, bmem, 0, size, 0, &p) != Result.Success)
                throw new GraphicsInitializationException("map staging");

            data.CopyTo(new Span<byte>(p, (int)size));
            _r._vk.UnmapMemory(_r._device, bmem);
        }

        public void OneTimeCommands(Action<CommandBuffer> record)
        {
            // Texture uploads serialize through one command-at-a-time path. We intentionally share the graphics queue
            // (for now) but isolate transient upload command buffers in their own pool so frame-command lifetime stays separate.
            lock (_r._uploadCommandLock)
            {
                CommandBufferAllocateInfo ai = new()
                {
                    SType = StructureType.CommandBufferAllocateInfo,
                    CommandPool = _r._uploadCommandPool,
                    Level = CommandBufferLevel.Primary,
                    CommandBufferCount = 1
                };

                CommandBuffer cmd = default;
                var commandAllocated = false;
                try
                {
                    EnsureSubmitFence();
                    if (_r._vk!.AllocateCommandBuffers(_r._device, in ai, &cmd) != Result.Success)
                        throw new GraphicsInitializationException("alloc 1cmd");
                    commandAllocated = true;

                    CommandBufferBeginInfo bi = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };

                    if (_r._vk.BeginCommandBuffer(cmd, in bi) != Result.Success)
                        throw new GraphicsInitializationException("begin 1cmd");

                    record(cmd);

                    if (_r._vk.EndCommandBuffer(cmd) != Result.Success)
                        throw new GraphicsInitializationException("end 1cmd");

                    SubmitInfo si = new()
                    {
                        SType = StructureType.SubmitInfo,
                        CommandBufferCount = 1,
                        PCommandBuffers = &cmd
                    };

                    _r._vk.ResetFences(_r._device, 1, in _submitFence);

                    if (_r._vk.QueueSubmit(_r._graphicsQueue, 1, in si, _submitFence) != Result.Success)
                        throw new GraphicsInitializationException("submit 1cmd");

                    _r._vk.WaitForFences(_r._device, 1, in _submitFence, true, ulong.MaxValue);
                }
                finally
                {
                    if (commandAllocated)
                        _r._vk!.FreeCommandBuffers(_r._device, _r._uploadCommandPool, 1, &cmd);
                }
            }
        }

        private void EnsureSubmitFence()
        {
            if (_submitFenceCreated)
                return;
            FenceCreateInfo fci = new() { SType = StructureType.FenceCreateInfo };
            if (_r._vk!.CreateFence(_r._device, in fci, null, out _submitFence) != Result.Success)
                throw new GraphicsInitializationException("create upload fence");
            _submitFenceCreated = true;
        }

        private void DestroyTextureImage(Image img, DeviceMemory mem, ImageView view)
        {
            if (view.Handle != default)
                _r._vk!.DestroyImageView(_r._device, view, null);
            if (img.Handle != default)
                _r._vk!.DestroyImage(_r._device, img, null);
            if (mem.Handle != default)
                _r._vk!.FreeMemory(_r._device, mem, null);
        }

        public void CreateDefaultTextures()
        {
            Span<byte> white = stackalloc byte[4] { 255, 255, 255, 255 };
            _r._whiteTextureId = RegisterTextureRgbaInternal(white, 1, 1, Format.R8G8B8A8Srgb);
            Span<byte> black = stackalloc byte[4] { 0, 0, 0, 255 };
            _r._blackTextureId = RegisterTextureRgbaInternal(black, 1, 1, Format.R8G8B8A8Srgb);
            Span<byte> n = stackalloc byte[4] { 128, 128, 255, 255 };
            _r._defaultNormalTextureId = RegisterTextureRgbaInternal(n, 1, 1, Format.R8G8B8A8Srgb);
        }

        public void Dispose()
        {
            if (!_submitFenceCreated || _r._vk is null)
                return;
            _r._vk.DestroyFence(_r._device, _submitFence, null);
            _submitFence = default;
            _submitFenceCreated = false;
        }
    }

    private TextureId RegisterTextureRgbaInternal(ReadOnlySpan<byte> rgba, int width, int height, Format format)
    {
        _textureUpload ??= new TextureUpload(this);
        return _textureUpload.RegisterTextureRgbaInternal(rgba, width, height, format);
    }

    private bool TryUploadTextureRgbaSubregionInternal(TextureId textureId, int dstX, int dstY, int width, int height,
        ReadOnlySpan<byte> rgba)
    {
        _textureUpload ??= new TextureUpload(this);
        return _textureUpload.TryUploadTextureRgbaSubregion(textureId, dstX, dstY, width, height, rgba);
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
