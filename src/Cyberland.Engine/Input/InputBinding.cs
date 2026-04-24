namespace Cyberland.Engine.Input;

/// <summary>
/// A single action/axis binding from one physical control, optionally scaled for axis composition.
/// </summary>
public readonly record struct InputBinding
{
    /// <summary>Create a binding from <paramref name="control"/> with optional <paramref name="scale"/>.</summary>
    public InputBinding(InputControl control, float scale = 1f)
    {
        Control = control;
        Scale = scale;
    }

    /// <summary>The physical input control read by this binding.</summary>
    public InputControl Control { get; }

    /// <summary>
    /// Scale applied to the control value for axis reads. For button/key controls this is typically <c>1</c> or <c>-1</c>.
    /// </summary>
    public float Scale { get; }
}
