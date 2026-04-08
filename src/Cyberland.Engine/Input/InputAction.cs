namespace Cyberland.Engine.Input;

/// <summary>
/// Named gameplay actions (move, interact) decoupled from physical keys for rebinding and mods.
/// </summary>
public readonly struct InputAction
{
    public InputAction(string id) => Id = id;
    public string Id { get; }
}
