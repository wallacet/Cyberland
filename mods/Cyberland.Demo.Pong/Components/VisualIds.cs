using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

/// <summary>Sprite entity handles (not registered as ECS components; passed into systems from <c>OnLoad</c>).</summary>
public readonly record struct VisualIds(
    EntityId Background,
    EntityId TitleBar,
    EntityId HintBar,
    EntityId ScorePlayer,
    EntityId ScoreCpu,
    EntityId LeftPad,
    EntityId RightPad,
    EntityId Ball);
