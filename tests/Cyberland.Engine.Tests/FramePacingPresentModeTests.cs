using Cyberland.Engine.Rendering;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Tests;

public sealed class FramePacingPresentModeTests
{
    [Fact]
    public void SelectPresentMode_empty_defaults_to_Fifo()
    {
        var m = FramePacingPresentMode.SelectPresentMode(Array.Empty<PresentModeKHR>(), FramePacing.VSync);
        Assert.Equal(PresentModeKHR.FifoKhr, m);
    }

    [Fact]
    public void SelectPresentMode_VSync_prefers_Mailbox_then_Fifo_then_Relaxed()
    {
        var onlyFifo = new[] { PresentModeKHR.FifoKhr };
        Assert.Equal(PresentModeKHR.FifoKhr, FramePacingPresentMode.SelectPresentMode(onlyFifo, FramePacing.VSync));

        var relaxedOnly = new[] { PresentModeKHR.FifoRelaxedKhr };
        Assert.Equal(
            PresentModeKHR.FifoRelaxedKhr,
            FramePacingPresentMode.SelectPresentMode(relaxedOnly, FramePacing.VSync));

        var fifoFirst = new[] { PresentModeKHR.FifoKhr, PresentModeKHR.FifoRelaxedKhr };
        Assert.Equal(PresentModeKHR.FifoKhr, FramePacingPresentMode.SelectPresentMode(fifoFirst, FramePacing.VSync));

        var fifoAndMailbox = new[] { PresentModeKHR.FifoKhr, PresentModeKHR.MailboxKhr };
        Assert.Equal(
            PresentModeKHR.MailboxKhr,
            FramePacingPresentMode.SelectPresentMode(fifoAndMailbox, FramePacing.VSync));
    }

    [Fact]
    public void SelectPresentMode_VSync_falls_back_to_Immediate_when_no_mailbox_fifo_or_relaxed()
    {
        var immediateOnly = new[] { PresentModeKHR.ImmediateKhr };
        Assert.Equal(
            PresentModeKHR.ImmediateKhr,
            FramePacingPresentMode.SelectPresentMode(immediateOnly, FramePacing.VSync));
    }

    [Fact]
    public void SelectPresentMode_Unlimited_prefers_Immediate_Mailbox_Fifo_Relaxed()
    {
        var imm = new[] { PresentModeKHR.ImmediateKhr };
        Assert.Equal(PresentModeKHR.ImmediateKhr, FramePacingPresentMode.SelectPresentMode(imm, FramePacing.Unlimited));

        var mb = new[] { PresentModeKHR.MailboxKhr, PresentModeKHR.FifoKhr };
        Assert.Equal(PresentModeKHR.MailboxKhr, FramePacingPresentMode.SelectPresentMode(mb, FramePacing.Unlimited));

        var fifo = new[] { PresentModeKHR.FifoKhr };
        Assert.Equal(PresentModeKHR.FifoKhr, FramePacingPresentMode.SelectPresentMode(fifo, FramePacing.Unlimited));

        var relaxed = new[] { PresentModeKHR.FifoRelaxedKhr };
        Assert.Equal(
            PresentModeKHR.FifoRelaxedKhr,
            FramePacingPresentMode.SelectPresentMode(relaxed, FramePacing.Unlimited));
    }

    [Fact]
    public void SelectPresentMode_Limited_matches_Unlimited_present_choice()
    {
        var modes = new[] { PresentModeKHR.MailboxKhr, PresentModeKHR.FifoKhr };
        var u = FramePacingPresentMode.SelectPresentMode(modes, FramePacing.Unlimited);
        var l = FramePacingPresentMode.SelectPresentMode(modes, FramePacing.Limited(30));
        Assert.Equal(u, l);
    }

    [Fact]
    public void SelectPresentMode_Unlimited_first_of_unrecognized_list()
    {
        // No Immediate/Mailbox/Fifo/Relaxed match — return first entry (defensive path).
        var list = new[] { (PresentModeKHR)9999 };
        Assert.Equal((PresentModeKHR)9999, FramePacingPresentMode.SelectPresentMode(list, FramePacing.Unlimited));
    }

    [Fact]
    public void SelectPresentMode_unknown_FramePacingMode_uses_low_latency_branch()
    {
        var pacing = new FramePacing((FramePacingMode)42);
        var modes = new[] { PresentModeKHR.MailboxKhr };
        Assert.Equal(PresentModeKHR.MailboxKhr, FramePacingPresentMode.SelectPresentMode(modes, pacing));
    }

    [Theory]
    [InlineData(2u, 0u, PresentModeKHR.FifoKhr, 3u)]
    [InlineData(2u, 3u, PresentModeKHR.MailboxKhr, 3u)]
    [InlineData(2u, 2u, PresentModeKHR.MailboxKhr, 2u)]
    [InlineData(1u, 0u, PresentModeKHR.FifoKhr, 2u)]
    public void AdjustMinImageCount(uint minC, uint maxC, PresentModeKHR mode, uint expected)
    {
        Assert.Equal(expected, FramePacingPresentMode.AdjustMinImageCount(minC, maxC, mode));
    }

    [Fact]
    public void AdjustMinImageCount_clamps_below_min_when_max_invalid()
    {
        // Defensive: if maxImageCount were below minImageCount, ensure count is not left under the surface minimum.
        Assert.Equal(3u, FramePacingPresentMode.AdjustMinImageCount(3u, 2u, PresentModeKHR.FifoKhr));
    }
}
