using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

/// <summary>BitmapText entity handles for HUD (not registered as components).</summary>
public readonly record struct HudTextIds(
    EntityId Title,
    EntityId GameOverLine,
    EntityId Hint,
    EntityId ScoreYou,
    EntityId ScorePlayerNum,
    EntityId ScoreCpuLabel,
    EntityId ScoreCpuNum);
