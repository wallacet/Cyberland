using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

public readonly record struct HudTextIds(
    EntityId Title,
    EntityId GameOverLine,
    EntityId Hint,
    EntityId ScoreYou,
    EntityId ScorePlayerNum,
    EntityId ScoreCpuLabel,
    EntityId ScoreCpuNum) : IComponent;
