using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Maps <see cref="FramePacing"/> to Vulkan <see cref="PresentModeKHR"/> and swapchain image counts.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="VulkanRenderer"/> so unit tests can cover branching without a GPU.
/// </remarks>
public static class FramePacingPresentMode
{
    /// <summary>
    /// Chooses a present mode supported by the surface from the current pacing preference.
    /// </summary>
    /// <param name="available">Modes reported by <c>vkGetPhysicalDeviceSurfacePresentModesKHR</c>.</param>
    /// <param name="pacing">Host frame pacing.</param>
    public static PresentModeKHR SelectPresentMode(IReadOnlyList<PresentModeKHR> available, FramePacing pacing)
    {
        if (available.Count == 0)
            return PresentModeKHR.FifoKhr;

        return pacing.Mode switch
        {
            FramePacingMode.VSync => SelectVSync(available),
            FramePacingMode.Unlimited or FramePacingMode.Limited => SelectLowLatency(available),
            _ => SelectLowLatency(available)
        };
    }

    /// <summary>
    /// Computes <c>minImageCount</c> for <see cref="SwapchainCreateInfoKHR"/>: at least <paramref name="minImageCount"/> + 1,
    /// bumped to 3 when <paramref name="chosen"/> is <see cref="PresentModeKHR.MailboxKhr"/>, then clamped to <paramref name="maxImageCount"/> when non-zero.
    /// </summary>
    public static uint AdjustMinImageCount(uint minImageCount, uint maxImageCount, PresentModeKHR chosen)
    {
        uint count = minImageCount + 1;
        if (chosen == PresentModeKHR.MailboxKhr)
            count = Math.Max(count, 3u);

        if (maxImageCount > 0 && count > maxImageCount)
            count = maxImageCount;

        if (count < minImageCount)
            count = minImageCount;

        return count;
    }

    private static PresentModeKHR SelectVSync(IReadOnlyList<PresentModeKHR> available)
    {
        if (Contains(available, PresentModeKHR.FifoKhr))
            return PresentModeKHR.FifoKhr;
        if (Contains(available, PresentModeKHR.FifoRelaxedKhr))
            return PresentModeKHR.FifoRelaxedKhr;
        return SelectLowLatency(available);
    }

    private static PresentModeKHR SelectLowLatency(IReadOnlyList<PresentModeKHR> available)
    {
        if (Contains(available, PresentModeKHR.ImmediateKhr))
            return PresentModeKHR.ImmediateKhr;
        if (Contains(available, PresentModeKHR.MailboxKhr))
            return PresentModeKHR.MailboxKhr;
        if (Contains(available, PresentModeKHR.FifoKhr))
            return PresentModeKHR.FifoKhr;
        if (Contains(available, PresentModeKHR.FifoRelaxedKhr))
            return PresentModeKHR.FifoRelaxedKhr;
        return available[0];
    }

    private static bool Contains(IReadOnlyList<PresentModeKHR> available, PresentModeKHR mode)
    {
        for (var i = 0; i < available.Count; i++)
        {
            if (available[i] == mode)
                return true;
        }

        return false;
    }
}
