using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

public sealed unsafe partial class VulkanRenderer
{
    /// <summary>
    /// Manages offscreen color target images/views/framebuffers for emissive/hdr/bloom chains.
    /// </summary>
    private sealed class OffscreenTargets
    {
        private readonly VulkanRenderer _r;

        public OffscreenTargets(VulkanRenderer renderer) => _r = renderer;

        public void CreateDeviceLocalImage2D(uint w, uint h, Format format, ImageUsageFlags usage, out Image img, out DeviceMemory mem, out ImageView view)
        {
            ImageCreateInfo ici = new()
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D { Width = w, Height = h, Depth = 1 },
                MipLevels = 1,
                ArrayLayers = 1,
                Format = format,
                Tiling = ImageTiling.Optimal,
                InitialLayout = ImageLayout.Undefined,
                Usage = usage,
                Samples = SampleCountFlags.Count1Bit,
                SharingMode = SharingMode.Exclusive
            };

            if (_r._vk!.CreateImage(_r._device, in ici, null, out img) != Result.Success)
                throw new GraphicsInitializationException("vkCreateImage failed.");

            _r._vk.GetImageMemoryRequirements(_r._device, img, out var req);

            MemoryAllocateInfo mai = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = req.Size,
                MemoryTypeIndex = _r.FindMemoryTypeDeviceLocal(req.MemoryTypeBits)
            };

            if (_r._vk.AllocateMemory(_r._device, in mai, null, out mem) != Result.Success)
                throw new GraphicsInitializationException("vkAllocateMemory (image) failed.");

            _r._vk.BindImageMemory(_r._device, img, mem, 0);

