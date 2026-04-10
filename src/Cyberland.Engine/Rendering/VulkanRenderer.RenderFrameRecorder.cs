using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

public sealed unsafe partial class VulkanRenderer
{
    /// <summary>
    /// Wrapper to keep frame-recording entrypoint separate from orchestration class body.
    /// </summary>
    private sealed class RenderFrameRecorder
    {
        private readonly VulkanRenderer _r;

        public RenderFrameRecorder(VulkanRenderer renderer) => _r = renderer;

        public void Record(CommandBuffer cmd, Framebuffer swapFb) => _r.RecordFullFrameCore(cmd, swapFb);
    }
}
