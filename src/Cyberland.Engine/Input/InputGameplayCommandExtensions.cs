namespace Cyberland.Engine.Input;

/// <summary>Helpers for reading <see cref="IInputService.FrameGameplayCommands"/> without indexing manually.</summary>
public static class InputGameplayCommandExtensions
{
    /// <summary>
    /// True when <paramref name="actionId"/> had a press edge this frame (same condition that increments
    /// <see cref="IInputService.ConsumePressed"/> pending counts).
    /// </summary>
    public static bool HasActionPressedThisFrame(this IInputService input, string actionId)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        var frame = input.FrameGameplayCommands;
        for (var i = 0; i < frame.Count; i++)
        {
            var c = frame[i];
            if (c.Kind == InputGameplayCommandKind.ActionPressed && string.Equals(c.Id, actionId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>True when either action had a press edge this frame.</summary>
    public static bool HasAnyActionPressedThisFrame(this IInputService input, string actionIdA, string actionIdB) =>
        input.HasActionPressedThisFrame(actionIdA) || input.HasActionPressedThisFrame(actionIdB);

    /// <summary>
    /// True when <paramref name="actionId"/> had a release edge this frame (same condition that increments
    /// <see cref="IInputService.ConsumeReleased"/> pending counts).
    /// </summary>
    public static bool HasActionReleasedThisFrame(this IInputService input, string actionId)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        var frame = input.FrameGameplayCommands;
        for (var i = 0; i < frame.Count; i++)
        {
            var c = frame[i];
            if (c.Kind == InputGameplayCommandKind.ActionReleased && string.Equals(c.Id, actionId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