            ImageViewCreateInfo ivci = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = img,
                ViewType = ImageViewType.Type2D,
                Format = format,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (_r._vk.CreateImageView(_r._device, in ivci, null, out view) != Result.Success)
                throw new GraphicsInitializationException("vkCreateImageView failed.");
        }

        public void CreateOffscreenImagesAndFramebuffers()
        {
            var w = _r._swapchainExtent.Width;
            var h = _r._swapchainExtent.Height;

            CreateDeviceLocalImage2D(w, h, HdrFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgEmissive, out _r._memEmissive, out _r._viewEmissive);

            CreateDeviceLocalImage2D(w, h, HdrFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgHdr, out _r._memHdr, out _r._viewHdr);

            ImageView emAtt = _r._viewEmissive;
            FramebufferCreateInfo fb1 = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _r._rpOffscreenInitialUndefined,
                AttachmentCount = 1,
                PAttachments = &emAtt,
                Width = w,
                Height = h,
                Layers = 1
            };

            if (_r._vk!.CreateFramebuffer(_r._device, in fb1, null, out _r._fbEmissive) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer emissive failed.");

            ImageView hdrAtt = _r._viewHdr;
            FramebufferCreateInfo fb2 = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _r._rpOffscreenInitialUndefined,
                AttachmentCount = 1,
                PAttachments = &hdrAtt,
                Width = w,
                Height = h,
                Layers = 1
            };

            if (_r._vk.CreateFramebuffer(_r._device, in fb2, null, out _r._fbHdr) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer hdr failed.");

            CreateDeviceLocalImage2D(w, h, HdrFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgGbuf0, out _r._memGbuf0, out _r._viewGbuf0);
            CreateDeviceLocalImage2D(w, h, HdrFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgGbuf1, out _r._memGbuf1, out _r._viewGbuf1);

            var gViews = stackalloc ImageView[2];
            gViews[0] = _r._viewGbuf0;
            gViews[1] = _r._viewGbuf1;
            FramebufferCreateInfo fbG = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _r._rpGbufferUndefined,
                AttachmentCount = 2,
                PAttachments = gViews,
                Width = w,
                Height = h,
                Layers = 1
            };
            if (_r._vk.CreateFramebuffer(_r._device, in fbG, null, out _r._fbGbuffer) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer gbuffer failed.");

            CreateDeviceLocalImage2D(w, h, HdrFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgWAccum, out _r._memWAccum, out _r._viewWAccum);
            CreateDeviceLocalImage2D(w, h, WboitRevealFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgWReveal, out _r._memWReveal, out _r._viewWReveal);

            var wViews = stackalloc ImageView[2];
            wViews[0] = _r._viewWAccum;
            wViews[1] = _r._viewWReveal;
            FramebufferCreateInfo fbW = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _r._rpWboitUndefined,
                AttachmentCount = 2,
                PAttachments = wViews,
                Width = w,
                Height = h,
                Layers = 1
            };
            if (_r._vk.CreateFramebuffer(_r._device, in fbW, null, out _r._fbWboit) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer wboit failed.");

            CreateDeviceLocalImage2D(w, h, HdrFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgHdrComposite, out _r._memHdrComposite, out _r._viewHdrComposite);

            ImageView hcAtt = _r._viewHdrComposite;
            FramebufferCreateInfo fbHc = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _r._rpOffscreenInitialUndefined,
                AttachmentCount = 1,
                PAttachments = &hcAtt,
                Width = w,
                Height = h,
                Layers = 1
            };
            if (_r._vk.CreateFramebuffer(_r._device, in fbHc, null, out _r._fbHdrComposite) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer hdr composite failed.");

            CreateBloomHalfResTargets();
            _r.ResetOffscreenAttachmentWrittenFlags();
        }

        public void CreateBloomHalfResTargets()
        {
            _r._bloomHalfW = Math.Max(_r._swapchainExtent.Width / 2, 1u);
            _r._bloomHalfH = Math.Max(_r._swapchainExtent.Height / 2, 1u);

            CreateDeviceLocalImage2D(_r._bloomHalfW, _r._bloomHalfH, HdrFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgBloom0, out _r._memBloom0, out _r._viewBloom0);

            CreateDeviceLocalImage2D(_r._bloomHalfW, _r._bloomHalfH, HdrFormat,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                out _r._imgBloom1, out _r._memBloom1, out _r._viewBloom1);

            ImageView att0 = _r._viewBloom0;
            FramebufferCreateInfo fb0 = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _r._rpOffscreenInitialUndefined,
                AttachmentCount = 1,
                PAttachments = &att0,
                Width = _r._bloomHalfW,
                Height = _r._bloomHalfH,
                Layers = 1
            };

            if (_r._vk!.CreateFramebuffer(_r._device, in fb0, null, out _r._fbBloom0) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer bloom0 failed.");

            ImageView att1 = _r._viewBloom1;
            FramebufferCreateInfo fb1 = fb0;
            fb1.PAttachments = &att1;

            if (_r._vk.CreateFramebuffer(_r._device, in fb1, null, out _r._fbBloom1) != Result.Success)
                throw new GraphicsInitializationException("vkCreateFramebuffer bloom1 failed.");

            var srcW = _r._bloomHalfW;
            var srcH = _r._bloomHalfH;
            for (var i = 0; i < BloomDownsampleLevels; i++)
            {
                _r._bloomDownW[i] = Math.Max(srcW / 2, 1u);
                _r._bloomDownH[i] = Math.Max(srcH / 2, 1u);
                srcW = _r._bloomDownW[i];
                srcH = _r._bloomDownH[i];

                CreateDeviceLocalImage2D(_r._bloomDownW[i], _r._bloomDownH[i], HdrFormat,
                    ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
                    out _r._imgBloomDown[i], out _r._memBloomDown[i], out _r._viewBloomDown[i]);

                ImageView downAtt = _r._viewBloomDown[i];
                FramebufferCreateInfo fbDown = new()
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = _r._rpOffscreenInitialUndefined,
                    AttachmentCount = 1,
                    PAttachments = &downAtt,
                    Width = _r._bloomDownW[i],
                    Height = _r._bloomDownH[i],
                    Layers = 1
                };

                if (_r._vk.CreateFramebuffer(_r._device, in fbDown, null, out _r._fbBloomDown[i]) != Result.Success)
                    throw new GraphicsInitializationException($"vkCreateFramebuffer bloom down[{i}] failed.");
            }
        }

        public void Recreate2DOffscreenTargets()
        {
            for (var i = 0; i < BloomDownsampleLevels; i++)
                DestroyOffscreenFramebuffer(ref _r._fbBloomDown[i], ref _r._viewBloomDown[i], ref _r._imgBloomDown[i], ref _r._memBloomDown[i]);
            DestroyOffscreenFramebuffer(ref _r._fbBloom0, ref _r._viewBloom0, ref _r._imgBloom0, ref _r._memBloom0);
            DestroyOffscreenFramebuffer(ref _r._fbBloom1, ref _r._viewBloom1, ref _r._imgBloom1, ref _r._memBloom1);
            DestroyGbufferAndWboitAndComposite();
            DestroyOffscreenFramebuffer(ref _r._fbEmissive, ref _r._viewEmissive, ref _r._imgEmissive, ref _r._memEmissive);
            DestroyOffscreenFramebuffer(ref _r._fbHdr, ref _r._viewHdr, ref _r._imgHdr, ref _r._memHdr);
            CreateOffscreenImagesAndFramebuffers();
            _r.UpdateCompositeDescriptorSet();
            _r.UpdateEmissiveSceneDescriptorSet();
            _r.UpdateBloomExtractDescriptorSet();
            _r.UpdateBloomGaussianDescriptorSets();
            _r.UpdateDeferredGbufferAndResolveDescriptorSets();
        }

        public void DestroyGbufferAndWboitAndComposite()
        {
            if (_r._fbGbuffer.Handle != default)
            {
                _r._vk!.DestroyFramebuffer(_r._device, _r._fbGbuffer, null);
                _r._fbGbuffer = default;
            }
            DestroyImageOnly(ref _r._viewGbuf0, ref _r._imgGbuf0, ref _r._memGbuf0);
            DestroyImageOnly(ref _r._viewGbuf1, ref _r._imgGbuf1, ref _r._memGbuf1);

            if (_r._fbWboit.Handle != default)
            {
                _r._vk!.DestroyFramebuffer(_r._device, _r._fbWboit, null);
                _r._fbWboit = default;
            }
            DestroyImageOnly(ref _r._viewWAccum, ref _r._imgWAccum, ref _r._memWAccum);
            DestroyImageOnly(ref _r._viewWReveal, ref _r._imgWReveal, ref _r._memWReveal);

            DestroyOffscreenFramebuffer(ref _r._fbHdrComposite, ref _r._viewHdrComposite, ref _r._imgHdrComposite, ref _r._memHdrComposite);
        }

        private void DestroyImageOnly(ref ImageView view, ref Image img, ref DeviceMemory mem)
        {
            if (_r._vk is null)
                return;

            if (view.Handle != default)
            {
                _r._vk.DestroyImageView(_r._device, view, null);
                view = default;
            }

            if (img.Handle != default)
            {
                _r._vk.DestroyImage(_r._device, img, null);
                img = default;
            }

            if (mem.Handle != default)
            {
                _r._vk.FreeMemory(_r._device, mem, null);
                mem = default;
            }
        }

        public void DestroyOffscreenFramebuffer(ref Framebuffer fb, ref ImageView view, ref Image img, ref DeviceMemory mem)
        {
            if (_r._vk is null)
                return;

            if (fb.Handle != default)
            {
                _r._vk.DestroyFramebuffer(_r._device, fb, null);
                fb = default;
            }

            if (view.Handle != default)
            {
                _r._vk.DestroyImageView(_r._device, view, null);
                view = default;
            }

            if (img.Handle != default)
            {
                _r._vk.DestroyImage(_r._device, img, null);
                img = default;
            }

            if (mem.Handle != default)
            {
                _r._vk.FreeMemory(_r._device, mem, null);
                mem = default;
            }
        }
    }
}
