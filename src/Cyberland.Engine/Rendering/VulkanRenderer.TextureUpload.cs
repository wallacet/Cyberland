using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

public sealed unsafe partial class VulkanRenderer
{
    /// <summary>
    /// Texture upload/staging utilities for renderer-owned texture slots.
    /// </summary>
    private sealed class TextureUpload
    {
        private readonly VulkanRenderer _r;

        public TextureUpload(VulkanRenderer renderer) => _r = renderer;

        public int RegisterTextureRgbaInternal(ReadOnlySpan<byte> rgba, int width, int height)
        {
            if (_r._vk is null || width <= 0 || height <= 0 || rgba.Length < width * height * 4)
                return -1;

            Image img = default;
            DeviceMemory mem = default;
            ImageView view = default;

            _r.CreateDeviceLocalImage((uint)width, (uint)height, Format.R8G8B8A8Srgb,
                ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                out img, out mem, out view);

            UploadBuffer(rgba, (ulong)(width * height * 4), out var staging, out var stagingMem);

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

            _r._vk!.DestroyBuffer(_r._device, staging, null);
            _r._vk.FreeMemory(_r._device, stagingMem, null);

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

            var slot = new GpuTexture
            {
                Image = img,
                Memory = mem,
                View = view,
                DescriptorSet = ds,
                Width = width,
                Height = height
            };

            var id = _r._textureSlots.Count;
            _r._textureSlots.Add(slot);
            return id;
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
            CommandBufferAllocateInfo ai = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _r._commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            CommandBuffer cmd = default;
            if (_r._vk!.AllocateCommandBuffers(_r._device, in ai, &cmd) != Result.Success)
                throw new GraphicsInitializationException("alloc 1cmd");

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

            Fence submitFence = default;
            FenceCreateInfo fci = new() { SType = StructureType.FenceCreateInfo };
            if (_r._vk.CreateFence(_r._device, in fci, null, out submitFence) != Result.Success)
                throw new GraphicsInitializationException("create upload fence");

            if (_r._vk.QueueSubmit(_r._graphicsQueue, 1, in si, submitFence) != Result.Success)
                throw new GraphicsInitializationException("submit 1cmd");

            _r._vk.WaitForFences(_r._device, 1, in submitFence, true, ulong.MaxValue);
            _r._vk.DestroyFence(_r._device, submitFence, null);
            _r._vk.FreeCommandBuffers(_r._device, _r._commandPool, 1, &cmd);
        }

        public void CreateDefaultTextures()
        {
            Span<byte> white = stackalloc byte[4] { 255, 255, 255, 255 };
            _r._whiteTextureId = RegisterTextureRgbaInternal(white, 1, 1);
            Span<byte> black = stackalloc byte[4] { 0, 0, 0, 255 };
            _r._blackTextureId = RegisterTextureRgbaInternal(black, 1, 1);
            Span<byte> n = stackalloc byte[4] { 128, 128, 255, 255 };
            _r._defaultNormalTextureId = RegisterTextureRgbaInternal(n, 1, 1);
        }
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
