namespace Cyberland.Engine.Input;

/// <summary>
/// Stable action/axis identifier used by the input service and binding store.
/// </summary>
public readonly record struct InputActionId
{
    /// <summary>Create a lightweight identifier from a stable string id.</summary>
    public InputActionId(string id) => Id = id;

    /// <summary>Action or axis id, for example <c>cyberland.demo/move_x</c>.</summary>
    public string Id { get; }

    /// <summary>Implicit conversion to the underlying id string.</summary>
    public static implicit operator string(InputActionId value) => value.Id;
}
