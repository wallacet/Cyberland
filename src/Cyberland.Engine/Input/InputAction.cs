namespace Cyberland.Engine.Input;

/// <summary>
/// Named gameplay actions (move, interact) decoupled from physical keys for rebinding and mods.
/// </summary>
public readonly struct InputAction
{
    /// <summary>Creates a lightweight token comparing equal when <paramref name="id"/> matches.</summary>
    public InputAction(string id) => Id = id;
    /// <summary>Stable action name (e.g. <c>move_up</c>) looked up in <see cref="KeyBindingStore"/>.</summary>
    public string Id { get; }
}
