namespace Cyberland.Demo.Snake;

public enum Phase
{
    Title,
    Playing,
    /// <summary>Entire board filled; no empty cell for food.</summary>
    Won,
    GameOver
}
