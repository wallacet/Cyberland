using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Snake;

public struct VisualBundle : IComponent
{
    public EntityId[] Segments;
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
