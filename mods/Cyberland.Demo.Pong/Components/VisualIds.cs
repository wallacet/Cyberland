using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

public readonly record struct VisualIds(
    EntityId Background,
    EntityId TitleBar,
    EntityId HintBar,
    EntityId ScorePlayer,
    EntityId ScoreCpu,
    EntityId LeftPad,
    EntityId RightPad,
    EntityId Ball) : IComponent;
