namespace Cyberland.Engine.Rendering;

/// <summary>
/// How the host paces frames: hardware VSync, uncapped presentation, or a CPU-limited target frame rate.
/// </summary>
/// <remarks>
/// Set via <see cref="IRenderer.FramePacing"/> (typically from the main thread when an options menu applies settings).
/// <see cref="Limited"/> uses a non-VSync Vulkan present path plus software timing so a cap below the display refresh works (e.g. 60 FPS on a 144 Hz panel).
/// </remarks>
public enum FramePacingMode
{
    /// <summary>
    /// Prefer tear-free presentation at display refresh: mailbox when available (else FIFO / relaxed FIFO).
    /// </summary>
    VSync,

    /// <summary>
    /// Prefer immediate or mailbox presentation — no CPU frame cap.
    /// </summary>
    Unlimited,

    /// <summary>
    /// Same presentation preference as <see cref="Unlimited"/>, plus a post-present delay to approximate <see cref="FramePacing.TargetFps"/>.
    /// </summary>
    Limited
}

/// <summary>
/// Frame pacing selection for <see cref="IRenderer.FramePacing"/>.
/// </summary>
/// <remarks>
/// <see cref="TargetFps"/> is only meaningful when <see cref="Mode"/> is <see cref="FramePacingMode.Limited"/>.
/// </remarks>
public readonly struct FramePacing : IEquatable<FramePacing>
{
    /// <summary>
    /// Initializes a pacing value with an explicit target FPS (used when <paramref name="mode"/> is <see cref="FramePacingMode.Limited"/>).
    /// </summary>
    /// <param name="mode">VSync, uncapped, or CPU-limited.</param>
    /// <param name="targetFps">Desired frames per second when <paramref name="mode"/> is <see cref="FramePacingMode.Limited"/>; ignored otherwise.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is <see cref="FramePacingMode.Limited"/> and <paramref name="targetFps"/> is outside 1–1000.</exception>
    public FramePacing(FramePacingMode mode, int targetFps = 0)
    {
        Mode = mode;
        TargetFps = targetFps;
        if (mode == FramePacingMode.Limited)
            ValidateTargetFps(targetFps);
    }

    /// <summary>Active pacing mode.</summary>
    public FramePacingMode Mode { get; }

    /// <summary>Target frames per second when <see cref="Mode"/> is <see cref="FramePacingMode.Limited"/>.</summary>
    public int TargetFps { get; }

    /// <summary>Hardware VSync (mailbox when available, else FIFO).</summary>
    public static FramePacing VSync => new(FramePacingMode.VSync);

    /// <summary>Uncapped frame rate (non-FIFO presentation when available).</summary>
    public static FramePacing Unlimited => new(FramePacingMode.Unlimited);

    /// <summary>
    /// Caps the frame rate in software using <paramref name="targetFps"/> (after GPU present).
    /// </summary>
    /// <param name="targetFps">Must be between 1 and 1000 inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetFps"/> is outside the supported range.</exception>
    public static FramePacing Limited(int targetFps) => new(FramePacingMode.Limited, targetFps);

    /// <inheritdoc />
    public bool Equals(FramePacing other) => Mode == other.Mode && TargetFps == other.TargetFps;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FramePacing other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Mode, TargetFps);

    /// <summary>Equality comparison.</summary>
    public static bool operator ==(FramePacing left, FramePacing right) => left.Equals(right);

    /// <summary>Inequality comparison.</summary>
    public static bool operator !=(FramePacing left, FramePacing right) => !left.Equals(right);

    private static void ValidateTargetFps(int targetFps)
    {
        const int min = 1;
        const int max = 1000;
        if (targetFps < min || targetFps > max)
            throw new ArgumentOutOfRangeException(nameof(targetFps), targetFps, $"Target FPS must be between {min} and {max} inclusive.");
    }
}
