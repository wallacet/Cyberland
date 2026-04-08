namespace Cyberland.Engine.Rendering;

/// <summary>
/// Thrown when Vulkan or related graphics setup fails during <see cref="VulkanRenderer.Initialize"/>.
/// <see cref="UserMessage"/> is suitable to show to the player.
/// </summary>
public sealed class GraphicsInitializationException : Exception
{
    /// <summary>Full text for a dialog or console, including troubleshooting steps.</summary>
    public string UserMessage { get; }

    public GraphicsInitializationException(string technicalDetail, Exception? innerException = null)
        : base(technicalDetail, innerException)
    {
        UserMessage = FormatUserMessage(technicalDetail, innerException);
    }

    private static string FormatUserMessage(string technicalDetail, Exception? innerException)
    {
        var detail = technicalDetail.Trim();
        if (innerException != null)
            detail += Environment.NewLine + innerException.Message.Trim();

        return
            $"""
            Cyberland could not start 3D graphics.

            Technical detail:
            {detail}

            What you can try:
            • Update your graphics drivers from NVIDIA, AMD, or Intel, or use Windows Update (look for “optional updates” for display drivers).
            • If you use a laptop with two GPUs, open the vendor control panel (NVIDIA / AMD / Intel) and set Cyberland to use the high-performance GPU.
            • Remote desktop, some VMs, and servers often cannot use Vulkan; run the game on the physical PC with a normal display.
            • Install or repair the Vulkan runtime from https://vulkan.lunarg.com/sdk/home#windows if drivers are current but Vulkan still fails.
            • Close other apps that might lock exclusive full-screen access, then try again.

            If nothing helps, contact support and include the technical detail above.
            """;
    }
}
