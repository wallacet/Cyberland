using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

/// <summary>Engine <see cref="Cyberland.Engine.Scene.BitmapText"/> entities for Pong HUD (filled by <see cref="PongVisualSyncSystem"/>).</summary>
public readonly record struct PongHudTextIds(
    EntityId Title,
    EntityId GameOverLine,
    EntityId Hint,
    EntityId ScoreYou,
    EntityId ScorePlayerNum,
    EntityId ScoreCpuLabel,
    EntityId ScoreCpuNum);
