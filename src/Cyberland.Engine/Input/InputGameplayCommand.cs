namespace Cyberland.Engine.Input;

/// <summary>Discriminator for <see cref="InputGameplayCommand"/>.</summary>
public enum InputGameplayCommandKind : byte
{
    /// <summary>Logical action transitioned from up to down during this frame's <see cref="IInputService.BeginFrame"/> sample.</summary>
    ActionPressed = 0,

    /// <summary>Logical action transitioned from down to up during this frame's <see cref="IInputService.BeginFrame"/> sample.</summary>
    ActionReleased = 1
}

/// <summary>
/// One logical action edge sampled during the current frame's <see cref="IInputService.BeginFrame"/>.
/// Listed in <see cref="IInputService.FrameGameplayCommands"/> and unchanged until the next <c>BeginFrame</c>.
/// </summary>
public readonly struct InputGameplayCommand
{
    /// <summary>Creates a gameplay command describing a sampled logical edge.</summary>
    /// <param name="kind">Whether this is a press or release edge.</param>
    /// <param name="id">Logical action id (same strings as <see cref="IInputService"/> queries).</param>
    public InputGameplayCommand(InputGameplayCommandKind kind, string id)
    {
        Kind = kind;
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    /// <inheritdoc cref="InputGameplayCommandKind"/>
    public InputGameplayCommandKind Kind { get; }

    /// <summary>Logical action id.</summary>
    public string Id { get; }
}
