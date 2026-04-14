using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Snake;

/// <summary>ECS entities for snake sprites, food, UI quads, and HUD <see cref="Cyberland.Engine.Scene.BitmapText"/> rows.</summary>
public sealed class SnakeVisualBundle
{
    public readonly EntityId[] Segments = new EntityId[SnakeConstants.GridW * SnakeConstants.GridH];
    public EntityId Food;
    public EntityId TitleBar;
    public EntityId GoPanel;
    public EntityId ScoreBar;
    public EntityId TxtTitle;
    public EntityId TxtHintTitle;
    public EntityId TxtGameOver;
    public EntityId TxtHintGo;
    public EntityId TxtPlaying;
    public EntityId TxtScore;
}
