using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

public sealed unsafe partial class VulkanRenderer
{
    /// <summary>
    /// Bloom pass orchestration helper to keep frame recording readable.
    /// </summary>
    private sealed class BloomPipeline
    {
        private readonly VulkanRenderer _r;
        // Pyramid single-texture descriptors: _dsBloomDownSrc[0]==bloom0, [i+1]==bloomDown[i] (downsample / blur).
        // bloom1 image is full half-res; Gaussian blur samples with UV offsets. RenderPass RenderArea for fbBloom1 must
        // cover the full attachment so clear wipes stale texels—partial clear left old bloom in the tail and caused offset ghosts.

        public BloomPipeline(VulkanRenderer renderer) => _r = renderer;

        public void Record(CommandBuffer cmd, bool bloomOn, float bloomRadius, float emissiveToBloomGain, Viewport vpHalf, Rect2D sciHalf, out ImageView bloomFinalView)
        {
            bloomFinalView = _r._viewBloom0;
            if (!bloomOn)
            {
                ClearValue cBl = new()
                {
                    Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
                };

                RenderPassBeginInfo rpBl = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloom0),
                    Framebuffer = _r._fbBloom0,
                    RenderArea = sciHalf,
                    ClearValueCount = 1,
                    PClearValues = &cBl
                };

                _r._vk!.CmdBeginRenderPass(cmd, &rpBl, SubpassContents.Inline);
                _r._vk.CmdEndRenderPass(cmd);
                _r._offsWrittenBloom0 = true;
                return;
            }

            var radiusScale = GetBloomGaussianRadiusScale(bloomRadius);

            RecordPrefilter(cmd, vpHalf, sciHalf, emissiveToBloomGain);
            RecordDownsampleChain(cmd);
            RecordSmallestBlur(cmd, radiusScale);
            RecordUpsampleAndRecombineBlur(cmd, radiusScale, vpHalf, sciHalf);
            bloomFinalView = _r._viewBloom0;
        }

        private void RecordPrefilter(CommandBuffer cmd, Viewport vpHalf, Rect2D sciHalf, float emissiveToBloomGain)
        {
            ClearValue cEx = new()
            {
                Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
            };

            RenderPassBeginInfo rpEx = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloom0),
                Framebuffer = _r._fbBloom0,
                RenderArea = sciHalf,
                ClearValueCount = 1,
                PClearValues = &cEx
            };

            _r._vk!.CmdBeginRenderPass(cmd, &rpEx, SubpassContents.Inline);
            _r._vk.CmdSetViewport(cmd, 0, 1, &vpHalf);
            _r._vk.CmdSetScissor(cmd, 0, 1, &sciHalf);
            _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomExtract);
            fixed (DescriptorSet* dsBe = &_r._dsBloomExtract)
            {
                _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomExtract, 0, 1, dsBe, 0, null);
            }

            var bePush = new BloomExtractPush
            {
                Threshold = 0.32f,
                Knee = 0.5f,
                EmissiveBloomGain = emissiveToBloomGain,
                Pad0 = 0f
            };

            _r._vk.CmdPushConstants(cmd, _r._plBloomExtract, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BloomExtractPush), &bePush);
            _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
            _r._vk.CmdEndRenderPass(cmd);
            _r._offsWrittenBloom0 = true;
            InsertWriteToSampleBarrier(cmd);
        }

        private void RecordDownsampleChain(CommandBuffer cmd)
        {
            for (var i = 0; i < BloomDownsampleLevels; i++)
            {
                Viewport vpDown = new()
                {
                    X = 0f,
                    Y = 0f,
                    Width = _r._bloomDownW[i],
                    Height = _r._bloomDownH[i],
                    MinDepth = 0f,
                    MaxDepth = 1f
                };
                Rect2D sciDown = new()
                {
                    Offset = default,
                    Extent = new Extent2D { Width = _r._bloomDownW[i], Height = _r._bloomDownH[i] }
                };
                ClearValue cDown = new()
                {
                    Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f }
                };

                RenderPassBeginInfo rpDown = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloomDown[i]),
                    Framebuffer = _r._fbBloomDown[i],
                    RenderArea = sciDown,
                    ClearValueCount = 1,
                    PClearValues = &cDown
                };

                _r._vk!.CmdBeginRenderPass(cmd, &rpDown, SubpassContents.Inline);
                _r._vk.CmdSetViewport(cmd, 0, 1, &vpDown);
                _r._vk.CmdSetScissor(cmd, 0, 1, &sciDown);
                _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomDownsample);
                fixed (DescriptorSet* dsDn = &_r._dsBloomDownSrc[i])
                {
                    // Downsample i reads previous level: i==0 reads half-res bloom0, otherwise reads bloomDown[i-1].
                    _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomDownsample, 0, 1, dsDn, 0, null);
                }
                var srcW = i == 0 ? _r._bloomHalfW : _r._bloomDownW[i - 1];
                var srcH = i == 0 ? _r._bloomHalfH : _r._bloomDownH[i - 1];
                var dsPush = new BloomResamplePush
                {
                    SrcW = srcW,
                    SrcH = srcH,
                    DstW = _r._bloomDownW[i],
                    DstH = _r._bloomDownH[i],
                    FineBlend = 0f
                };
                _r._vk.CmdPushConstants(cmd, _r._plBloomDownsample, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BloomResamplePush), &dsPush);

                _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
                _r._vk.CmdEndRenderPass(cmd);
                _r._offsWrittenBloomDown[i] = true;
                InsertWriteToSampleBarrier(cmd);
            }
        }

        private void RecordSmallestBlur(CommandBuffer cmd, float radiusScale)
        {
            Rect2D sciBloom1Full = new()
            {
                Offset = default,
                Extent = new Extent2D { Width = _r._bloomHalfW, Height = _r._bloomHalfH }
            };

            var smallest = BloomDownsampleLevels - 1;
            Viewport vpSmall = new()
            {
                X = 0f,
                Y = 0f,
                Width = _r._bloomDownW[smallest],
                Height = _r._bloomDownH[smallest],
                MinDepth = 0f,
                MaxDepth = 1f
            };
            Rect2D sciSmall = new()
            {
                Offset = default,
                Extent = new Extent2D { Width = _r._bloomDownW[smallest], Height = _r._bloomDownH[smallest] }
            };

            for (var p = 0; p < BloomBlurPingPongs; p++)
            {
                ClearValue cGh = new() { Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f } };
                RenderPassBeginInfo rpGh = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloom1),
                    Framebuffer = _r._fbBloom1,
                    RenderArea = sciBloom1Full,
                    ClearValueCount = 1,
                    PClearValues = &cGh
                };

                _r._vk!.CmdBeginRenderPass(cmd, &rpGh, SubpassContents.Inline);
                _r._vk.CmdSetViewport(cmd, 0, 1, &vpSmall);
                _r._vk.CmdSetScissor(cmd, 0, 1, &sciSmall);
                _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomGaussian);
                fixed (DescriptorSet* dsG0 = &_r._dsBloomDownSrc[smallest + 1])
                {
                    // Smallest blur starts from smallest downsample level.
                    _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomGaussian, 0, 1, dsG0, 0, null);
                }
                var gPushH = new BloomGaussianPush { DirX = 1f, DirY = 0f, RadiusScale = radiusScale, Pad = 0f };
                _r._vk.CmdPushConstants(cmd, _r._plBloomGaussian, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BloomGaussianPush), &gPushH);
                _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
                _r._vk.CmdEndRenderPass(cmd);
                _r._offsWrittenBloom1 = true;
                InsertWriteToSampleBarrier(cmd);

                ClearValue cGv = cGh;
                RenderPassBeginInfo rpGv = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloomDown[smallest]),
                    Framebuffer = _r._fbBloomDown[smallest],
                    RenderArea = sciSmall,
                    ClearValueCount = 1,
                    PClearValues = &cGv
                };

                _r._vk.CmdBeginRenderPass(cmd, &rpGv, SubpassContents.Inline);
                _r._vk.CmdSetViewport(cmd, 0, 1, &vpSmall);
                _r._vk.CmdSetScissor(cmd, 0, 1, &sciSmall);
                _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomGaussian);
                fixed (DescriptorSet* dsG1 = &_r._dsBloomGaussianSrcBloom1)
                {
                    _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomGaussian, 0, 1, dsG1, 0, null);
                }
                var gPushV = new BloomGaussianPush { DirX = 0f, DirY = 1f, RadiusScale = radiusScale, Pad = 0f };
                _r._vk.CmdPushConstants(cmd, _r._plBloomGaussian, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BloomGaussianPush), &gPushV);
                _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
                _r._vk.CmdEndRenderPass(cmd);
                _r._offsWrittenBloomDown[smallest] = true;
                InsertWriteToSampleBarrier(cmd);
            }
        }

        private void RecordUpsampleAndRecombineBlur(CommandBuffer cmd, float radiusScale, Viewport vpHalf, Rect2D sciHalf)
        {
            Rect2D sciBloom1Full = new()
            {
                Offset = default,
                Extent = new Extent2D { Width = _r._bloomHalfW, Height = _r._bloomHalfH }
            };

            for (var i = BloomDownsampleLevels - 2; i >= 0; i--)
            {
                Viewport vpUp = new()
                {
                    X = 0f,
                    Y = 0f,
                    Width = _r._bloomDownW[i],
                    Height = _r._bloomDownH[i],
                    MinDepth = 0f,
                    MaxDepth = 1f
                };
                Rect2D sciUp = new()
                {
                    Offset = default,
                    Extent = new Extent2D { Width = _r._bloomDownW[i], Height = _r._bloomDownH[i] }
                };
                ClearValue cUp = new() { Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f } };

                // Upsample coarse mip with a 9-tap tent to bloom1 (temp), then copy to down[i]. fineBlend stays 0: adding the prior fine mip
                // here duplicated energy and showed a second lobe offset from the main bloom (dual-filter left for shader layout; not used).
                _r.UpdateBloomUpsampleDescriptorSet(_r._viewBloomDown[i + 1], _r._viewBloomDown[i]);
                RenderPassBeginInfo rpUpTemp = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloom1),
                    Framebuffer = _r._fbBloom1,
                    RenderArea = sciBloom1Full,
                    ClearValueCount = 1,
                    PClearValues = &cUp
                };

                _r._vk!.CmdBeginRenderPass(cmd, &rpUpTemp, SubpassContents.Inline);
                _r._vk.CmdSetViewport(cmd, 0, 1, &vpUp);
                _r._vk.CmdSetScissor(cmd, 0, 1, &sciUp);
                _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomUpsample);
                fixed (DescriptorSet* dsUp = &_r._dsBloomUpsample)
                {
                    _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomUpsample, 0, 1, dsUp, 0, null);
                }

                var upPush = new BloomResamplePush
                {
                    SrcW = _r._bloomDownW[i + 1],
                    SrcH = _r._bloomDownH[i + 1],
                    DstW = _r._bloomDownW[i],
                    DstH = _r._bloomDownH[i],
                    FineBlend = 0f
                };
                _r._vk.CmdPushConstants(cmd, _r._plBloomUpsample, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BloomResamplePush), &upPush);
                _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
                _r._vk.CmdEndRenderPass(cmd);
                _r._offsWrittenBloom1 = true;
                InsertWriteToSampleBarrier(cmd);

                RenderPassBeginInfo rpCopy = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloomDown[i]),
                    Framebuffer = _r._fbBloomDown[i],
                    RenderArea = sciUp,
                    ClearValueCount = 1,
                    PClearValues = &cUp
                };

                _r._vk.CmdBeginRenderPass(cmd, &rpCopy, SubpassContents.Inline);
                _r._vk.CmdSetViewport(cmd, 0, 1, &vpUp);
                _r._vk.CmdSetScissor(cmd, 0, 1, &sciUp);
                _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomCopy);
                fixed (DescriptorSet* dsCp = &_r._dsBloomGaussianSrcBloom1)
                {
                    _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomCopy, 0, 1, dsCp, 0, null);
                }

                _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
                _r._vk.CmdEndRenderPass(cmd);
                _r._offsWrittenBloomDown[i] = true;
                InsertWriteToSampleBarrier(cmd);

                // Blur each recombined stage before continuing up the chain.
                ClearValue cBlurH = cUp;
                RenderPassBeginInfo rpBlurH = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloom1),
                    Framebuffer = _r._fbBloom1,
                    RenderArea = sciBloom1Full,
                    ClearValueCount = 1,
                    PClearValues = &cBlurH
                };
                _r._vk.CmdBeginRenderPass(cmd, &rpBlurH, SubpassContents.Inline);
                _r._vk.CmdSetViewport(cmd, 0, 1, &vpUp);
                _r._vk.CmdSetScissor(cmd, 0, 1, &sciUp);
                _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomGaussian);
                fixed (DescriptorSet* dsGh = &_r._dsBloomDownSrc[i + 1])
                {
                    // Horizontal blur reads the just-written recombined level i.
                    _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomGaussian, 0, 1, dsGh, 0, null);
                }
                var pushH = new BloomGaussianPush { DirX = 1f, DirY = 0f, RadiusScale = radiusScale, Pad = 0f };
                _r._vk.CmdPushConstants(cmd, _r._plBloomGaussian, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BloomGaussianPush), &pushH);
                _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
                _r._vk.CmdEndRenderPass(cmd);
                _r._offsWrittenBloom1 = true;
                InsertWriteToSampleBarrier(cmd);

                ClearValue cBlurV = cUp;
                RenderPassBeginInfo rpBlurV = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloomDown[i]),
                    Framebuffer = _r._fbBloomDown[i],
                    RenderArea = sciUp,
                    ClearValueCount = 1,
                    PClearValues = &cBlurV
                };
                _r._vk.CmdBeginRenderPass(cmd, &rpBlurV, SubpassContents.Inline);
                _r._vk.CmdSetViewport(cmd, 0, 1, &vpUp);
                _r._vk.CmdSetScissor(cmd, 0, 1, &sciUp);
                _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomGaussian);
                fixed (DescriptorSet* dsGv = &_r._dsBloomGaussianSrcBloom1)
                {
                    _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomGaussian, 0, 1, dsGv, 0, null);
                }
                var pushV = new BloomGaussianPush { DirX = 0f, DirY = 1f, RadiusScale = radiusScale, Pad = 0f };
                _r._vk.CmdPushConstants(cmd, _r._plBloomGaussian, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BloomGaussianPush), &pushV);
                _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
                _r._vk.CmdEndRenderPass(cmd);
                InsertWriteToSampleBarrier(cmd);
            }

            ClearValue cUpHalf = new() { Color = new ClearColorValue { Float32_0 = 0f, Float32_1 = 0f, Float32_2 = 0f, Float32_3 = 0f } };
            RenderPassBeginInfo rpUpHalf = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _r.OffscreenRpFor(_r._offsWrittenBloom0),
                Framebuffer = _r._fbBloom0,
                RenderArea = sciHalf,
                ClearValueCount = 1,
                PClearValues = &cUpHalf
            };

            _r.UpdateBloomUpsampleDescriptorSet(_r._viewBloomDown[0], _r._viewBloomDown[0]);
            _r._vk!.CmdBeginRenderPass(cmd, &rpUpHalf, SubpassContents.Inline);
            _r._vk.CmdSetViewport(cmd, 0, 1, &vpHalf);
            _r._vk.CmdSetScissor(cmd, 0, 1, &sciHalf);
            _r._vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _r._pipeBloomUpsample);
            fixed (DescriptorSet* dsUpHalf = &_r._dsBloomUpsample)
            {
                // Final upsample to half-res: coarse tent only (fineBlend=0); bind down[0] twice so descriptors stay valid.
                _r._vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _r._plBloomUpsample, 0, 1, dsUpHalf, 0, null);
            }
            var upHalfPush = new BloomResamplePush
            {
                SrcW = _r._bloomDownW[0],
                SrcH = _r._bloomDownH[0],
                DstW = _r._bloomHalfW,
                DstH = _r._bloomHalfH,
                FineBlend = 0f
            };
            _r._vk.CmdPushConstants(cmd, _r._plBloomUpsample, ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BloomResamplePush), &upHalfPush);
            _r._vk.CmdDraw(cmd, 3, 1, 0, 0);
            _r._vk.CmdEndRenderPass(cmd);
            InsertWriteToSampleBarrier(cmd);
        }

        private void InsertWriteToSampleBarrier(CommandBuffer cmd)
        {
            MemoryBarrier barrier = new()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };
            _r._vk!.CmdPipelineBarrier(
                cmd,
                PipelineStageFlags.ColorAttachmentOutputBit,
                PipelineStageFlags.FragmentShaderBit,
                0,
                1,
                &barrier,
                0,
                null,
                0,
                null);
        }
    }
}
